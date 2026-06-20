using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public sealed class NetworkFighter : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> netPosition = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
            FightConstants.MaxHealth,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netState = new NetworkVariable<int>(
            (int)FighterState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netFacing = new NetworkVariable<float>(
            1f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private FighterController fighter;
        private NetworkFighterInput latestInput;
        private int slotIndex = -1;

        public FighterController Fighter => fighter;
        public int SlotIndex => slotIndex;
        public bool HasSubmittedInput { get; private set; }

        public void Configure(int index, string displayName, Color primary, Color accent, Vector3 startPosition)
        {
            slotIndex = index;
            if (fighter == null)
            {
                fighter = gameObject.AddComponent<FighterController>();
            }

            fighter.Initialize(index, displayName, primary, accent, startPosition);
            latestInput = default;
            HasSubmittedInput = false;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            netPosition.OnValueChanged += HandleNetPositionChanged;
            netHealth.OnValueChanged += HandleNetHealthChanged;
            netState.OnValueChanged += HandleNetStateChanged;
            netFacing.OnValueChanged += HandleNetFacingChanged;
            ApplyAllNetworkValues();
        }

        public override void OnNetworkDespawn()
        {
            netPosition.OnValueChanged -= HandleNetPositionChanged;
            netHealth.OnValueChanged -= HandleNetHealthChanged;
            netState.OnValueChanged -= HandleNetStateChanged;
            netFacing.OnValueChanged -= HandleNetFacingChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsOwner)
            {
                SubmitInputServerRpc(NetworkFighterInput.ReadLocal());
            }

            if (!IsServer)
            {
                ApplyAllNetworkValues();
            }
        }

        public bool TryConsumeInput(out NetworkFighterInput input)
        {
            input = latestInput;
            if (!HasSubmittedInput)
            {
                return false;
            }

            HasSubmittedInput = false;
            return true;
        }

        public void ServerTick(float deltaTime, FighterInputSnapshot input, bool controlsEnabled)
        {
            if (fighter == null)
            {
                return;
            }

            fighter.Tick(deltaTime, input, controlsEnabled);
            PublishState();
        }

        public void ServerApplyRoundReset(Vector3 startPosition)
        {
            if (fighter == null)
            {
                return;
            }

            fighter.ResetForRound(startPosition);
            PublishState();
        }

        public void ServerApplyMatchResult(bool won)
        {
            fighter?.SetMatchResult(won);
            PublishState();
        }

        [ServerRpc]
        private void SubmitInputServerRpc(NetworkFighterInput input)
        {
            latestInput = input;
            HasSubmittedInput = true;
        }

        private void PublishState()
        {
            netPosition.Value = fighter.transform.position;
            netHealth.Value = fighter.Health;
            netState.Value = (int)fighter.State;
            netFacing.Value = fighter.Facing;
        }

        private void ApplyAllNetworkValues()
        {
            if (fighter == null || IsServer)
            {
                return;
            }

            fighter.ApplyNetworkDisplayState(
                netPosition.Value,
                netHealth.Value,
                (FighterState)netState.Value,
                netFacing.Value);
        }

        private void HandleNetPositionChanged(Vector3 previous, Vector3 current) => ApplyAllNetworkValues();
        private void HandleNetHealthChanged(float previous, float current) => ApplyAllNetworkValues();
        private void HandleNetStateChanged(int previous, int current) => ApplyAllNetworkValues();
        private void HandleNetFacingChanged(float previous, float current) => ApplyAllNetworkValues();
    }
}
