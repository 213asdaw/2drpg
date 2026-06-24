using UnityEditor;
using UnityEngine;

namespace FightingGame.Editor
{
    public static class FightingGameEditorMenu
    {
        [MenuItem("Fighting Game/Play Mode Info")]
        private static void ShowPlayModeInfo()
        {
            EditorUtility.DisplayDialog(
                "2D Fighting Game",
                "Play를 누르면 메인 메뉴가 표시됩니다.\n\n" +
                "온라인: Relay+Lobby (로비 코드)\n" +
                "오프라인 P1: A/D W S J K L\n" +
                "오프라인 P2: 방향키 + 1 2 3\n\n" +
                "UGS: Edit > Project Settings > Services",
                "OK");
        }

        [MenuItem("Fighting Game/Online Setup Check")]
        private static void ShowOnlineSetupCheck()
        {
            string cloudProjectId = PlayerSettings.cloudProjectId;
            bool linked = !string.IsNullOrEmpty(cloudProjectId);
            string message = linked
                ? "Unity Cloud 연결됨\n\nProject ID: " + cloudProjectId + "\n\n"
                  + "코드 환경 이름: " + FightingRelayLobbyService.UgsEnvironmentName + "\n"
                  + "(Dashboard 환경과 다르면 FightingRelayLobbyService.cs 의\n"
                  + " UgsEnvironmentName 을 Dashboard 환경 이름과 맞추세요)\n\n"
                  + "Dashboard에서 확인:\n"
                  + "- Authentication (Anonymous)\n"
                  + "- Lobby\n"
                  + "- Relay"
                : "Unity Cloud가 연결되지 않았습니다.\n\n"
                  + "Environment 목록이 '기다려 달라'에서 멈춰도\n"
                  + "Services 상단에서 ALPHA 프로젝트 Link 는 가능합니다.\n\n"
                  + "1) Unity Hub 로그인\n"
                  + "2) Edit > Project Settings > Services (General)\n"
                  + "3) 기존 클라우드 프로젝트 > ALPHA > Link\n"
                  + "4) Environment UI는 무시해도 됨 (코드에서 환경 지정)\n"
                  + "5) Dashboard: Anonymous + Lobby + Relay\n"
                  + "6) Editor 재시작\n\n"
                  + "Environment UI가 계속 멈추면:\n"
                  + "- Hub 로그아웃/로그인, VPN 끄기\n"
                  + "- Unity 완전 종료 후 재실행\n"
                  + "- 그래도 안 되면 Library 폴더 삭제 후 재열기";

            EditorUtility.DisplayDialog("온라인(UGS) 설정 확인", message, "OK");
        }

        [MenuItem("Fighting Game/Input Not Working?")]
        private static void ShowInputTroubleshooting()
        {
            EditorUtility.DisplayDialog(
                "키 입력이 안 될 때",
                "1) Game 탭을 클릭한 뒤 키를 누르세요.\n" +
                "2) 상단 ▶ Pause 가 켜져 있으면 해제하세요.\n" +
                "3) Console 창의 Error Pause 를 끄세요.\n" +
                "   (SearchDatabase 빨간 에러는 게임 버그가 아닙니다)\n" +
                "4) Edit > Project Settings > Player > Active Input Handling = Both\n" +
                "   (Unity 6 기본값이 New Input System 이면 구 Input.GetKey 가 안 됩니다)\n" +
                "5) 한/영 키로 영문 입력 모드로 바꾸세요.\n" +
                "6) Library\\Search 폴더 삭제 후 Unity 재실행.",
                "OK");
        }
    }
}
