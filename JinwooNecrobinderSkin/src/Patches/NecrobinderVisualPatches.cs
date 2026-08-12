using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace JinwooNecrobinderSkin.Patches;

/// <summary>
/// Light-touch visual patches for Necrobinder instances.
/// Full combat Spine replacement should be provided via PCK path
/// res://animations/characters/necrobinder/ or CustomSkeletonLoader ReplaceResources.
/// </summary>
[HarmonyPatch]
public static class NecrobinderCreateVisualsPatch
{
    private static bool _logged;

    static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.CharacterModel")
            ?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Characters.Necrobinder");
        if (type == null)
        {
            return null;
        }

        return AccessTools.Method(type, "CreateVisuals")
            ?? AccessTools.DeclaredMethod(type, "CreateVisuals");
    }

    static void Postfix(object __instance)
    {
        try
        {
            if (!IsNecrobinder(__instance))
            {
                return;
            }

            if (!_logged)
            {
                _logged = true;
                Log.Info($"[{ModEntry.ModId}] Necrobinder visuals created — Sung Jin-Woo skin active.");
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[{ModEntry.ModId}] CreateVisuals postfix error: {ex.Message}");
        }
    }

    private static bool IsNecrobinder(object instance)
    {
        var typeName = instance.GetType().Name;
        if (typeName.Contains("Necrobinder", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var idProp = instance.GetType().GetProperty("Id")
            ?? instance.GetType().GetProperty("CharacterId")
            ?? instance.GetType().GetProperty("ModelId");
        var id = idProp?.GetValue(instance)?.ToString();
        return !string.IsNullOrEmpty(id) && id.Contains("NECRO", StringComparison.OrdinalIgnoreCase);
    }
}

[HarmonyPatch]
public static class CharacterSelectTitlePatch
{
    static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect.NCharacterSelectButton");
        if (type == null)
        {
            return null;
        }

        return AccessTools.PropertyGetter(type, "Title")
            ?? AccessTools.Method(type, "GetTitle")
            ?? AccessTools.Method(type, "UpdateTitle");
    }

    static void Postfix(object __instance, ref string __result)
    {
        try
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            if (__result.Contains("Necrobinder", StringComparison.OrdinalIgnoreCase)
                || __result.Contains("네크로바인더", StringComparison.Ordinal))
            {
                __result = "성진우";
            }
        }
        catch
        {
            // ignore
        }
    }
}
