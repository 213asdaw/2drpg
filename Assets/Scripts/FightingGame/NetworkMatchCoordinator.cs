using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FightingGame
{
    public sealed class NetworkMatchCoordinator : NetworkBehaviour
    {
        private readonly NetworkVariable<int> netPhase = new NetworkVariable<int>(
            (int)MatchPhase.Intro,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netRoundTimer = new NetworkVariable<float>(
            FightConstants.RoundDuration,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netCurrentRound = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netPlayerOneRoundWins = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> netPlayerTwoRoundWins = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<FixedString128Bytes> netStatusMessage = new NetworkVariable<FixedString128Bytes>(
            "READY",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<NetworkFighter> fighters = new List<NetworkFighter>();
        private readonly Dictionary<ulong, FighterArchetypeId> clientArchetypeChoices = new Dictionary<ulong, FighterArchetypeId>();
        private float phaseTimer;
        private bool matchInitialized;
        private NetworkFighter lastRoundWinner;

        public MatchPhase Phase => (MatchPhase)netPhase.Value;
        public float RoundTimer => netRoundTimer.Value;
        public int CurrentRound => netCurrentRound.Value;
        public int PlayerOneRoundWins => netPlayerOneRoundWins.Value;
        public int PlayerTwoRoundWins => netPlayerTwoRoundWins.Value;
        public string StatusMessage => netStatusMessage.Value.ToString();
        public bool ControlsEnabled =>
            Phase == MatchPhase.Intro || Phase == MatchPhase.Fighting;
        public IReadOnlyList<NetworkFighter> Fighters => fighters;

        public static NetworkMatchCoordinator Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (!matchInitialized)
            {
                TryInitializeMatch();
                return;
            }

            ServerTickMatch(Time.deltaTime);
        }

        public void ServerRestartMatch()
        {
            if (!IsServer)
            {
                return;
            }

            netPlayerOneRoundWins.Value = 0;
            netPlayerTwoRoundWins.Value = 0;
            netCurrentRound.Value = 1;
            lastRoundWinner = null;
            ResetFightersForRound();
            BeginIntro("ROUND 1 — FIGHT!");
        }

        private void TryInitializeMatch()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || networkManager.ConnectedClientsIds.Count < 2)
            {
                return;
            }

            SpawnFighters(networkManager);
            if (fighters.Count < 2)
            {
                return;
            }

            matchInitialized = true;
            netPlayerOneRoundWins.Value = 0;
            netPlayerTwoRoundWins.Value = 0;
            netCurrentRound.Value = 1;
            ResetFightersForRound();
            BeginIntro("ROUND 1 — FIGHT!");
        }

        private void SpawnFighters(NetworkManager networkManager)
        {
            if (fighters.Count >= 2)
            {
                return;
            }

            List<ulong> clientIds = networkManager.ConnectedClientsIds.OrderBy(id => id).ToList();
            GameObject fighterTemplate = FightingNetworkBootstrap.GetFighterPrefabTemplate();
            Vector3[] spawnPositions =
            {
                new Vector3(-3.5f, FightConstants.GroundY, 0f),
                new Vector3(3.5f, FightConstants.GroundY, 0f)
            };

            FighterArchetypeId[] archetypes =
            {
                ResolveArchetypeForClient(0, clientIds[0]),
                clientIds.Count > 1 ? ResolveArchetypeForClient(1, clientIds[1]) : FighterArchetypeId.Default
            };

            for (int i = 0; i < clientIds.Count && i < 2; i++)
            {
                GameObject fighterObject = Instantiate(fighterTemplate);
                fighterObject.SetActive(true);
                NetworkObject networkObject = fighterObject.GetComponent<NetworkObject>();
                NetworkFighter networkFighter = fighterObject.GetComponent<NetworkFighter>();
                networkFighter.Configure(i, archetypes[i], spawnPositions[i]);
                networkObject.SpawnWithOwnership(clientIds[i]);
                fighters.Add(networkFighter);
            }

            if (fighters.Count == 2)
            {
                fighters[0].Fighter.SetOpponent(fighters[1].Fighter);
                fighters[1].Fighter.SetOpponent(fighters[0].Fighter);
            }
        }

        public void SubmitLocalArchetype(FighterArchetypeId archetype)
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                RegisterClientArchetype(NetworkManager.Singleton.LocalClientId, archetype);
                return;
            }

            SubmitLocalArchetypeServerRpc(archetype);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitLocalArchetypeServerRpc(FighterArchetypeId archetype, ServerRpcParams rpcParams = default)
        {
            RegisterClientArchetype(rpcParams.Receive.SenderClientId, archetype);
        }

        private void RegisterClientArchetype(ulong clientId, FighterArchetypeId archetype)
        {
            clientArchetypeChoices[clientId] = archetype;

            for (int i = 0; i < fighters.Count; i++)
            {
                NetworkObject networkObject = fighters[i].NetworkObject;
                if (networkObject != null && networkObject.OwnerClientId == clientId)
                {
                    fighters[i].ServerReconfigureArchetype(archetype);
                    break;
                }
            }
        }

        private FighterArchetypeId ResolveArchetypeForClient(int slotIndex, ulong clientId)
        {
            if (clientArchetypeChoices.TryGetValue(clientId, out FighterArchetypeId chosen))
            {
                return chosen;
            }

            return slotIndex == 0
                ? FightSessionConfig.GetArchetypeForSlot(0)
                : FighterArchetypeId.Default;
        }

        private void ServerTickMatch(float deltaTime)
        {
            NetworkFighter playerOne = fighters[0];
            NetworkFighter playerTwo = fighters[1];
            bool controlsEnabled = ControlsEnabled;

            ServerTickFighter(playerOne, deltaTime, controlsEnabled);
            ServerTickFighter(playerTwo, deltaTime, controlsEnabled);

            switch (Phase)
            {
                case MatchPhase.Intro:
                    phaseTimer -= deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        StartFighting();
                    }

                    break;

                case MatchPhase.Fighting:
                    netRoundTimer.Value -= deltaTime;
                    if (!playerOne.Fighter.IsAlive || !playerTwo.Fighter.IsAlive)
                    {
                        NetworkFighter winner = playerOne.Fighter.IsAlive ? playerOne : playerTwo;
                        FinishRound(winner, winner.Fighter.DisplayName + " KO!");
                    }
                    else if (netRoundTimer.Value <= 0f)
                    {
                        if (Mathf.Approximately(playerOne.Fighter.Health, playerTwo.Fighter.Health))
                        {
                            FinishRound(null, "TIME — DRAW");
                        }
                        else
                        {
                            NetworkFighter winner = playerOne.Fighter.Health > playerTwo.Fighter.Health ? playerOne : playerTwo;
                            FinishRound(winner, "TIME — " + winner.Fighter.DisplayName + " WINS");
                        }
                    }

                    break;

                case MatchPhase.RoundEnd:
                    phaseTimer -= deltaTime;
                    if (phaseTimer <= 0f)
                    {
                        if (netPlayerOneRoundWins.Value >= FightConstants.RoundsToWin || netPlayerTwoRoundWins.Value >= FightConstants.RoundsToWin)
                        {
                            BeginMatchEnd(lastRoundWinner);
                        }
                        else
                        {
                            netCurrentRound.Value++;
                            ResetFightersForRound();
                            BeginIntro("ROUND " + netCurrentRound.Value + " — FIGHT!");
                        }
                    }

                    break;
            }
        }

        private static void ServerTickFighter(NetworkFighter networkFighter, float deltaTime, bool controlsEnabled)
        {
            FighterInputSnapshot input = networkFighter.TryConsumeInput(out NetworkFighterInput networkInput)
                ? networkInput.ToSnapshot()
                : FighterInputSnapshot.Empty;
            networkFighter.ServerTick(deltaTime, input, controlsEnabled);
        }

        private void ResetFightersForRound()
        {
            fighters[0].ServerApplyRoundReset(new Vector3(-3.5f, FightConstants.GroundY, 0f));
            fighters[1].ServerApplyRoundReset(new Vector3(3.5f, FightConstants.GroundY, 0f));
        }

        private void BeginIntro(string message)
        {
            netPhase.Value = (int)MatchPhase.Intro;
            phaseTimer = 0.15f;
            netRoundTimer.Value = FightConstants.RoundDuration;
            netStatusMessage.Value = message;
        }

        private void StartFighting()
        {
            netPhase.Value = (int)MatchPhase.Fighting;
            netStatusMessage.Value = "FIGHT!";
        }

        private void FinishRound(NetworkFighter winner, string message)
        {
            netPhase.Value = (int)MatchPhase.RoundEnd;
            phaseTimer = FightConstants.RoundEndDelay;
            lastRoundWinner = winner;
            netStatusMessage.Value = message;

            if (winner == fighters[0])
            {
                netPlayerOneRoundWins.Value++;
                fighters[0].ServerApplyMatchResult(true);
                fighters[1].ServerApplyMatchResult(false);
            }
            else if (winner == fighters[1])
            {
                netPlayerTwoRoundWins.Value++;
                fighters[1].ServerApplyMatchResult(true);
                fighters[0].ServerApplyMatchResult(false);
            }
        }

        private void BeginMatchEnd(NetworkFighter winner)
        {
            netPhase.Value = (int)MatchPhase.MatchEnd;
            phaseTimer = FightConstants.MatchEndDelay;
            netStatusMessage.Value = winner != null
                ? winner.Fighter.DisplayName + " MATCH WIN!"
                : "MATCH DRAW";

            if (winner == fighters[0])
            {
                fighters[0].ServerApplyMatchResult(true);
                fighters[1].ServerApplyMatchResult(false);
            }
            else if (winner == fighters[1])
            {
                fighters[1].ServerApplyMatchResult(true);
                fighters[0].ServerApplyMatchResult(false);
            }
        }
    }
}
