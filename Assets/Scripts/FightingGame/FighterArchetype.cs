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
            0.95f,
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
                    + " | U 독화살 (지속 피해)";
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

        public const float MoltenGuardCooldown = 10f;
        public const float MoltenGuardStartup = 0.24f;
        public const float MoltenGuardDuration = 3.5f;
        public const float MoltenGuardDamageMultiplier = 0.38f;
    }

    public static class IzSkills
    {
        public const float PoisonArrowCooldown = 4.5f;
        public const float PoisonArrowStartup = 0.32f;
        public const float PoisonArrowRecovery = 0.24f;
        public const float PoisonArrowDamage = 9f;
        public const float PoisonArrowSpeed = 16f;
        public const float PoisonArrowLifetime = 1.5f;
        public const float PoisonDuration = 2.8f;
        public const float PoisonTickDamage = 4f;
        public const float PoisonTickInterval = 0.7f;
    }
}
