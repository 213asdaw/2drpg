using UnityEngine;

namespace FightingGame
{
    [DefaultExecutionOrder(-2000)]
    internal sealed class FightKeyboardPoll : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePollRunner()
        {
            if (FindObjectOfType<FightKeyboardPoll>() != null)
            {
                return;
            }

            GameObject pollObject = new GameObject("FightKeyboardPoll");
            pollObject.hideFlags = HideFlags.HideAndDontSave;
            pollObject.AddComponent<FightKeyboardPoll>();
            DontDestroyOnLoad(pollObject);
        }

        private void Update()
        {
            FightKeyCapture.PollHardwareKeys();
        }
    }
}
