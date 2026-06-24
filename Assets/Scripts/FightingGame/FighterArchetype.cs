namespace FightingGame
{
    public enum FighterArchetypeId
    {
        Default,
        FlameSwordsman,
        Iz
    }

    public enum SkillId
    {
        None,
        FlameSlashWave,
        MoltenGuard,
        PoisonArrow
    }

    public sealed class FighterArchetypeDefinition
    {
        public FighterArchetypeId Id { get; }
        public string DisplayName { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float AttackSpeedMultiplier { get; }
        public float DamageMultiplier { get; }
        public bool HasFlameSlash { get; }
        public bool HasMoltenGuard { get; }
        public bool HasPoisonArrow { get; }
        public float VisualGroundOffset { get; }

        public FighterArchetypeDefinition(
            FighterArchetypeId id,
            string displayName,
            float maxHealth,
            float moveSpeed,
            float attackSpeedMultiplier,
            float damageMultiplier,
            bool hasFlameSlash,
            bool hasMoltenGuard,
            bool hasPoisonArrow = false,
            float visualGroundOffset = 0f)
        {
            Id = id;
            DisplayName = displayName;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            DamageMultiplier = damageMultiplier;
            HasFlameSlash = hasFlameSlash;
            HasMoltenGuard = hasMoltenGuard;
            HasPoisonArrow = hasPoisonArrow;
            VisualGroundOffset = visualGroundOffset;
        }
    }

    public static class FighterArchetypes
    {
        public static readonly FighterArchetypeDefinition Default = new FighterArchetypeDefinition(
            FighterArchetypeId.Default,
            "격투가",
            125f,
            5.5f,
            1.42f,
            1.1f,
            false,
            false,
            false,
            0.25f);

        public static readonly FighterArchetypeDefinition FlameSwordsman = new FighterArchetypeDefinition(
            FighterArchetypeId.FlameSwordsman,
            "카론",
            118f,
            5.1f,
            1.12f,
            0.9f,
            true,
            true,
            false,
            0f);

        public static readonly FighterArchetypeDefinition Iz = new FighterArchetypeDefinition(
            FighterArchetypeId.Iz,
            "이즈",
            108f,
            5.6f,
            1.08f,
            0.76f,
            false,
            false,
            true,
            0.1f);

        public static FighterArchetypeDefinition Get(FighterArchetypeId id)
        {
            switch (id)
            {
                case FighterArchetypeId.FlameSwordsman:
                    return FlameSwordsman;
                case FighterArchetypeId.Iz:
                    return Iz;
                default:
                    return Default;
            }
        }

        public static string GetSelectionSummary(FighterArchetypeId id)
        {
            FighterArchetypeDefinition definition = Get(id);
            if (definition.HasFlameSlash || definition.HasMoltenGuard)
            {
                return definition.DisplayName + " — HP " + definition.MaxHealth.ToString("0")
                    + " | U/I 스킬 (화염 베기, 용암 가드)";
            }

            if (definition.HasPoisonArrow)
            {
                return definition.DisplayName + " — HP " + definition.MaxHealth.ToString("0")
                    + " | 사거리 ↑ · 순간 딜 ↓ | U 독화살 (독 스택 DOT)";
            }

            return definition.DisplayName + " — HP " + definition.MaxHealth.ToString("0")
                + " | 공격·공속 ↑ | 스킬 없음";
        }
    }

    public static class FlameSwordsmanSkills
    {
        public const float FlameSlashCooldown = 5f;
        public const float FlameSlashStartup = 0.28f;
        public const float FlameSlashRecovery = 0.22f;
        public const float FlameSlashProjectileDamage = 16f;
        public const float FlameSlashProjectileSpeed = 13f;
        public const float FlameSlashProjectileLifetime = 1.4f;
        public const float FlameSlashVisualScale = 1.38f;

        public const float MoltenGuardCooldown = 10f;
        public const float MoltenGuardStartup = 0.24f;
        public const float MoltenGuardDuration = 3.5f;
        public const float MoltenGuardDamageMultiplier = 0.38f;
    }

    public static class IzSkills
    {
        public const float MeleeReachScale = 1.14f;
        public const float PoisonArrowVisualScale = 0.72f;

        public const float PoisonArrowCooldown = 4.5f;
        public const float PoisonArrowStartup = 0.32f;
        public const float PoisonArrowRecovery = 0.24f;
        public const float PoisonArrowDamage = 5f;
        public const float PoisonArrowSpeed = 19f;
        public const float PoisonArrowLifetime = 2.65f;

        public const float MeleeArrowVisualScale = 0.94f;
        public const float MeleeArrowFlatDamage = 1f;
        public const float MeleeArrowKnockbackMultiplier = 0.42f;
        public const float MeleeArrowHitstunMultiplier = 0.55f;
        public const float MeleeArrowSpeedLight = 13f;
        public const float MeleeArrowSpeedKick = 11f;
        public const float MeleeArrowSpeedHeavy = 7.5f;
        public const float MeleeArrowLifetimeLight = 0.44f;
        public const float MeleeArrowLifetimeKick = 0.5f;
        public const float MeleeArrowLifetimeHeavy = 0.72f;

        public const int MaxPoisonStacks = 5;
        public const int MeleePoisonStacks = 1;
        public const int HeavyMeleePoisonStacks = 2;
        public const int SkillPoisonStacks = 2;
        public const float PoisonStackDecayTime = 4f;
        public const float PoisonTickInterval = 0.65f;

        // stacks 1~5 — each step increases tick damage clearly (2/4/6/8/10).
        public static float GetPoisonTickDamage(int stacks)
        {
            switch (stacks)
            {
                case 5: return 8f;
                case 4: return 7f;
                case 3: return 5f;
                case 2: return 3f;
                case 1: return 2f;
                default: return 0f;
            }
        }
        public static int GetMeleePoisonStacks(AttackType attackType)
        {
            return attackType == AttackType.Heavy ? HeavyMeleePoisonStacks : MeleePoisonStacks;
        }

        public static float GetMeleeArrowSpeed(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.Heavy: return MeleeArrowSpeedHeavy;
                case AttackType.Kick: return MeleeArrowSpeedKick;
                default: return MeleeArrowSpeedLight;
            }
        }

        public static float GetMeleeArrowLifetime(AttackType attackType)
        {
            switch (attackType)
            {
                case AttackType.Heavy: return MeleeArrowLifetimeHeavy;
                case AttackType.Kick: return MeleeArrowLifetimeKick;
                default: return MeleeArrowLifetimeLight;
            }
        }
    }
}
