using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace JinwooNecrobinderSkin.Patches;

/// <summary>
/// Replaces leftover "Osty" / "골골이" strings in UI text with Igris / 이그리트.
/// </summary>
[HarmonyPatch]
public static class OstyToIgrisTextPatch
{
    private static readonly Regex OstyWord = new(@"\bOsty\b", RegexOptions.Compiled);
    private static readonly Regex OstyPossessive = new(@"\bOsty's\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static MethodBase? TargetMethod()
    {
        // Broad LocManager.Get / Translate style APIs.
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Localization.LocManager");
        if (type == null)
        {
            return null;
        }

        return AccessTools.Method(type, "Get", new[] { typeof(string) })
            ?? AccessTools.Method(type, "Translate", new[] { typeof(string) })
            ?? AccessTools.Method(type, "GetString", new[] { typeof(string) });
    }

    static void Postfix(ref string __result)
    {
        if (string.IsNullOrEmpty(__result))
        {
            return;
        }

        __result = Rewrite(__result);
    }

    public static string Rewrite(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string result = text;
        if (result.Contains("골골이", StringComparison.Ordinal))
        {
            result = result.Replace("골골이", "이그리트", StringComparison.Ordinal);
        }

        if (result.Contains("Osty", StringComparison.OrdinalIgnoreCase))
        {
            result = OstyPossessive.Replace(result, "Igris'");
            result = OstyWord.Replace(result, "Igris");
            result = Regex.Replace(result, @"\bosty\b", "igris", RegexOptions.IgnoreCase);
        }

        return result;
    }
}

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
                Log.Info($"[{ModEntry.ModId}] Necrobinder visuals created — Sung Jin-Woo + Igris skin active.");
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

    static void Postfix(ref string __result)
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

            __result = OstyToIgrisTextPatch.Rewrite(__result);
        }
        catch
        {
            // ignore
        }
    }
}
