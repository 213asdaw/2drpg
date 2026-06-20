namespace FightingGame
{
    public enum FighterArchetypeId
    {
        Default,
        FlameSwordsman
    }

    public enum SkillId
    {
        None,
        FlameSlashWave,
        MoltenGuard
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

        public FighterArchetypeDefinition(
            FighterArchetypeId id,
            string displayName,
            float maxHealth,
            float moveSpeed,
            float attackSpeedMultiplier,
            float damageMultiplier,
            bool hasFlameSlash,
            bool hasMoltenGuard)
        {
            Id = id;
            DisplayName = displayName;
            MaxHealth = maxHealth;
            MoveSpeed = moveSpeed;
            AttackSpeedMultiplier = attackSpeedMultiplier;
            DamageMultiplier = damageMultiplier;
            HasFlameSlash = hasFlameSlash;
            HasMoltenGuard = hasMoltenGuard;
        }
    }

    public static class FighterArchetypes
    {
        public static readonly FighterArchetypeDefinition Default = new FighterArchetypeDefinition(
            FighterArchetypeId.Default,
            "검투사",
            FightConstants.BaseMaxHealth,
            FightConstants.BaseMoveSpeed,
            1f,
            1f,
            false,
            false);

        public static readonly FighterArchetypeDefinition FlameSwordsman = new FighterArchetypeDefinition(
            FighterArchetypeId.FlameSwordsman,
            "카론",
            118f,
            5.1f,
            1.18f,
            0.9f,
            true,
            true);

        public static FighterArchetypeDefinition Get(FighterArchetypeId id)
        {
            switch (id)
            {
                case FighterArchetypeId.FlameSwordsman:
                    return FlameSwordsman;
                default:
                    return Default;
            }
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
}
