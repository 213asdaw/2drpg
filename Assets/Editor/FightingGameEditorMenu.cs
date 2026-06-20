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
                "빈 씬에서 Play를 누르면 FightingGame이 자동으로 생성됩니다.\n\n" +
                "P1: A/D W S J K L\n" +
                "P2: 방향키 + 1 2 3",
                "OK");
        }
    }
}
