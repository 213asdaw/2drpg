using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public struct NetworkFighterInput : INetworkSerializable
    {
        public float Horizontal;
        public bool BlockHeld;
        public bool JumpPressed;
        public bool LightPressed;
        public bool KickPressed;
        public bool HeavyPressed;
        public bool Skill1Pressed;
        public bool Skill2Pressed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref BlockHeld);
            serializer.SerializeValue(ref JumpPressed);
            serializer.SerializeValue(ref LightPressed);
            serializer.SerializeValue(ref KickPressed);
            serializer.SerializeValue(ref HeavyPressed);
            serializer.SerializeValue(ref Skill1Pressed);
            serializer.SerializeValue(ref Skill2Pressed);
        }

        public FighterInputSnapshot ToSnapshot()
        {
            return new FighterInputSnapshot(
                Horizontal,
                JumpPressed,
                BlockHeld,
                LightPressed,
                KickPressed,
                HeavyPressed,
                Skill1Pressed,
                Skill2Pressed);
        }

        public static NetworkFighterInput FromSnapshot(FighterInputSnapshot snapshot)
        {
            return new NetworkFighterInput
            {
                Horizontal = snapshot.Horizontal,
                BlockHeld = snapshot.BlockHeld,
                JumpPressed = snapshot.JumpPressed,
                LightPressed = snapshot.LightPressed,
                KickPressed = snapshot.KickPressed,
                HeavyPressed = snapshot.HeavyPressed,
                Skill1Pressed = snapshot.Skill1Pressed,
                Skill2Pressed = snapshot.Skill2Pressed
            };
        }

        public static NetworkFighterInput ReadLocal(int playerSlot = 0)
        {
            FighterInputSnapshot snapshot = playerSlot == 0
                ? FighterInputReader.ReadPlayerOne()
                : FighterInputReader.ReadPlayerTwo();
            return FromSnapshot(snapshot);
        }
    }
}
