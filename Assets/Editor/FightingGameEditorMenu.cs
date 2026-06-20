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

        [MenuItem("Fighting Game/Input Not Working?")]
        private static void ShowInputTroubleshooting()
        {
            EditorUtility.DisplayDialog(
                "키 입력이 안 될 때",
                "1) Game 탭을 클릭한 뒤 키를 누르세요.\n" +
                "2) 상단 ▶ Pause 가 켜져 있으면 해제하세요.\n" +
                "3) Console 창의 Error Pause 를 끄세요.\n" +
                "   (SearchDatabase 빨간 에러는 게임 버그가 아닙니다)\n" +
                "4) 한/영 키로 영문 입력 모드로 바꾸세요.\n" +
                "5) Library\\Search 폴더 삭제 후 Unity 재실행.",
                "OK");
        }
    }
}
