using UnityEditor;
using UnityEngine;

namespace IdleRPG.EditorTools
{
    public static class IdleRpgEditorMenu
    {
        private const string SaveKey = "idle-rpg-save-v1";

        [MenuItem("Idle RPG/Reset Save Data")]
        public static void ResetSaveData()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Debug.Log("Idle RPG save data reset.");
        }
    }
}
