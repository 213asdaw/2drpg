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
        public bool IsQuickMatch { get; set; }
    }

    public static class FightingRelayLobbyService
    {
        public const string RelayJoinCodeKey = "RelayJoinCode";
        public const string GameFilterKey = "S1";
        public const string GameFilterValue = "2DFight";
        public const string MatchModeKey = "S2";
        public const string MatchModeCustom = "Custom";
        public const string MatchModeQuickQueue = "QuickQueue";
        public const int MaxPlayers = 2;
        private const string ConnectionType = "dtls";
        private const int QuickMatchSearchAttempts = 10;
        private const int QuickMatchSearchDelayMs = 1200;

        private const string UgsEnvironmentOptionKey = "com.unity.services.core.environment-name";

        /// <summary>
        /// Dashboard에서 API를 켠 환경 이름과 같아야 합니다.
        /// Editor의 Environment 목록이 "기다려 달라"에서 멈추면 여기 값으로 대신 맞춥니다.
        /// (기본: production — Dashboard 상단 환경이 "알파"면 보통 alpha 또는 production)
        /// </summary>
        public const string UgsEnvironmentName = "production";

        private static RelayLobbySession currentSession;

        public static RelayLobbySession CurrentSession => currentSession;

        public static bool IsCloudProjectLinked()
        {
            return !string.IsNullOrEmpty(Application.cloudProjectId);
        }

        public static string BuildNotLinkedHint()
        {
            return "Unity Cloud 프로젝트가 연결되지 않았습니다.\n\n"
                + "1) Unity Hub에 로그인\n"
                + "2) Editor: Edit > Project Settings > Services\n"
                + "3) '새 클라우드 프로젝트 만들기' 또는 '기존 클라우드 프로젝트 사용' 선택\n"
                + "4) dashboard.unity.com > 해당 프로젝트 > 개발(Development)\n"
                + "   Authentication, Lobby, Relay 를 각각 활성화\n"
                + "5) Unity Editor 재시작 후 다시 시도\n\n"
                + "오프라인(로컬 2P)은 UGS 없이 바로 플레이 가능합니다.";
        }

        public static async Task InitializeAsync()
        {
            if (!IsCloudProjectLinked())
            {
                throw new InvalidOperationException("Cloud Project ID가 없습니다. Edit > Project Settings > Services에서 프로젝트를 연결하세요.");
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions()
                    .SetOption(UgsEnvironmentOptionKey, UgsEnvironmentName);
                await UnityServices.InitializeAsync(options);
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
            ConfigureRelayTransport(networkManager, AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = BuildLobbyData(relayJoinCode, MatchModeCustom)
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("2D Fight", MaxPlayers, options);
            currentSession = new RelayLobbySession
            {
                LobbyId = lobby.Id,
                LobbyCode = lobby.LobbyCode,
                IsHost = true,
                IsQuickMatch = false
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

        public static async Task<RelayLobbySession> QuickMatchAsync(
            NetworkManager networkManager,
            Action<string> statusCallback = null)
        {
            await InitializeAsync();

            for (int attempt = 0; attempt < QuickMatchSearchAttempts; attempt++)
            {
                statusCallback?.Invoke("빠른 매칭 상대 검색 중... (" + (attempt + 1) + "/" + QuickMatchSearchAttempts + ")");

                RelayLobbySession joinedSession = await TryJoinQuickMatchLobbyAsync(networkManager);
                if (joinedSession != null)
                {
                    statusCallback?.Invoke("상대를 찾았습니다!");
                    return joinedSession;
                }

                if (attempt < QuickMatchSearchAttempts - 1)
                {
                    await Task.Delay(QuickMatchSearchDelayMs);
                }
            }

            statusCallback?.Invoke("대기열 등록 중...");

            RelayLobbySession lastChanceJoin = await TryJoinQuickMatchLobbyAsync(networkManager);
            if (lastChanceJoin != null)
            {
                statusCallback?.Invoke("상대를 찾았습니다!");
                return lastChanceJoin;
            }

            await Task.Delay(UnityEngine.Random.Range(250, 900));

            lastChanceJoin = await TryJoinQuickMatchLobbyAsync(networkManager);
            if (lastChanceJoin != null)
            {
                statusCallback?.Invoke("상대를 찾았습니다!");
                return lastChanceJoin;
            }

            statusCallback?.Invoke("빠른 매칭 대기열 등록 완료");
            return await HostQuickMatchWaitingLobbyAsync(networkManager);
        }

        private static async Task<RelayLobbySession> TryJoinQuickMatchLobbyAsync(NetworkManager networkManager)
        {
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 5,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        QueryFilter.FieldOptions.AvailableSlots,
                        "1",
                        QueryFilter.OpOptions.EQ),
                    new QueryFilter(
                        QueryFilter.FieldOptions.S1,
                        GameFilterValue,
                        QueryFilter.OpOptions.EQ),
                    new QueryFilter(
                        QueryFilter.FieldOptions.S2,
                        MatchModeQuickQueue,
                        QueryFilter.OpOptions.EQ)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            foreach (Lobby lobbySummary in response.Results)
            {
                try
                {
                    Lobby lobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbySummary.Id);
                    await JoinLobbyInternalAsync(networkManager, lobby, true);
                    return currentSession;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Quick match join skipped: " + exception.Message);
                }
            }

            return null;
        }

        private static async Task<RelayLobbySession> HostQuickMatchWaitingLobbyAsync(NetworkManager networkManager)
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            ConfigureRelayTransport(networkManager, AllocationUtils.ToRelayServerData(allocation, ConnectionType));

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = BuildLobbyData(relayJoinCode, MatchModeQuickQueue)
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync("Quick Match", MaxPlayers, options);
            currentSession = new RelayLobbySession
            {
                LobbyId = lobby.Id,
                LobbyCode = lobby.LobbyCode,
                IsHost = true,
                IsQuickMatch = true
            };

            return currentSession;
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
            if (!IsCloudProjectLinked())
            {
                return BuildNotLinkedHint();
            }

            string message = exception != null ? exception.Message : string.Empty;
            string lower = message.ToLowerInvariant();

            if (lower.Contains("invalidprojectid") || lower.Contains("project id"))
            {
                return BuildNotLinkedHint() + "\n\n오류: " + message;
            }

            if (lower.Contains("authentication") || lower.Contains("sign in") || lower.Contains("401"))
            {
                return "Authentication(익명 로그인)이 꺼져 있거나 권한이 없습니다.\n"
                    + "Dashboard > 개발(Development) > Authentication 을 활성화하세요.\n\n"
                    + "오류: " + message;
            }

            if (lower.Contains("lobby"))
            {
                return "Lobby 서비스가 활성화되지 않았습니다.\n"
                    + "Dashboard > 개발(Development) > Lobby 를 활성화하세요.\n\n"
                    + "오류: " + message;
            }

            if (lower.Contains("relay"))
            {
                return "Relay 서비스가 활성화되지 않았습니다.\n"
                    + "Dashboard > 개발(Development) > Relay 를 활성화하세요.\n\n"
                    + "오류: " + message;
            }

            return "Unity Gaming Services 설정이 필요합니다.\n"
                + "Editor: Edit > Project Settings > Services (프로젝트 연결)\n"
                + "Dashboard: Authentication + Lobby + Relay 활성화\n\n"
                + "오류: " + message;
        }

        private static async Task JoinLobbyInternalAsync(
            NetworkManager networkManager,
            Lobby lobby,
            bool isQuickMatch = false)
        {
            if (lobby.Data == null || !lobby.Data.ContainsKey(RelayJoinCodeKey))
            {
                throw new InvalidOperationException("로비에 Relay 정보가 없습니다.");
            }

            string relayJoinCode = lobby.Data[RelayJoinCodeKey].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            ConfigureRelayTransport(networkManager, AllocationUtils.ToRelayServerData(joinAllocation, ConnectionType));

            currentSession = new RelayLobbySession
            {
                LobbyId = lobby.Id,
                LobbyCode = lobby.LobbyCode,
                IsHost = false,
                IsQuickMatch = isQuickMatch
            };
        }

        private static Dictionary<string, DataObject> BuildLobbyData(string relayJoinCode, string matchMode)
        {
            return new Dictionary<string, DataObject>
            {
                {
                    GameFilterKey,
                    new DataObject(DataObject.VisibilityOptions.Public, GameFilterValue)
                },
                {
                    MatchModeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, matchMode)
                },
                {
                    RelayJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                }
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
