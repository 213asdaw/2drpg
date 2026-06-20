namespace FightingGame
{
    public static class FightConstants
    {
        public const float PixelsPerUnit = 64f;
        public const float ArenaHalfWidth = 6f;
        public const float GroundY = -2.5f;
        public const float Gravity = -28f;
        public const float JumpVelocity = 9.5f;
        public const float MoveSpeed = 5.5f;
        public const float MaxHealth = 100f;
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
        Hitstun,
        Victory,
        Defeat
    }

    public enum AttackType
    {
        None,
        Light,
        Kick,
        Heavy
    }

    public enum MatchPhase
    {
        Intro,
        Fighting,
        RoundEnd,
        MatchEnd
    }
}
