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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref BlockHeld);
            serializer.SerializeValue(ref JumpPressed);
            serializer.SerializeValue(ref LightPressed);
            serializer.SerializeValue(ref KickPressed);
            serializer.SerializeValue(ref HeavyPressed);
        }

        public FighterInputSnapshot ToSnapshot()
        {
            return new FighterInputSnapshot(
                Horizontal,
                JumpPressed,
                BlockHeld,
                LightPressed,
                KickPressed,
                HeavyPressed);
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
                HeavyPressed = snapshot.HeavyPressed
            };
        }

        public static NetworkFighterInput ReadLocal()
        {
            return FromSnapshot(FighterInputReader.ReadPlayerOne());
        }
    }
}
