using System;
using UnityEngine;

namespace FightingGame
{
    public sealed class AttackDefinition
    {
        public AttackType Type { get; }
        public float Startup { get; }
        public float Active { get; }
        public float Recovery { get; }
        public float Damage { get; }
        public float Knockback { get; }
        public float Hitstun { get; }
        public Vector2 HitboxSize { get; }
        public Vector2 HitboxOffset { get; }

        public AttackDefinition(
            AttackType type,
            float startup,
            float active,
            float recovery,
            float damage,
            float knockback,
            float hitstun,
            Vector2 hitboxSize,
            Vector2 hitboxOffset)
        {
            Type = type;
            Startup = startup;
            Active = active;
            Recovery = recovery;
            Damage = damage;
            Knockback = knockback;
            Hitstun = hitstun;
            HitboxSize = hitboxSize;
            HitboxOffset = hitboxOffset;
        }

        public float TotalDuration => Startup + Active + Recovery;
    }

    public sealed class FighterController : MonoBehaviour
    {
        private static readonly AttackDefinition LightAttack = new AttackDefinition(
            AttackType.Light,
            0.08f,
            0.1f,
            0.18f,
            8f,
            1.2f,
            0.25f,
            new Vector2(0.9f, 0.8f),
            new Vector2(0.75f, 1.1f));

        private static readonly AttackDefinition KickAttack = new AttackDefinition(
            AttackType.Kick,
            0.12f,
            0.12f,
            0.22f,
            12f,
            1.8f,
            0.32f,
            new Vector2(1.0f, 0.55f),
            new Vector2(0.85f, 0.55f));

        private static readonly AttackDefinition HeavyAttack = new AttackDefinition(
            AttackType.Heavy,
            0.22f,
            0.14f,
            0.34f,
            20f,
            2.8f,
            0.45f,
            new Vector2(1.1f, 1.0f),
            new Vector2(0.95f, 1.0f));

        private SpriteRenderer bodyRenderer;
        private SpriteRenderer hitboxRenderer;
        private float velocityY;
        private float facing = 1f;
        private float health = FightConstants.MaxHealth;
        private float stateTimer;
        private float invulnTimer;
        private float hitstunTimer;
        private AttackDefinition currentAttack;
        private bool grounded = true;
        private bool hasHitThisAttack;
        private int playerIndex;
        private Color primaryColor;
        private Color accentColor;
        private FighterController opponent;
        private bool lastVisualFacingRight = true;
        private Sprite cachedBodySprite;

        public string DisplayName { get; private set; }
        public FighterState State { get; private set; } = FighterState.Idle;
        public float Health => health;
        public float HealthRatio => Mathf.Clamp01(health / FightConstants.MaxHealth);
        public float Facing => facing;
        public bool IsAlive => health > 0f;
        public bool CanAct => IsAlive && State != FighterState.Victory && State != FighterState.Defeat;

        public event Action<FighterController, float> Damaged;
        public event Action<FighterController, FighterController, AttackType> LandedHit;

        public void Initialize(int index, string displayName, Color primary, Color accent, Vector3 startPosition)
        {
            playerIndex = index;
            DisplayName = displayName;
            primaryColor = primary;
            accentColor = accent;
            transform.position = startPosition;
            facing = index == 0 ? 1f : -1f;
            health = FightConstants.MaxHealth;
            BuildVisuals();
            SetState(FighterState.Idle);
        }

        public void SetOpponent(FighterController other)
        {
            opponent = other;
        }

        public void ResetForRound(Vector3 startPosition)
        {
            transform.position = startPosition;
            velocityY = 0f;
            facing = playerIndex == 0 ? 1f : -1f;
            health = FightConstants.MaxHealth;
            invulnTimer = 0f;
            hitstunTimer = 0f;
            grounded = true;
            hasHitThisAttack = false;
            currentAttack = null;
            SetState(FighterState.Idle);
            UpdateFacingTowardOpponent();
            UpdateVisuals();
        }

        public void SetMatchResult(bool won)
        {
            SetState(won ? FighterState.Victory : FighterState.Defeat);
        }

        public void Tick(float deltaTime, FighterInputSnapshot input, bool controlsEnabled)
        {
            invulnTimer = Mathf.Max(0f, invulnTimer - deltaTime);

            if (State == FighterState.Victory || State == FighterState.Defeat)
            {
                UpdateVisuals();
                return;
            }

            if (hitstunTimer > 0f)
            {
                hitstunTimer -= deltaTime;
                ApplyGravity(deltaTime);
                ClampToArena();
                UpdateVisuals();
                if (hitstunTimer <= 0f && IsAlive)
                {
                    SetState(grounded ? FighterState.Idle : FighterState.Fall);
                }

                return;
            }

            if (IsAttackState(State))
            {
                TickAttack(deltaTime);
                ApplyGravity(deltaTime);
                ClampToArena();
                UpdateVisuals();
                return;
            }

            if (!controlsEnabled || !IsAlive)
            {
                ApplyGravity(deltaTime);
                ClampToArena();
                UpdateVisuals();
                return;
            }

            UpdateFacingTowardOpponent();

            if (input.BlockHeld && grounded)
            {
                SetState(FighterState.Block);
            }
            else if (input.HeavyPressed && CanStartAttack())
            {
                BeginAttack(HeavyAttack);
            }
            else if (input.KickPressed && CanStartAttack())
            {
                BeginAttack(KickAttack);
            }
            else if (input.LightPressed && CanStartAttack())
            {
                BeginAttack(LightAttack);
            }
            else if (input.JumpPressed && grounded)
            {
                velocityY = FightConstants.JumpVelocity;
                grounded = false;
                SetState(FighterState.Jump);
            }
            else if (Mathf.Abs(input.Horizontal) > 0.01f && grounded)
            {
                transform.position += new Vector3(input.Horizontal * FightConstants.MoveSpeed * deltaTime, 0f, 0f);
                facing = Mathf.Sign(input.Horizontal);
                SetState(FighterState.Walk);
            }
            else if (grounded)
            {
                SetState(FighterState.Idle);
            }

            ApplyGravity(deltaTime);
            ClampToArena();
            UpdateVisuals();
        }

        public void TryApplyHitFrom(FighterController attacker, AttackDefinition attack)
        {
            if (!attacker.IsAttackActive() || attacker.hasHitThisAttack || opponent != this)
            {
                return;
            }

            if (invulnTimer > 0f || !IsAlive)
            {
                return;
            }

            Bounds hitbox = attacker.GetActiveHitboxBounds();
            Bounds hurtbox = GetHurtboxBounds();
            if (!hitbox.Intersects(hurtbox))
            {
                return;
            }

            attacker.hasHitThisAttack = true;
            float damage = attack.Damage;
            bool blocking = State == FighterState.Block && grounded;
            if (blocking)
            {
                damage *= FightConstants.BlockDamageMultiplier;
            }

            health = Mathf.Max(0f, health - damage);
            invulnTimer = FightConstants.HitInvulnTime;
            hitstunTimer = blocking ? attack.Hitstun * 0.5f : attack.Hitstun;

            float knockbackDirection = Mathf.Sign(transform.position.x - attacker.transform.position.x);
            if (knockbackDirection == 0f)
            {
                knockbackDirection = attacker.facing;
            }

            transform.position += new Vector3(knockbackDirection * attack.Knockback * (blocking ? 0.35f : 1f), 0f, 0f);
            ClampToArena();

            SetState(FighterState.Hitstun);
            Damaged?.Invoke(this, damage);
            attacker.LandedHit?.Invoke(attacker, this, attack.Type);

            if (health <= 0f)
            {
                SetState(FighterState.Defeat);
            }
        }

        public bool IsAttackActive()
        {
            if (!IsAttackState(State) || currentAttack == null)
            {
                return false;
            }

            float activeStart = currentAttack.Startup;
            float activeEnd = activeStart + currentAttack.Active;
            return stateTimer >= activeStart && stateTimer <= activeEnd;
        }

        public Bounds GetActiveHitboxBounds()
        {
            if (currentAttack == null)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Vector3 center = transform.position + new Vector3(facing * currentAttack.HitboxOffset.x, currentAttack.HitboxOffset.y, 0f);
            Vector3 size = new Vector3(currentAttack.HitboxSize.x, currentAttack.HitboxSize.y, 0.1f);
            return new Bounds(center, size);
        }

        private Bounds GetHurtboxBounds()
        {
            return new Bounds(transform.position + new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 1.5f, 0.1f));
        }

        private void BuildVisuals()
        {
            bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = 10 + playerIndex;

            GameObject hitboxVisual = new GameObject("HitboxDebug");
            hitboxVisual.transform.SetParent(transform, false);
            hitboxRenderer = hitboxVisual.AddComponent<SpriteRenderer>();
            hitboxRenderer.sprite = ProceduralArt.CreateRectSprite(8, 8, new Color(1f, 0.2f, 0.2f, 0.35f), "Hitbox");
            hitboxRenderer.sortingOrder = 30;
            hitboxRenderer.enabled = false;
        }

        private void BeginAttack(AttackDefinition attack)
        {
            currentAttack = attack;
            hasHitThisAttack = false;
            stateTimer = 0f;
            SetState(AttackTypeToState(attack.Type));
        }

        private void TickAttack(float deltaTime)
        {
            stateTimer += deltaTime;
            if (currentAttack == null)
            {
                SetState(grounded ? FighterState.Idle : FighterState.Fall);
                return;
            }

            if (IsAttackActive() && opponent != null)
            {
                opponent.TryApplyHitFrom(this, currentAttack);
            }

            if (stateTimer >= currentAttack.TotalDuration)
            {
                currentAttack = null;
                hasHitThisAttack = false;
                SetState(grounded ? FighterState.Idle : FighterState.Fall);
            }
        }

        private bool CanStartAttack()
        {
            return grounded && State != FighterState.Block && !IsAttackState(State);
        }

        private void ApplyGravity(float deltaTime)
        {
            if (!grounded)
            {
                velocityY += FightConstants.Gravity * deltaTime;
                transform.position += new Vector3(0f, velocityY * deltaTime, 0f);
            }

            if (transform.position.y <= FightConstants.GroundY)
            {
                transform.position = new Vector3(transform.position.x, FightConstants.GroundY, transform.position.z);
                velocityY = 0f;
                grounded = true;
                if (State == FighterState.Jump || State == FighterState.Fall)
                {
                    SetState(FighterState.Idle);
                }
            }
            else
            {
                grounded = false;
                if (State == FighterState.Idle || State == FighterState.Walk)
                {
                    SetState(FighterState.Fall);
                }
            }
        }

        private void ClampToArena()
        {
            float x = Mathf.Clamp(transform.position.x, -FightConstants.ArenaHalfWidth, FightConstants.ArenaHalfWidth);
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }

        private void UpdateFacingTowardOpponent()
        {
            if (opponent == null || IsAttackState(State) || State == FighterState.Hitstun)
            {
                return;
            }

            float direction = Mathf.Sign(opponent.transform.position.x - transform.position.x);
            if (direction != 0f)
            {
                facing = direction;
            }
        }

        private void SetState(FighterState newState)
        {
            if (State == newState)
            {
                return;
            }

            State = newState;
            stateTimer = 0f;
        }

        private void UpdateVisuals()
        {
            bool faceRight = facing >= 0f;
            if (cachedBodySprite == null || faceRight != lastVisualFacingRight)
            {
                cachedBodySprite = ProceduralArt.CreateFighterBody(primaryColor, accentColor, faceRight);
                lastVisualFacingRight = faceRight;
            }

            bodyRenderer.sprite = cachedBodySprite;
            bodyRenderer.flipX = false;

            float bob = State == FighterState.Walk ? Mathf.Sin(Time.time * 12f) * 0.04f : 0f;
            float squash = State == FighterState.Block ? -0.08f : 0f;
            transform.localScale = new Vector3(1f, 1f + squash, 1f);
            bodyRenderer.transform.localPosition = new Vector3(0f, bob, 0f);

            if (State == FighterState.Block)
            {
                bodyRenderer.color = new Color(0.85f, 0.95f, 1f, 1f);
            }
            else if (State == FighterState.Hitstun)
            {
                bodyRenderer.color = new Color(1f, 0.65f, 0.65f, 1f);
            }
            else if (invulnTimer > 0f)
            {
                bodyRenderer.color = new Color(1f, 1f, 1f, 0.75f);
            }
            else
            {
                bodyRenderer.color = Color.white;
            }

            if (IsAttackActive() && currentAttack != null)
            {
                hitboxRenderer.enabled = true;
                Bounds hitbox = GetActiveHitboxBounds();
                hitboxRenderer.transform.position = hitbox.center;
                hitboxRenderer.transform.localScale = new Vector3(
                    currentAttack.HitboxSize.x * 8f,
                    currentAttack.HitboxSize.y * 8f,
                    1f);
            }
            else
            {
                hitboxRenderer.enabled = false;
            }
        }

        private static bool IsAttackState(FighterState state)
        {
            return state == FighterState.LightAttack
                || state == FighterState.KickAttack
                || state == FighterState.HeavyAttack;
        }

        private static FighterState AttackTypeToState(AttackType type)
        {
            switch (type)
            {
                case AttackType.Light:
                    return FighterState.LightAttack;
                case AttackType.Kick:
                    return FighterState.KickAttack;
                case AttackType.Heavy:
                    return FighterState.HeavyAttack;
                default:
                    return FighterState.Idle;
            }
        }
    }
}
