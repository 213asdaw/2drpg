using UnityEditor;
using UnityEngine;

namespace IdleRPG.EditorTools
{
    public static class IdleRpgEditorMenu
    {
        [MenuItem("Idle RPG/Reset Save Data")]
        public static void ResetSaveData()
        {
            IdleRpgAccountService.ResetAllAccountData();
            Debug.Log("Idle RPG account and save data reset.");
        }
    }
}
