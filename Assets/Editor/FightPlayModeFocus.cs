#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FightingGame.Editor
{
    [InitializeOnLoad]
    public static class FightPlayModeFocus
    {
        static FightPlayModeFocus()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        private static void HandlePlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.delayCall += FocusGameView;
        }

        private static void FocusGameView()
        {
            System.Type gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                return;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            gameView?.Focus();
        }
    }
}
#endif
