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
    }
}
