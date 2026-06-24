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

        public AttackDefinition Scale(float attackSpeedMultiplier, float damageMultiplier, float knockbackMultiplier = 1f)
        {
            float speed = Mathf.Max(0.01f, attackSpeedMultiplier);
            return new AttackDefinition(
                Type,
                Startup / speed,
                Active / speed,
                Recovery / speed,
                Damage * damageMultiplier,
                Knockback * knockbackMultiplier,
                Hitstun / speed,
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
        public bool IsPoisonArrow;
        public SpriteRenderer Renderer;
    }

    public sealed class FighterController : MonoBehaviour
    {
        private static readonly AttackDefinition BaseLightAttack = new AttackDefinition(
            AttackType.Light, 0.03f, 0.28f, 0.14f, 8f, 0.85f, 0.25f,
            new Vector2(0.92f, 0.88f), new Vector2(0.62f, 0.80f));

        private static readonly AttackDefinition BaseKickAttack = new AttackDefinition(
            AttackType.Kick, 0.16f, 0.24f, 0.26f, 12f, 1.65f, 0.32f,
            new Vector2(1.15f, 0.66f), new Vector2(0.95f, 0.42f));

        private static readonly AttackDefinition BaseHeavyAttack = new AttackDefinition(
            AttackType.Heavy, 0.55f, 0.38f, 0.52f, 20f, 3.0f, 0.45f,
            new Vector2(1.28f, 0.92f), new Vector2(0.98f, 0.84f));

        private AttackDefinition lightAttack;
        private AttackDefinition kickAttack;
        private AttackDefinition heavyAttack;

        private SpriteRenderer bodyRenderer;
        private SpriteRenderer auraRenderer;
        private SpriteRenderer attackEffectRenderer;
        private Transform bodyRoot;
        private Transform attackEffectRoot;
        private Transform projectileRoot;
        private readonly List<FlameProjectile> projectiles = new List<FlameProjectile>();

        private float velocityY;
        private float knockbackVelocityX;
        private float knockbackDelayTimer;
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
        private int poisonStacks;
        private float poisonDecayTimer;
        private float poisonTickTimer;
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
        private Sprite cachedAttackEffectSprite;
        private int cachedAttackEffectKey = int.MinValue;
        private const float VisualScale = 1.35f;
        private const float CombatScale = VisualScale;
        private const float HitboxScale = 1f;
        private const float KnockbackDeceleration = 16f;
        private const float KaronSwordVisualY = 0.64f;
        private const float KaronSwordHitboxY = 0.50f;
        private float visualGroundOffset;

        public string DisplayName { get; private set; }
        public FighterArchetypeId ArchetypeId => archetypeId;
        public FighterState State { get; private set; } = FighterState.Idle;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float HealthRatio => Mathf.Clamp01(health / maxHealth);
        public float Facing => facing;
        public bool IsAlive => health > 0f;
        public bool IsDefenseBuffActive => defenseBuffTimer > 0f || defenseBuffVisual;
        public bool IsPoisoned => poisonStacks > 0;
        public int PoisonStacks => poisonStacks;
        public bool CanAct => IsAlive && State != FighterState.Victory && State != FighterState.Defeat;
        public float Skill1CooldownRemaining => skill1Cooldown;
        public float Skill2CooldownRemaining => skill2Cooldown;
        public float Skill1CooldownMax => archetypeId == FighterArchetypeId.Iz
            ? IzSkills.PoisonArrowCooldown
            : archetypeId == FighterArchetypeId.FlameSwordsman
                ? FlameSwordsmanSkills.FlameSlashCooldown
                : 0f;
        public float Skill2CooldownMax => archetypeId == FighterArchetypeId.FlameSwordsman
            ? FlameSwordsmanSkills.MoltenGuardCooldown
            : 0f;

        public event Action<FighterController, float> Damaged;
        public event Action<FighterController, float, int> PoisonTicked;
        public event Action<FighterController, int> PoisonStacksChanged;
        public event Action<FighterController, FighterController, AttackType> LandedHit;
        public event Action<FighterController> BuffActivated;

        public void Initialize(int index, FighterArchetypeId archetype, Vector3 startPosition)
        {
            FighterArchetypeDefinition definition = FighterArchetypes.Get(archetype);
            playerIndex = index;
            archetypeId = archetype;
            DisplayName = definition.DisplayName;
            maxHealth = definition.MaxHealth;
            moveSpeed = definition.MoveSpeed;
            visualGroundOffset = definition.VisualGroundOffset;
            primaryColor = archetype == FighterArchetypeId.FlameSwordsman
                ? new Color(0.95f, 0.42f, 0.18f)
                : archetype == FighterArchetypeId.Iz
                    ? new Color(0.55f, 0.28f, 0.65f)
                    : index == 0 ? new Color(0.28f, 0.62f, 0.95f) : new Color(0.95f, 0.38f, 0.32f);
            accentColor = archetype == FighterArchetypeId.FlameSwordsman
                ? new Color(0.35f, 0.12f, 0.08f)
                : archetype == FighterArchetypeId.Iz
                    ? new Color(0.38f, 0.78f, 0.42f)
                    : index == 0 ? new Color(0.12f, 0.22f, 0.42f) : new Color(0.42f, 0.12f, 0.12f);

            float knockbackScale = archetype == FighterArchetypeId.Iz ? 0.72f : 1f;
            lightAttack = BaseLightAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier, knockbackScale);
            kickAttack = BaseKickAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier, knockbackScale);
            heavyAttack = BaseHeavyAttack.Scale(definition.AttackSpeedMultiplier, definition.DamageMultiplier, knockbackScale);

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
            knockbackVelocityX = 0f;
            knockbackDelayTimer = 0f;
            facing = playerIndex == 0 ? 1f : -1f;
            health = maxHealth;
            invulnTimer = 0f;
            hitstunTimer = 0f;
            skill1Cooldown = 0f;
            skill2Cooldown = 0f;
            defenseBuffTimer = 0f;
            defenseBuffVisual = false;
            poisonStacks = 0;
            poisonDecayTimer = 0f;
            poisonTickTimer = 0f;
            grounded = true;
            hasHitThisAttack = false;
            currentAttack = null;
            castingSkill = SkillId.None;
            transform.localScale = Vector3.one * VisualScale;
            ClearProjectiles();
            SetState(FighterState.Idle);
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
            TickPoison(deltaTime);
            TickProjectiles(deltaTime);

            if (State == FighterState.Victory || State == FighterState.Defeat)
            {
                UpdateVisuals();
                return;
            }

            if (hitstunTimer > 0f)
            {
                hitstunTimer -= deltaTime;
                if (knockbackDelayTimer > 0f)
                {
                    knockbackDelayTimer -= deltaTime;
                }
                else
                {
                    ApplyKnockback(deltaTime);
                }

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

            if (!controlsEnabled || !IsAlive)
            {
                if (IsAttackState(State))
                {
                    TickAttack(deltaTime);
                }

                ApplyGravity(deltaTime);
                ClampToArena();
                UpdateVisuals();
                return;
            }

            if (!IsAttackState(State))
            {
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
                    BeginAttack(heavyAttack, input.Horizontal);
                }
                else if (input.KickPressed && CanStartAttack())
                {
                    BeginAttack(kickAttack, input.Horizontal);
                }
                else if (input.LightPressed && CanStartAttack())
                {
                    BeginAttack(lightAttack, input.Horizontal);
                }
                else if (input.JumpPressed && grounded && !IsAttackState(State))
                {
                    velocityY = FightConstants.JumpVelocity;
                    grounded = false;
                    SetState(FighterState.Jump);
                }
                else if (Mathf.Abs(input.Horizontal) > 0.01f)
                {
                    transform.position += new Vector3(input.Horizontal * moveSpeed * deltaTime, 0f, 0f);
                    facing = Mathf.Sign(input.Horizontal);
                    SetState(grounded ? FighterState.Walk : FighterState.Fall);
                }
                else if (grounded)
                {
                    SetState(FighterState.Idle);
                }
            }

            if (IsAttackState(State))
            {
                TickAttack(deltaTime);
            }

            ApplyGravity(deltaTime);
            ClampToArena();
            UpdateVisuals();
        }

        public void ReceiveMeleeHit(FighterController attacker, AttackDefinition attack)
        {
            if (attacker == null || attack == null || attacker.hasHitThisAttack || !IsAlive)
            {
                return;
            }

            if (!attacker.IsAttackActive())
            {
                return;
            }

            if (invulnTimer > 0f)
            {
                return;
            }

            attacker.hasHitThisAttack = true;
            ApplyDamage(attacker, attack.Damage, attack.Knockback, attack.Hitstun, attack.Type);
            if (attacker.archetypeId == FighterArchetypeId.Iz)
            {
                AddPoisonStacks(IzSkills.MeleePoisonStacks);
            }
        }

        public void TryApplyHitFrom(FighterController attacker, AttackDefinition attack)
        {
            if (attacker == null || attack == null || opponent != this || attacker.hasHitThisAttack)
            {
                return;
            }

            if (!attacker.IsAttackActive())
            {
                return;
            }

            ReceiveMeleeHit(attacker, attack);
        }

        public bool IsInHitWindow() => IsAttackActive();

        public bool IsAttackActive()
        {
            if (!IsAttackState(State) || currentAttack == null)
            {
                return false;
            }

            float activeStart = currentAttack.Startup;
            float activeEnd = currentAttack.Startup + currentAttack.Active;
            return stateTimer >= activeStart && stateTimer < activeEnd;
        }

        public Bounds GetActiveHitboxBounds()
        {
            if (currentAttack == null)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            float activeProgress = GetAttackActiveProgress();
            if (activeProgress < 0f)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            float bodyCenterY = 0.95f + visualGroundOffset * 0.45f;
            float reachMin;
            float reachMax;
            float hitWidth;
            float hitHeight;
            float centerY;

            if (archetypeId == FighterArchetypeId.Iz)
            {
                float reachScale = IzSkills.MeleeReachScale;
                if (currentAttack.Type == AttackType.Kick)
                {
                    reachMin = 0.62f;
                    reachMax = 1.18f;
                    hitWidth = 0.58f;
                    hitHeight = 0.46f;
                    centerY = 0.42f;
                }
                else if (currentAttack.Type == AttackType.Heavy)
                {
                    reachMin = 0.68f;
                    reachMax = 1.08f;
                    hitWidth = 0.52f;
                    hitHeight = 0.58f;
                    centerY = currentAttack.HitboxOffset.y * HitboxScale * 0.78f;
                }
                else
                {
                    reachMin = 0.64f;
                    reachMax = 0.88f;
                    hitWidth = 0.48f;
                    hitHeight = 0.5f;
                    centerY = currentAttack.HitboxOffset.y * HitboxScale * 0.78f;
                }

                float bowReach = Mathf.Lerp(reachMin, reachMax, activeProgress) * reachScale;
                Vector3 center = transform.position + new Vector3(
                    facing * bowReach,
                    bodyCenterY - 0.95f + centerY,
                    0f);
                return new Bounds(center, new Vector3(hitWidth, hitHeight, 0.1f));
            }

            if (archetypeId == FighterArchetypeId.FlameSwordsman)
            {
                float reachScale = currentAttack.Type == AttackType.Heavy ? 1.05f : currentAttack.Type == AttackType.Kick ? 0.92f : 0.88f;
                float reach = Mathf.Lerp(0.48f, 0.98f, activeProgress) * reachScale;
                float width = currentAttack.Type == AttackType.Heavy ? 0.68f : 0.58f;
                float height = currentAttack.Type == AttackType.Heavy ? 0.72f : 0.62f;
                Vector3 center = transform.position + new Vector3(
                    facing * reach,
                    bodyCenterY - 0.95f + KaronSwordHitboxY,
                    0f);
                return new Bounds(center, new Vector3(width, height, 0.1f));
            }

            float extendScale = currentAttack.Type == AttackType.Kick ? 1.0f : 0.92f;
            if (currentAttack.Type == AttackType.Kick)
            {
                reachMin = 0.58f;
                reachMax = 1.22f;
                hitWidth = 0.88f;
                hitHeight = 0.62f;
                centerY = 0.42f;
            }
            else if (currentAttack.Type == AttackType.Heavy)
            {
                reachMin = 0.64f;
                reachMax = 1.18f;
                hitWidth = 0.92f;
                hitHeight = 0.88f;
                centerY = currentAttack.HitboxOffset.y * HitboxScale * 0.85f;
            }
            else
            {
                reachMin = currentAttack.HitboxOffset.x * 0.72f;
                reachMax = currentAttack.HitboxOffset.x;
                hitWidth = currentAttack.HitboxSize.x * 0.72f * HitboxScale;
                hitHeight = currentAttack.HitboxSize.y * 0.72f * HitboxScale;
                centerY = currentAttack.HitboxOffset.y * HitboxScale * 0.85f;
            }

            float punchReach = Mathf.Lerp(reachMin, reachMax, activeProgress) * extendScale;
            Vector3 meleeCenter = transform.position + new Vector3(
                facing * punchReach,
                bodyCenterY - 0.95f + centerY,
                0f);
            return new Bounds(
                meleeCenter,
                new Vector3(hitWidth, hitHeight, 0.1f));
        }

        private float GetAttackActiveProgress()
        {
            if (currentAttack == null || !IsAttackActive())
            {
                return -1f;
            }

            return Mathf.Clamp01((stateTimer - currentAttack.Startup) / Mathf.Max(0.01f, currentAttack.Active));
        }

        private bool CanUseSkill1()
        {
            FighterArchetypeDefinition def = FighterArchetypes.Get(archetypeId);
            return (def.HasFlameSlash || def.HasPoisonArrow) && skill1Cooldown <= 0f && CanStartAttack();
        }

        private bool CanUseSkill2()
        {
            FighterArchetypeDefinition def = FighterArchetypes.Get(archetypeId);
            return def.HasMoltenGuard && skill2Cooldown <= 0f && CanStartAttack();
        }

        private void BeginSkill1()
        {
            SnapFacingTowardOpponent();
            castingSkill = archetypeId == FighterArchetypeId.Iz ? SkillId.PoisonArrow : SkillId.FlameSlashWave;
            stateTimer = 0f;
            SetState(FighterState.Skill1Cast);
        }

        private void BeginSkill2()
        {
            SnapFacingTowardOpponent();
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
                        BuffActivated?.Invoke(this);
                    }

                    if (stateTimer >= FlameSwordsmanSkills.MoltenGuardStartup + 0.12f)
                    {
                        castingSkill = SkillId.None;
                        SetState(grounded ? FighterState.Idle : FighterState.Fall);
                    }
                }
            }
            else if (castingSkill == SkillId.PoisonArrow)
            {
                if (stateTimer >= IzSkills.PoisonArrowStartup)
                {
                    if (stateTimer - deltaTime < IzSkills.PoisonArrowStartup)
                    {
                        SpawnPoisonArrowProjectile();
                        skill1Cooldown = IzSkills.PoisonArrowCooldown;
                    }

                    if (stateTimer >= IzSkills.PoisonArrowStartup + IzSkills.PoisonArrowRecovery)
                    {
                        castingSkill = SkillId.None;
                        SetState(grounded ? FighterState.Idle : FighterState.Fall);
                    }
                }
            }
        }

        private void SpawnPoisonArrowProjectile()
        {
            GameObject projectileObject = new GameObject("PoisonArrow");
            projectileObject.transform.SetParent(null, true);
            SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
            renderer.sprite = ProceduralArt.CreatePoisonArrowProjectileSprite();
            renderer.sortingOrder = 20 + playerIndex;
            renderer.flipX = false;

            Vector3 spawnPosition = transform.position + new Vector3(facing * 0.85f, 0.92f + visualGroundOffset, 0f);
            renderer.transform.position = spawnPosition;
            projectiles.Add(new FlameProjectile
            {
                Position = spawnPosition,
                Facing = facing,
                Damage = IzSkills.PoisonArrowDamage,
                Speed = IzSkills.PoisonArrowSpeed,
                Lifetime = IzSkills.PoisonArrowLifetime,
                IsPoisonArrow = true,
                Renderer = renderer
            });
            ApplyPoisonArrowVisual(projectiles[projectiles.Count - 1]);
        }

        private static void ApplyPoisonArrowVisual(FlameProjectile projectile)
        {
            if (projectile.Renderer == null)
            {
                return;
            }

            float scale = IzSkills.PoisonArrowVisualScale;
            float direction = projectile.Facing >= 0f ? 1f : -1f;
            projectile.Renderer.flipX = false;
            projectile.Renderer.transform.localScale = new Vector3(direction * scale, scale, 1f);
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
                    if (projectile.IsPoisonArrow)
                    {
                        ApplyPoisonArrowVisual(projectile);
                    }
                    else
                    {
                        projectile.Renderer.flipX = projectile.Facing < 0f;
                    }
                }

                if (!projectile.HasHit && opponent != null && opponent.IsAlive)
                {
                    Vector3 hitCenter = projectile.IsPoisonArrow
                        ? projectile.Position + new Vector3(projectile.Facing * 0.16f, 0f, 0f)
                        : projectile.Position;
                    Vector3 hitSize = projectile.IsPoisonArrow
                        ? new Vector3(0.46f, 0.16f, 0.1f)
                        : new Vector3(1.1f, 0.55f, 0.1f);
                    Bounds projectileBounds = new Bounds(hitCenter, hitSize);
                    if (projectileBounds.Intersects(opponent.GetHurtboxBounds()))
                    {
                        projectile.HasHit = true;
                        if (projectile.IsPoisonArrow)
                        {
                            opponent.ApplyDamage(this, projectile.Damage, 0.95f, 0.22f, AttackType.PoisonArrow);
                            opponent.AddPoisonStacks(IzSkills.SkillPoisonStacks);
                        }
                        else
                        {
                            opponent.ApplyDamage(this, projectile.Damage, 2.2f, 0.35f, AttackType.FlameSlash);
                        }
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
                damage *= IsDefenseBuffActive
                    ? FightConstants.MoltenGuardBlockDamageMultiplier
                    : FightConstants.BlockDamageMultiplier;
            }
            else if (IsDefenseBuffActive)
            {
                damage *= FlameSwordsmanSkills.MoltenGuardDamageMultiplier;
            }

            health = Mathf.Max(0f, health - damage);
            invulnTimer = FightConstants.HitInvulnTime;

            float knockbackDirection = Mathf.Sign(transform.position.x - attacker.transform.position.x);
            if (knockbackDirection == 0f)
            {
                knockbackDirection = attacker.facing;
            }

            float knockbackDistance = knockback * (blocking ? 0.35f : 1f);
            knockbackVelocityX = knockbackDirection * Mathf.Sqrt(2f * KnockbackDeceleration * knockbackDistance);
            float knockbackDuration = Mathf.Sqrt(2f * knockbackDistance / KnockbackDeceleration);
            float stunDuration = blocking ? hitstun * 0.5f : hitstun;
            if (attackType == AttackType.Heavy)
            {
                stunDuration += blocking ? FightConstants.HeavyAttackStunBonus * 0.45f : FightConstants.HeavyAttackStunBonus;
            }

            hitstunTimer = Mathf.Max(stunDuration, knockbackDuration + (attackType == AttackType.Heavy && !blocking
                ? FightConstants.HeavyAttackKnockbackDelay
                : 0f));
            knockbackDelayTimer = attackType == AttackType.Heavy && !blocking
                ? FightConstants.HeavyAttackKnockbackDelay
                : 0f;
            SetState(FighterState.Hitstun);
            Damaged?.Invoke(this, damage);
            attacker.LandedHit?.Invoke(attacker, this, attackType);

            if (health <= 0f)
            {
                SetState(FighterState.Defeat);
            }
        }

        public void AddPoisonStacks(int stackGain)
        {
            if (stackGain <= 0)
            {
                return;
            }

            if (poisonStacks < IzSkills.MaxPoisonStacks)
            {
                poisonStacks = Mathf.Min(IzSkills.MaxPoisonStacks, poisonStacks + stackGain);
            }

            poisonDecayTimer = IzSkills.PoisonStackDecayTime;
            if (poisonTickTimer <= 0f && poisonStacks > 0)
            {
                poisonTickTimer = IzSkills.PoisonTickInterval;
            }

            PoisonStacksChanged?.Invoke(this, poisonStacks);
        }

        private void TickPoison(float deltaTime)
        {
            if (poisonStacks <= 0 || !IsAlive)
            {
                poisonStacks = 0;
                poisonDecayTimer = 0f;
                return;
            }

            poisonDecayTimer -= deltaTime;
            if (poisonDecayTimer <= 0f)
            {
                poisonStacks = 0;
                poisonDecayTimer = 0f;
                poisonTickTimer = 0f;
                return;
            }

            poisonTickTimer -= deltaTime;
            while (poisonTickTimer <= 0f && poisonStacks > 0 && IsAlive)
            {
                float tickDamage = IzSkills.GetPoisonTickDamage(poisonStacks);
                health = Mathf.Max(0f, health - tickDamage);
                PoisonTicked?.Invoke(this, tickDamage, poisonStacks);
                poisonTickTimer += IzSkills.PoisonTickInterval;
                if (health <= 0f)
                {
                    SetState(FighterState.Defeat);
                    poisonStacks = 0;
                    poisonDecayTimer = 0f;
                    return;
                }
            }
        }

        public Bounds GetHurtboxBounds()
        {
            float centerY = 0.98f + visualGroundOffset * 0.45f;
            return new Bounds(
                transform.position + new Vector3(0f, centerY, 0f),
                new Vector3(0.82f * HitboxScale, 1.55f * HitboxScale, 0.1f));
        }

        private void BuildVisuals()
        {
            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(transform, false);
            bodyRoot = bodyObject.transform;
            bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = 10 + playerIndex;

            GameObject auraObject = new GameObject("DefenseAura");
            auraObject.transform.SetParent(bodyRoot, false);
            auraRenderer = auraObject.AddComponent<SpriteRenderer>();
            auraRenderer.sprite = ProceduralArt.CreateRectSprite(24, 24, new Color(1f, 0.55f, 0.12f, 0.35f), "DefenseAura");
            auraRenderer.sortingOrder = 8 + playerIndex;
            auraRenderer.enabled = false;

            GameObject projectileContainer = new GameObject("Projectiles");
            projectileContainer.transform.SetParent(transform, false);
            projectileRoot = projectileContainer.transform;

            GameObject attackEffectObject = new GameObject("AttackEffect");
            attackEffectObject.transform.SetParent(bodyRoot, false);
            attackEffectRoot = attackEffectObject.transform;
            attackEffectRenderer = attackEffectObject.AddComponent<SpriteRenderer>();
            attackEffectRenderer.sortingOrder = 15 + playerIndex;
            attackEffectRenderer.enabled = false;
        }

        private void BeginAttack(AttackDefinition attack, float inputHorizontal = 0f)
        {
            if (Mathf.Abs(inputHorizontal) > 0.01f)
            {
                facing = Mathf.Sign(inputHorizontal);
            }

            currentAttack = attack;
            hasHitThisAttack = false;
            stateTimer = 0f;
            if (!grounded)
            {
                velocityY = 0f;
            }

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
                if (currentAttack.Type == AttackType.Heavy && grounded)
                {
                    transform.position += new Vector3(facing * 0.32f * deltaTime, 0f, 0f);
                }

                ResolveAttackHit();
            }

            if (stateTimer >= currentAttack.TotalDuration)
            {
                currentAttack = null;
                hasHitThisAttack = false;
                SetState(grounded ? FighterState.Idle : FighterState.Fall);
            }
        }

        private void ResolveAttackHit()
        {
            if (opponent == null || hasHitThisAttack || currentAttack == null || !IsAttackActive())
            {
                return;
            }

            Bounds hitbox = GetActiveHitboxBounds();
            Bounds hurtbox = opponent.GetHurtboxBounds();
            if (!hitbox.Intersects(hurtbox))
            {
                return;
            }

            opponent.ReceiveMeleeHit(this, currentAttack);
        }

        private bool CanStartAttack()
        {
            return State != FighterState.Block && !IsAttackState(State) && !IsSkillCastState(State);
        }

        private void ApplyKnockback(float deltaTime)
        {
            if (Mathf.Abs(knockbackVelocityX) <= 0.01f)
            {
                knockbackVelocityX = 0f;
                return;
            }

            transform.position += new Vector3(knockbackVelocityX * deltaTime, 0f, 0f);
            knockbackVelocityX = Mathf.MoveTowards(knockbackVelocityX, 0f, KnockbackDeceleration * deltaTime);
        }

        private void ApplyGravity(float deltaTime)
        {
            if (!grounded)
            {
                if (IsAttackState(State))
                {
                    velocityY = 0f;
                }
                else
                {
                    velocityY += FightConstants.Gravity * deltaTime;
                    transform.position += new Vector3(0f, velocityY * deltaTime, 0f);
                }
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

        private void SnapFacingTowardOpponent()
        {
            if (opponent == null)
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
                    : archetypeId == FighterArchetypeId.Iz
                        ? ProceduralArt.CreateIzBody(faceRight)
                        : ProceduralArt.CreateFighterBody(primaryColor, accentColor, faceRight);
                lastVisualFacingRight = faceRight;
            }

            bodyRenderer.sprite = cachedBodySprite;

            float bob = State == FighterState.Walk ? Mathf.Sin(Time.time * 12f) * 0.04f : 0f;
            float squash = State == FighterState.Block ? -0.08f : 0f;
            float lean = 0f;
            if (State == FighterState.HeavyAttack && currentAttack != null && currentAttack.Type == AttackType.Heavy)
            {
                if (stateTimer < currentAttack.Startup)
                {
                    float windUp = currentAttack.Startup <= 0f ? 1f : stateTimer / currentAttack.Startup;
                    squash = Mathf.Lerp(0f, -0.12f, windUp);
                    lean = -facing * windUp * 0.08f;
                }
                else if (stateTimer < currentAttack.Startup + currentAttack.Active)
                {
                    float strike = (stateTimer - currentAttack.Startup) / Mathf.Max(0.01f, currentAttack.Active);
                    squash = Mathf.Lerp(-0.12f, 0.06f, strike);
                    lean = facing * strike * 0.1f;
                }
                else
                {
                    float recover = (stateTimer - currentAttack.Startup - currentAttack.Active)
                        / Mathf.Max(0.01f, currentAttack.Recovery);
                    squash = Mathf.Lerp(0.06f, 0f, recover);
                    lean = facing * Mathf.Lerp(0.1f, 0f, recover);
                }
            }

            transform.localScale = Vector3.one * VisualScale;
            if (bodyRoot != null)
            {
                bodyRoot.localPosition = new Vector3(lean, visualGroundOffset + bob, 0f);
                bodyRoot.localScale = new Vector3(1f, 1f + squash, 1f);
            }

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
            else if (archetypeId == FighterArchetypeId.Iz && State == FighterState.Skill1Cast)
            {
                auraRenderer.enabled = false;
                bodyRenderer.color = new Color(0.82f, 1f, 0.72f, 1f);
            }
            else if (archetypeId == FighterArchetypeId.Iz && poisonStacks > 0)
            {
                auraRenderer.enabled = false;
                float stackRatio = poisonStacks / (float)IzSkills.MaxPoisonStacks;
                float pulse = 0.88f + Mathf.Sin(Time.time * (10f + stackRatio * 6f)) * 0.12f;
                bodyRenderer.color = new Color(
                    0.78f - stackRatio * 0.18f,
                    pulse + stackRatio * 0.08f,
                    0.52f - stackRatio * 0.08f,
                    1f);
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

            UpdateAttackEffectVisuals(bob);
            UpdateSkillCastVisuals(bob);
        }

        private void UpdateSkillCastVisuals(float bob)
        {
            if (attackEffectRenderer == null || attackEffectRoot == null)
            {
                return;
            }

            if (State != FighterState.Skill1Cast || archetypeId != FighterArchetypeId.Iz || castingSkill != SkillId.PoisonArrow)
            {
                if (!IsAttackState(State))
                {
                    attackEffectRenderer.enabled = false;
                    cachedAttackEffectKey = int.MinValue;
                }

                return;
            }

            float progress = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, IzSkills.PoisonArrowStartup));
            int progressBucket = Mathf.Clamp(Mathf.FloorToInt(progress * 8f), 0, 7);
            int effectKey = 5000 + progressBucket;
            if (effectKey != cachedAttackEffectKey)
            {
                cachedAttackEffectKey = effectKey;
                cachedAttackEffectSprite = ProceduralArt.CreateBowDrawEffect(progress);
            }

            bool faceRight = facing >= 0f;
            float facingSign = faceRight ? 1f : -1f;
            attackEffectRenderer.enabled = true;
            attackEffectRenderer.sprite = cachedAttackEffectSprite;
            attackEffectRenderer.flipX = !faceRight;
            attackEffectRoot.localRotation = Quaternion.identity;
            attackEffectRoot.localPosition = new Vector3(facingSign * 0.42f, 0.88f + bob, 0f);
            attackEffectRoot.localScale = Vector3.one * 1.15f;
            attackEffectRenderer.color = new Color(0.78f, 1f, 0.72f, 0.95f);
        }

        private void UpdateAttackEffectVisuals(float bob)
        {
            if (attackEffectRenderer == null || attackEffectRoot == null)
            {
                return;
            }

            if (State == FighterState.Skill1Cast && archetypeId == FighterArchetypeId.Iz)
            {
                return;
            }

            if (!IsAttackState(State) || currentAttack == null)
            {
                attackEffectRenderer.enabled = false;
                cachedAttackEffectKey = int.MinValue;
                return;
            }

            if (currentAttack.Type == AttackType.Heavy)
            {
                UpdateHeavyAttackEffectVisuals(bob);
                return;
            }

            float recoveryStart = currentAttack.Startup + currentAttack.Active;
            if (stateTimer >= recoveryStart)
            {
                attackEffectRenderer.enabled = false;
                cachedAttackEffectKey = int.MinValue;
                return;
            }

            if (stateTimer < currentAttack.Startup)
            {
                attackEffectRenderer.enabled = false;
                cachedAttackEffectKey = int.MinValue;
                return;
            }

            float activeProgress = (stateTimer - currentAttack.Startup) / Mathf.Max(0.01f, currentAttack.Active);
            int progressBucket = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(activeProgress) * 8f), 0, 7);
            bool faceRight = facing >= 0f;
            float facingSign = faceRight ? 1f : -1f;

            attackEffectRenderer.enabled = true;
            attackEffectRenderer.flipX = !faceRight;

            if (archetypeId == FighterArchetypeId.Iz)
            {
                int effectKey = 6000 + (int)currentAttack.Type * 10 + progressBucket;
                if (effectKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = effectKey;
                    cachedAttackEffectSprite = ProceduralArt.CreateBowDrawEffect(activeProgress);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float extend = Mathf.Sin(activeProgress * Mathf.PI) * 0.35f;
                attackEffectRoot.localRotation = Quaternion.identity;
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.38f + extend),
                    0.82f + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * (currentAttack.Type == AttackType.Heavy ? 1.25f : 1.05f);
                attackEffectRenderer.color = new Color(0.78f, 1f, 0.72f, 0.92f);
            }
            else if (archetypeId == FighterArchetypeId.FlameSwordsman)
            {
                int effectKey = 1000 + (int)currentAttack.Type * 10 + progressBucket;
                if (effectKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = effectKey;
                    cachedAttackEffectSprite = ProceduralArt.CreateSwordSwingEffect(
                        currentAttack.Type,
                        progressBucket / 7f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float startAngle = faceRight ? -108f : 108f;
                float endAngle = faceRight ? 18f : -18f;
                float angle = Mathf.Lerp(startAngle, endAngle, activeProgress);
                attackEffectRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.15f + activeProgress * 0.35f),
                    KaronSwordVisualY + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * 1.1f;
                attackEffectRenderer.color = new Color(1f, 0.88f, 0.55f, 0.92f);
            }
            else if (currentAttack.Type == AttackType.Kick)
            {
                int effectKey = 2000 + progressBucket;
                if (effectKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = effectKey;
                    cachedAttackEffectSprite = ProceduralArt.CreateKickEffect(progressBucket / 7f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float extend = Mathf.Sin(activeProgress * Mathf.PI) * 0.88f;
                attackEffectRoot.localRotation = Quaternion.identity;
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.52f + extend),
                    0.45f + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * 1.15f;
                attackEffectRenderer.color = new Color(1f, 0.92f, 0.82f, 0.95f);
            }
            else
            {
                int effectKey = 3000 + (int)currentAttack.Type * 10 + progressBucket;
                if (effectKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = effectKey;
                    cachedAttackEffectSprite = ProceduralArt.CreatePunchEffect(
                        currentAttack.Type,
                        progressBucket / 7f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float extend = Mathf.Sin(activeProgress * Mathf.PI) * 0.55f;
                attackEffectRoot.localRotation = Quaternion.identity;
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.42f + extend),
                    0.92f + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one;
                attackEffectRenderer.color = new Color(1f, 0.9f, 0.82f, 0.95f);
            }
        }

        private void UpdateHeavyAttackEffectVisuals(float bob)
        {
            bool faceRight = facing >= 0f;
            float facingSign = faceRight ? 1f : -1f;
            float recoveryStart = currentAttack.Startup + currentAttack.Active;

            if (stateTimer >= currentAttack.TotalDuration)
            {
                attackEffectRenderer.enabled = false;
                cachedAttackEffectKey = int.MinValue;
                return;
            }

            attackEffectRenderer.enabled = true;
            attackEffectRenderer.flipX = !faceRight;

            if (stateTimer < currentAttack.Startup)
            {
                float windUp = currentAttack.Startup <= 0f ? 1f : stateTimer / currentAttack.Startup;
                int progressBucket = Mathf.Clamp(Mathf.FloorToInt(windUp * 4f), 0, 3);
                int effectKey = archetypeId == FighterArchetypeId.Iz ? 7100 + progressBucket : 4000 + progressBucket;
                if (effectKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = effectKey;
                    cachedAttackEffectSprite = archetypeId == FighterArchetypeId.Iz
                        ? ProceduralArt.CreateBowDrawEffect(windUp * 0.42f)
                        : archetypeId == FighterArchetypeId.FlameSwordsman
                            ? ProceduralArt.CreateSwordWindUpEffect(progressBucket / 3f)
                            : ProceduralArt.CreatePunchWindUpEffect(progressBucket / 3f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                attackEffectRoot.localRotation = Quaternion.identity;
                if (archetypeId == FighterArchetypeId.Iz)
                {
                    attackEffectRoot.localPosition = new Vector3(
                        -facingSign * (0.08f + windUp * 0.12f),
                        0.84f + bob,
                        0f);
                    attackEffectRoot.localScale = Vector3.one * (0.95f + windUp * 0.2f);
                    attackEffectRenderer.color = new Color(0.72f, 0.95f, 0.68f, 0.7f + windUp * 0.25f);
                }
                else
                {
                    attackEffectRoot.localPosition = new Vector3(
                        -facingSign * (0.12f + windUp * 0.18f),
                        (archetypeId == FighterArchetypeId.FlameSwordsman ? KaronSwordVisualY - 0.04f : 0.98f) + bob,
                        0f);
                    attackEffectRoot.localScale = Vector3.one * (0.85f + windUp * 0.15f);
                    attackEffectRenderer.color = new Color(0.85f, 0.78f, 0.72f, 0.65f + windUp * 0.2f);
                }

                return;
            }

            if (stateTimer >= recoveryStart)
            {
                attackEffectRenderer.enabled = false;
                cachedAttackEffectKey = int.MinValue;
                return;
            }

            float activeProgress = (stateTimer - currentAttack.Startup) / Mathf.Max(0.01f, currentAttack.Active);
            int strikeBucket = Mathf.Clamp(Mathf.FloorToInt(activeProgress * 8f), 0, 7);

            if (archetypeId == FighterArchetypeId.Iz)
            {
                int strikeKey = 7200 + strikeBucket;
                float drawProgress = 0.45f + activeProgress * 0.55f;
                if (strikeKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = strikeKey;
                    cachedAttackEffectSprite = ProceduralArt.CreateBowDrawEffect(drawProgress);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float extend = Mathf.Sin(activeProgress * Mathf.PI) * 0.52f;
                attackEffectRoot.localRotation = Quaternion.identity;
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.34f + extend),
                    0.8f + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * (1.05f + activeProgress * 0.35f);
                attackEffectRenderer.color = new Color(0.78f, 1f, 0.72f, 0.95f);
            }
            else if (archetypeId == FighterArchetypeId.FlameSwordsman)
            {
                int strikeKey = 5000 + strikeBucket;
                if (strikeKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = strikeKey;
                    cachedAttackEffectSprite = ProceduralArt.CreateSwordSwingEffect(
                        AttackType.Heavy,
                        strikeBucket / 7f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float startAngle = faceRight ? -108f : 108f;
                float endAngle = faceRight ? 18f : -18f;
                float angle = Mathf.Lerp(startAngle, endAngle, activeProgress);
                attackEffectRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.15f + activeProgress * 0.45f),
                    KaronSwordVisualY + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * 1.35f;
                attackEffectRenderer.color = new Color(1f, 0.72f, 0.38f, 0.95f);
            }
            else
            {
                int strikeKey = 5000 + strikeBucket;
                if (strikeKey != cachedAttackEffectKey)
                {
                    cachedAttackEffectKey = strikeKey;
                    cachedAttackEffectSprite = ProceduralArt.CreatePunchEffect(AttackType.Heavy, strikeBucket / 7f);
                }

                attackEffectRenderer.sprite = cachedAttackEffectSprite;
                float extend = Mathf.Sin(activeProgress * Mathf.PI) * 0.98f;
                attackEffectRoot.localRotation = Quaternion.identity;
                attackEffectRoot.localPosition = new Vector3(
                    facingSign * (0.48f + extend),
                    0.92f + bob,
                    0f);
                attackEffectRoot.localScale = Vector3.one * 1.25f;
                attackEffectRenderer.color = new Color(1f, 0.9f, 0.82f, 0.95f);
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
