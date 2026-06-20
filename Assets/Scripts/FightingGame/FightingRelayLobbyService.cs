using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace FightingGame
{
    public sealed class RelayLobbySession
    {
        public string LobbyId { get; set; }
        public string LobbyCode { get; set; }
        public bool IsHost { get; set; }
    }

    public static class FightingRelayLobbyService
    {
        public const string RelayJoinCodeKey = "RelayJoinCode";
        public const string GameFilterKey = "S1";
        public const string GameFilterValue = "2DFight";
        public const int MaxPlayers = 2;
        private const string ConnectionType = "dtls";

        private static RelayLobbySession currentSession;

        public static RelayLobbySession CurrentSession => currentSession;

        public static async Task InitializeAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public static async Task<RelayLobbySession> HostLobbyAsync(NetworkManager networkManager)
        {
            await InitializeAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            ConfigureRelayTransport(networkManager, new RelayServerData(allocation, ConnectionType));

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        GameFilterKey,
                        new DataObject(DataObject.VisibilityOptions.Public, GameFilterValue)
                    },
                    {
                        RelayJoinCodeKey,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("2D Fight", MaxPlayers, options);
            currentSession = new RelayLobbySession
            {
                LobbyId = lobby.Id,
                LobbyCode = lobby.LobbyCode,
                IsHost = true
            };

            return currentSession;
        }

        public static async Task<RelayLobbySession> JoinLobbyByCodeAsync(NetworkManager networkManager, string lobbyCode)
        {
            await InitializeAsync();
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(NormalizeLobbyCode(lobbyCode));
            await JoinLobbyInternalAsync(networkManager, lobby);
            return currentSession;
        }

        public static async Task<RelayLobbySession> QuickMatchAsync(NetworkManager networkManager)
        {
            await InitializeAsync();

            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 1,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.AvailableSlots,
                        "1",
                        QueryFilter.OpOptions.GE),
                    new QueryFilter(
                        QueryFilter.FieldOptions.S1,
                        GameFilterValue,
                        QueryFilter.OpOptions.EQ)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            if (response.Results.Count > 0)
            {
                Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(response.Results[0].Id);
                await JoinLobbyInternalAsync(networkManager, lobby);
                return currentSession;
            }

            return await HostLobbyAsync(networkManager);
        }

        public static async Task SendHeartbeatAsync()
        {
            if (currentSession == null || !currentSession.IsHost || string.IsNullOrEmpty(currentSession.LobbyId))
            {
                return;
            }

            await LobbyService.Instance.SendHeartbeatPingAsync(currentSession.LobbyId);
        }

        public static async Task LeaveLobbyAsync()
        {
            if (currentSession == null)
            {
                return;
            }

            try
            {
                if (currentSession.IsHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentSession.LobbyId);
                }
                else if (AuthenticationService.Instance.IsSignedIn)
                {
                    await LobbyService.Instance.RemovePlayerAsync(currentSession.LobbyId, AuthenticationService.Instance.PlayerId);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to leave lobby: " + exception.Message);
            }
            finally
            {
                currentSession = null;
            }
        }

        public static string BuildSetupHint(Exception exception)
        {
            return "Unity Gaming Services 설정이 필요합니다.\n"
                + "Unity Editor > Edit > Project Settings > Services 에서 프로젝트를 연결하고\n"
                + "Dashboard에서 Lobby/Relay API를 활성화하세요.\n\n"
                + "오류: " + exception.Message;
        }

        private static async Task JoinLobbyInternalAsync(NetworkManager networkManager, Lobby lobby)
        {
            if (lobby.Data == null || !lobby.Data.ContainsKey(RelayJoinCodeKey))
            {
                throw new InvalidOperationException("로비에 Relay 정보가 없습니다.");
            }

            string relayJoinCode = lobby.Data[RelayJoinCodeKey].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            ConfigureRelayTransport(networkManager, new RelayServerData(joinAllocation, ConnectionType));

            currentSession = new RelayLobbySession
            {
                LobbyId = lobby.Id,
                LobbyCode = lobby.LobbyCode,
                IsHost = false
            };
        }

        private static void ConfigureRelayTransport(NetworkManager networkManager, RelayServerData relayServerData)
        {
            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                throw new InvalidOperationException("UnityTransport component is missing.");
            }

            transport.SetRelayServerData(relayServerData);
        }

        private static string NormalizeLobbyCode(string lobbyCode)
        {
            return lobbyCode.Trim().ToUpperInvariant();
        }
    }
}
