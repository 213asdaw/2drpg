#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGame
{
    public static class FightInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureInputSystem()
        {
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.onBeforeUpdate += FightKeyCapture.PollHardwareKeys;
        }
    }
}
#else
using UnityEngine;

namespace FightingGame
{
    public static class FightInputBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureLegacyPoll()
        {
            Application.runInBackground = true;
            // Legacy-only projects still poll arrows from Update via FightKeyboardPoll.
        }
    }
}
#endif
