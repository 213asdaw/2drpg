namespace FightingGame
{
    public static class FightSessionConfig
    {
        public static FighterArchetypeId PlayerOneArchetype { get; private set; } = FighterArchetypeId.FlameSwordsman;
        public static FighterArchetypeId PlayerTwoArchetype { get; private set; } = FighterArchetypeId.Default;
        public static FighterArchetypeId LocalPlayerArchetype { get; private set; } = FighterArchetypeId.Default;

        public static void SetOfflineSelection(FighterArchetypeId playerOne, FighterArchetypeId playerTwo)
        {
            PlayerOneArchetype = playerOne;
            PlayerTwoArchetype = playerTwo;
        }

        public static void SetLocalArchetype(FighterArchetypeId archetype)
        {
            LocalPlayerArchetype = archetype;
        }

        public static void ApplyHostSlotArchetype()
        {
            PlayerOneArchetype = LocalPlayerArchetype;
        }

        public static FighterArchetypeId GetArchetypeForSlot(int slotIndex)
        {
            return slotIndex == 0 ? PlayerOneArchetype : PlayerTwoArchetype;
        }
    }
}
