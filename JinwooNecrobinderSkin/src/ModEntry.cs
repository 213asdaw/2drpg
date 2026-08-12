using System;
using System.Reflection;
using HarmonyLib;
using JinwooNecrobinderSkin.Patches;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace JinwooNecrobinderSkin;

/// <summary>
/// STS2 entry point — renames Necrobinder to Sung Jin-Woo and applies visual tweaks.
/// Asset overrides (char select / cards) live in the companion .pck and CustomCardTextures pack.
/// </summary>
[ModInitializer(nameof(OnModLoaded))]
public static class ModEntry
{
    public const string ModId = "JinwooNecrobinderSkin";
    public const string HarmonyId = "com.jinwoo.necrobinder.skin";

    public static void OnModLoaded()
    {
        try
        {
            var harmony = new Harmony(HarmonyId);
            int ok = 0;
            int fail = 0;
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<HarmonyPatch>() == null)
                {
                    continue;
                }

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Log.Warn($"[{ModId}] patch failed: {type.Name}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }

            LocalizationBootstrap.Apply();
            Log.Info($"[{ModId}] Sung Jin-Woo Necrobinder skin loaded ({ok} patches ok, {fail} failed).");
        }
        catch (Exception ex)
        {
            Log.Error($"[{ModId}] FATAL: {ex}");
        }
    }
}
