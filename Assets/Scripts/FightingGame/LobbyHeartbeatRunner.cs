using UnityEngine;

namespace FightingGame
{
    public sealed class LobbyHeartbeatRunner : MonoBehaviour
    {
        private const float HeartbeatIntervalSeconds = 15f;
        private float heartbeatTimer;

        private void Update()
        {
            RelayLobbySession session = FightingRelayLobbyService.CurrentSession;
            if (session == null || !session.IsHost)
            {
                return;
            }

            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer > 0f)
            {
                return;
            }

            heartbeatTimer = HeartbeatIntervalSeconds;
            _ = FightingRelayLobbyService.SendHeartbeatAsync();
        }

        private void OnDestroy()
        {
            _ = FightingRelayLobbyService.LeaveLobbyAsync();
        }
    }
}
