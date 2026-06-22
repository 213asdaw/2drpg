using System;
using System.Collections.Generic;
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

        public AttackDefinition Scale(float attackSpeedMultiplier, float damageMultiplier)
        {
            return new AttackDefinition(
                Type,
                Startup * attackSpeedMultiplier,
                Active * attackSpeedMultiplier,
                Recovery * attackSpeedMultiplier,
                Damage * damageMultiplier,
                Knockback,
                Hitstun * attackSpeedMultiplier,
                HitboxSize,
                HitboxOffset);
        }
    }

    internal sealed class FlameProjectile
    {
        public Vector3 Position;
        public float Facing;
        public float Damage;
        public float Speed;
        public float Lifetime;
        public bool HasHit;
        public SpriteRenderer Renderer;
    }

    public sealed class FighterController : MonoBehaviour
    {
        private static readonly AttackDefinition BaseLightAttack = new AttackDefinition(
            AttackType.Light, 0.08f, 0.1f, 0.18f, 8f, 1.2f, 0.25f,
            new Vector2(0.9f, 0.8f), new Vector2(0.75f, 1.1f));

        private static readonly AttackDefinition BaseKickAttack = new AttackDefinition(
            AttackType.Kick, 0.12f, 0.12f, 0.22f, 12f, 1.8f, 0.32f,
            new Vector2(1.0f, 0.55f), new Vector2(0.85f, 0.55f));

        private static readonly AttackDefinition BaseHeavyAttack = new AttackDefinition(
            AttackType.Heavy, 0.22f, 0.14f, 0.34f, 20f, 2.8f, 0.45f,
            new Vector2(1.1f, 1.0f), new Vector2(0.95f, 1.0f));

        private AttackDefinition lightAttack;
        private AttackDefinition kickAttack;
        private AttackDefinition heavyAttack;

        private SpriteRenderer bodyRenderer;
        private SpriteRenderer auraRenderer;
        private SpriteRenderer hitboxRenderer;
        private Transform projectileRoot;
        private readonly List<FlameProjectile> projectiles = new List<FlameProjectile>();

        private float velocityY;
        private float facing = 1f;
        private float health;
        private float maxHealth;
        private float moveSpeed;
        private float stateTimer;
        private float invulnTimer;
        private float hitstunTimer;
        private float skill1Cooldown;
        private float skill2Cooldown;
        private float defenseBuffTimer;
        private bool defenseBuffVisual;
        private AttackDefinition currentAttack;
        private SkillId castingSkill = SkillId.None;
        private bool grounded = true;
        private bool hasHitThisAttack;
        private int playerIndex;
        private FighterArchetypeId archetypeId = FighterArchetypeId.Default;
        private Color primaryColor;
        private Color accentColor;
        private FighterController opponent;
        private bool lastVisualFacingRight = true;
        private Sprite cachedBodySprite;
        private const float VisualScale = 1.35f;

        public string DisplayName { get; private set; }
        public FighterArchetypeId ArchetypeId => archetypeId;
        public FighterState State { get; private set; } = FighterState.Idle;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float HealthRatio => Mathf.Clamp01(health / maxHealth);
        public float Facing => facing;
        public bool IsAlive => health > 0f;
        public bool IsDefenseBuffActive => defenseBuffTimer > 0f || defenseBuffVisual;
        public bool CanAct => IsAlive && State != FighterState.Victory && State != FighterState.Defeat;

        public event Action<FighterController, float> Damaged;
        public event Action<FighterController, FighterController, AttackType> LandedHit;

        public void Initialize(int index, FighterArchetypeId archetype, Vector3 startPosition)
        {
            FighterArchetypeDefinition definition = FighterArchetypes.Get(archetype);
            playerIndex = index;
            archetypeId = archetype;
            DisplayName = definition.DisplayName;
            maxHealth = definition.MaxHealth;
            moveSpeed = definition.MoveSpeed;
            primaryColor = archetype == FighterArchetypeId.FlameSwordsman
                ? new Color(0.95f, 0.42f, 0.18f)
                : index == 0 ? new Color(0.28f, 0.62f, 0.95f) : new Color(0.95f, 0.38f, 0.32f);
            accentColor = archetype == FighterArchetypeId.FlameSwordsman
                ? new Color(0.35f, 0.12f, 0.08f)
                : index == 0 ? new Color(0.12f, 0.22f, 0.42f) : new Color(0.42f, 0.12f, 0.12f);

            lightAttack = BaseLightAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier);
            kickAttack = BaseKickAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier);
            heavyAttack = BaseHeavyAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier);

            transform.position = startPosition;
            facing = index == 0 ? 1f : -1f;
            health = maxHealth;
            skill1Cooldown = 0f;
            skill2Cooldown = 0f;
            defenseBuffTimer = 0f;
            defenseBuffVisual = false;
            ClearProjectiles();
            transform.localScale = Vector3.one * VisualScale;

            if (bodyRenderer == null)
            {
                BuildVisuals();
            }

            cachedBodySprite = null;
            SetState(FighterState.Idle);
            UpdateVisuals();
        }

        public void Initialize(int index, string displayName, Color primary, Color accent, Vector3 startPosition)
        {
            Initialize(index, FighterArchetypeId.Default, startPosition);
            DisplayName = displayName;
            primaryColor = primary;
            accentColor = accent;
            cachedBodySprite = null;
            UpdateVisuals();
        }

        public void SetOpponent(FighterController other) => opponent = other;

        public void ResetForRound(Vector3 startPosition)
        {
            transform.position = startPosition;
            velocityY = 0f;
            facing = playerIndex == 0 ? 1f : -1f;
            health = maxHealth;
            invulnTimer = 0f;
            hitstunTimer = 0f;
            skill1Cooldown = 0f;
            skill2Cooldown = 0f;
            defenseBuffTimer = 0f;
            defenseBuffVisual = false;
            grounded = true;
            hasHitThisAttack = false;
            currentAttack = null;
            castingSkill = SkillId.None;
            transform.localScale = Vector3.one * VisualScale;
            ClearProjectiles();
            SetState(FighterState.Idle);
            UpdateFacingTowardOpponent();
            UpdateVisuals();
        }

        public void SetMatchResult(bool won) => SetState(won ? FighterState.Victory : FighterState.Defeat);

        public void ApplyNetworkDisplayState(Vector3 position, float healthValue, FighterState stateValue, float facingValue, bool defenseBuffActive)
        {
            transform.position = position;
            health = healthValue;
            facing = facingValue;
            State = stateValue;
            defenseBuffVisual = defenseBuffActive;
            UpdateVisuals();
        }

        public void SetDisplayName(string displayName) => DisplayName = displayName;

        public void Tick(float deltaTime, FighterInputSnapshot input, bool controlsEnabled)
        {
            skill1Cooldown = Mathf.Max(0f, skill1Cooldown - deltaTime);
            skill2Cooldown = Mathf.Max(0f, skill2Cooldown - deltaTime);
            defenseBuffTimer = Mathf.Max(0f, defenseBuffTimer - deltaTime);
            if (defenseBuffTimer <= 0f)
            {
                defenseBuffVisual = false;
            }

            invulnTimer = Mathf.Max(0f, invulnTimer - deltaTime);
            TickProjectiles(deltaTime);

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

            if (IsSkillCastState(State))
            {
                TickSkillCast(deltaTime);
                ApplyGravity(deltaTime);
                ClampToArena();
                UpdateVisuals();
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
            else if (input.Skill2Pressed && CanUseSkill2())
            {
                BeginSkill2();
            }
            else if (input.Skill1Pressed && CanUseSkill1())
            {
                BeginSkill1();
            }
            else if (input.HeavyPressed && CanStartAttack())
            {
                BeginAttack(heavyAttack);
            }
            else if (input.KickPressed && CanStartAttack())
            {
                BeginAttack(kickAttack);
            }
            else if (input.LightPressed && CanStartAttack())
            {
                BeginAttack(lightAttack);
            }
            else if (input.JumpPressed && grounded)
            {
                velocityY = FightConstants.JumpVelocity;
                grounded = false;
                SetState(FighterState.Jump);
            }
            else if (Mathf.Abs(input.Horizontal) > 0.01f && grounded)
            {
                transform.position += new Vector3(input.Horizontal * moveSpeed * deltaTime, 0f, 0f);
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
            if (!hitbox.Intersects(GetHurtboxBounds()))
            {
                return;
            }

            attacker.hasHitThisAttack = true;
            ApplyDamage(attacker, attack.Damage, attack.Knockback, attack.Hitstun, attack.Type);
        }

        public bool IsAttackActive()
        {
            if (!IsAttackState(State) || currentAttack == null)
            {
                return false;
            }

            float activeEnd = currentAttack.Startup + currentAttack.Active;
            return stateTimer >= currentAttack.Startup && stateTimer <= activeEnd;
        }

        public Bounds GetActiveHitboxBounds()
        {
            if (currentAttack == null)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Vector3 center = transform.position + new Vector3(facing * currentAttack.HitboxOffset.x, currentAttack.HitboxOffset.y, 0f);
            return new Bounds(center, new Vector3(currentAttack.HitboxSize.x, currentAttack.HitboxSize.y, 0.1f));
        }

        private bool CanUseSkill1()
        {
            FighterArchetypeDefinition def = FighterArchetypes.Get(archetypeId);
            return def.HasFlameSlash && grounded && skill1Cooldown <= 0f && CanStartAttack();
        }

        private bool CanUseSkill2()
        {
            FighterArchetypeDefinition def = FighterArchetypes.Get(archetypeId);
            return def.HasMoltenGuard && grounded && skill2Cooldown <= 0f && CanStartAttack();
        }

        private void BeginSkill1()
        {
            castingSkill = SkillId.FlameSlashWave;
            stateTimer = 0f;
            SetState(FighterState.Skill1Cast);
        }

        private void BeginSkill2()
        {
            castingSkill = SkillId.MoltenGuard;
            stateTimer = 0f;
            SetState(FighterState.Skill2Cast);
        }

        private void TickSkillCast(float deltaTime)
        {
            stateTimer += deltaTime;
            if (castingSkill == SkillId.FlameSlashWave)
            {
                if (stateTimer >= FlameSwordsmanSkills.FlameSlashStartup)
                {
                    if (stateTimer - deltaTime < FlameSwordsmanSkills.FlameSlashStartup)
                    {
                        SpawnFlameSlashProjectile();
                        skill1Cooldown = FlameSwordsmanSkills.FlameSlashCooldown;
                    }

                    if (stateTimer >= FlameSwordsmanSkills.FlameSlashStartup + FlameSwordsmanSkills.FlameSlashRecovery)
                    {
                        castingSkill = SkillId.None;
                        SetState(grounded ? FighterState.Idle : FighterState.Fall);
                    }
                }
            }
            else if (castingSkill == SkillId.MoltenGuard)
            {
                if (stateTimer >= FlameSwordsmanSkills.MoltenGuardStartup)
                {
                    if (stateTimer - deltaTime < FlameSwordsmanSkills.MoltenGuardStartup)
                    {
                        defenseBuffTimer = FlameSwordsmanSkills.MoltenGuardDuration;
                        skill2Cooldown = FlameSwordsmanSkills.MoltenGuardCooldown;
                        LandedHit?.Invoke(this, opponent, AttackType.MoltenGuard);
                    }

                    if (stateTimer >= FlameSwordsmanSkills.MoltenGuardStartup + 0.12f)
                    {
                        castingSkill = SkillId.None;
                        SetState(grounded ? FighterState.Idle : FighterState.Fall);
                    }
                }
            }
        }

        private void SpawnFlameSlashProjectile()
        {
            GameObject projectileObject = new GameObject("FlameSlash");
            projectileObject.transform.SetParent(projectileRoot, false);
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = ProceduralArt.CreateFlameSlashProjectileSprite();
            renderer.sortingOrder = 20 + playerIndex;

            Vector3 spawnPosition = transform.position + new Vector3(facing * 1.1f, 1.05f, 0f);
            projectiles.Add(new FlameProjectile
            {
                Position = spawnPosition,
                Facing = facing,
                Damage = FlameSwordsmanSkills.FlameSlashProjectileDamage,
                Speed = FlameSwordsmanSkills.FlameSlashProjectileSpeed,
                Lifetime = FlameSwordsmanSkills.FlameSlashProjectileLifetime,
                Renderer = renderer
            });
        }

        private void TickProjectiles(float deltaTime)
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                FlameProjectile projectile = projectiles[i];
                projectile.Position += new Vector3(projectile.Facing * projectile.Speed * deltaTime, 0f, 0f);
                projectile.Lifetime -= deltaTime;

                if (projectile.Renderer != null)
                {
                    projectile.Renderer.transform.position = projectile.Position;
                    projectile.Renderer.flipX = projectile.Facing < 0f;
                }

                if (!projectile.HasHit && opponent != null && opponent.IsAlive)
                {
                    Bounds projectileBounds = new Bounds(projectile.Position, new Vector3(1.1f, 0.55f, 0.1f));
                    if (projectileBounds.Intersects(opponent.GetHurtboxBounds()))
                    {
                        projectile.HasHit = true;
                        opponent.ApplyDamage(this, projectile.Damage, 2.2f, 0.35f, AttackType.FlameSlash);
                    }
                }

                projectiles[i] = projectile;
                if (projectile.Lifetime <= 0f || Mathf.Abs(projectile.Position.x) > FightConstants.ArenaHalfWidth + 1f)
                {
                    if (projectile.Renderer != null)
                    {
                        Destroy(projectile.Renderer.gameObject);
                    }

                    projectiles.RemoveAt(i);
                }
            }
        }

        private void ApplyDamage(FighterController attacker, float damage, float knockback, float hitstun, AttackType attackType)
        {
            bool blocking = State == FighterState.Block && grounded;
            if (blocking)
            {
                damage *= FightConstants.BlockDamageMultiplier;
            }
            else if (IsDefenseBuffActive)
            {
                damage *= FlameSwordsmanSkills.MoltenGuardDamageMultiplier;
            }

            health = Mathf.Max(0f, health - damage);
            invulnTimer = FightConstants.HitInvulnTime;
            hitstunTimer = blocking ? hitstun * 0.5f : hitstun;

            float knockbackDirection = Mathf.Sign(transform.position.x - attacker.transform.position.x);
            if (knockbackDirection == 0f)
            {
                knockbackDirection = attacker.facing;
            }

            transform.position += new Vector3(knockbackDirection * knockback * (blocking ? 0.35f : 1f), 0f, 0f);
            ClampToArena();
            SetState(FighterState.Hitstun);
            Damaged?.Invoke(this, damage);
            attacker.LandedHit?.Invoke(attacker, this, attackType);

            if (health <= 0f)
            {
                SetState(FighterState.Defeat);
            }
        }

        public Bounds GetHurtboxBounds()
        {
            return new Bounds(transform.position + new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 1.5f, 0.1f));
        }

        private void BuildVisuals()
        {
            bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = 10 + playerIndex;

            GameObject auraObject = new GameObject("DefenseAura");
            auraObject.transform.SetParent(transform, false);
            auraRenderer = auraObject.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = ProceduralArt.CreateRectSprite(24, 24, new Color(1f, 0.55f, 0.12f, 0.35f), "DefenseAura");
            auraRenderer.sortingOrder = 8 + playerIndex;
            auraRenderer.enabled = false;

            GameObject projectileContainer = new GameObject("Projectiles");
            projectileContainer.transform.SetParent(transform, false);
            projectileRoot = projectileContainer.transform;

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
            return grounded && State != FighterState.Block && !IsAttackState(State) && !IsSkillCastState(State);
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
            if (opponent == null || IsAttackState(State) || IsSkillCastState(State) || State == FighterState.Hitstun)
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
                cachedBodySprite = archetypeId == FighterArchetypeId.FlameSwordsman
                    ? ProceduralArt.CreateFlameSwordsmanBody(faceRight)
                    : ProceduralArt.CreateFighterBody(primaryColor, accentColor, faceRight);
                lastVisualFacingRight = faceRight;
            }

            bodyRenderer.sprite = cachedBodySprite;

            float bob = State == FighterState.Walk ? Mathf.Sin(Time.time * 12f) * 0.04f : 0f;
            float squash = State == FighterState.Block ? -0.08f : 0f;
            transform.localScale = new Vector3(1f, 1f + squash, 1f);
            bodyRenderer.transform.localPosition = new Vector3(0f, bob, 0f);

            if (archetypeId == FighterArchetypeId.FlameSwordsman && IsDefenseBuffActive)
            {
                auraRenderer.enabled = true;
                auraRenderer.transform.localPosition = new Vector3(0f, 1.05f + bob, 0f);
                float pulse = 0.85f + Mathf.Sin(Time.time * 12f) * 0.15f;
                auraRenderer.transform.localScale = new Vector3(pulse * 1.6f, pulse * 1.9f, 1f);
                bodyRenderer.color = new Color(1f, 0.55f + pulse * 0.2f, 0.35f, 1f);
            }
            else if (archetypeId == FighterArchetypeId.FlameSwordsman && State == FighterState.Skill1Cast)
            {
                auraRenderer.enabled = false;
                bodyRenderer.color = new Color(1f, 0.78f, 0.52f, 1f);
            }
            else if (archetypeId == FighterArchetypeId.FlameSwordsman && (State == FighterState.Idle || State == FighterState.Walk))
            {
                auraRenderer.enabled = false;
                float pulse = 0.97f + Mathf.Sin(Time.time * 3.5f) * 0.03f;
                bodyRenderer.color = new Color(pulse, pulse * 0.98f, pulse * 0.97f, 1f);
            }
            else if (IsDefenseBuffActive)
            {
                auraRenderer.enabled = true;
                auraRenderer.transform.localPosition = new Vector3(0f, 0.95f + bob, 0f);
                float pulse = 0.9f + Mathf.Sin(Time.time * 10f) * 0.12f;
                auraRenderer.transform.localScale = new Vector3(pulse * 1.5f, pulse * 1.8f, 1f);
                bodyRenderer.color = new Color(1f, 0.82f, 0.55f, 1f);
            }
            else if (State == FighterState.Block)
            {
                auraRenderer.enabled = false;
                bodyRenderer.color = new Color(0.85f, 0.95f, 1f, 1f);
            }
            else if (State == FighterState.Hitstun)
            {
                auraRenderer.enabled = false;
                bodyRenderer.color = new Color(1f, 0.65f, 0.65f, 1f);
            }
            else if (invulnTimer > 0f)
            {
                auraRenderer.enabled = false;
                bodyRenderer.color = new Color(1f, 1f, 1f, 0.75f);
            }
            else
            {
                auraRenderer.enabled = false;
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

        private void ClearProjectiles()
        {
            foreach (FlameProjectile projectile in projectiles)
            {
                if (projectile.Renderer != null)
                {
                    Destroy(projectile.Renderer.gameObject);
                }
            }

            projectiles.Clear();
        }

        private static bool IsAttackState(FighterState state)
        {
            return state == FighterState.LightAttack
                || state == FighterState.KickAttack
                || state == FighterState.HeavyAttack;
        }

        private static bool IsSkillCastState(FighterState state)
        {
            return state == FighterState.Skill1Cast || state == FighterState.Skill2Cast;
        }

        private static FighterState AttackTypeToState(AttackType type)
        {
            switch (type)
            {
                case AttackType.Light: return FighterState.LightAttack;
                case AttackType.Kick: return FighterState.KickAttack;
                case AttackType.Heavy: return FighterState.HeavyAttack;
                default: return FighterState.Idle;
            }
        }

        private void OnDestroy() => ClearProjectiles();
    }
}
