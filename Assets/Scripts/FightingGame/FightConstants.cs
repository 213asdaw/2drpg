namespace FightingGame
{
    public static class FightConstants
    {
        public const float PixelsPerUnit = 64f;
        public const float ArenaHalfWidth = 6f;
        public const float GroundY = -2.5f;
        public const float Gravity = -28f;
        public const float JumpVelocity = 9.5f;
        public const float BaseMoveSpeed = 5.5f;
        public const float BaseMaxHealth = 100f;
        public const float MaxHealth = BaseMaxHealth;
        public const float MoveSpeed = BaseMoveSpeed;
        public const float RoundDuration = 99f;
        public const int RoundsToWin = 2;
        public const float BlockDamageMultiplier = 0.25f;
        public const float HitInvulnTime = 0.15f;
        public const float RoundEndDelay = 2.5f;
        public const float MatchEndDelay = 3f;
    }

    public enum FighterState
    {
        Idle,
        Walk,
        Jump,
        Fall,
        LightAttack,
        KickAttack,
        HeavyAttack,
        Block,
        Skill1Cast,
        Skill2Cast,
        Hitstun,
        Victory,
        Defeat
    }

    public enum AttackType
    {
        None,
        Light,
        Kick,
        Heavy,
        FlameSlash,
        MoltenGuard
    }

    public enum MatchPhase
    {
        Intro,
        Fighting,
        RoundEnd,
        MatchEnd
    }
}
