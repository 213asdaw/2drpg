using System;
using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace JinwooNecrobinderSkin.Patches;

/// <summary>
/// Runtime rename: Necrobinder → Sung Jin-Woo, Osty → Igris.
/// </summary>
public static class LocalizationBootstrap
{
    private static readonly (string Key, string Ko, string En)[] Entries =
    {
        ("NECROBINDER.title", "성진우", "Sung Jin-Woo"),
        ("NECROBINDER.description", "그림자를 지배하는 군주. 충실한 그림자 병사 이그리트와 함께 첨탑을 오른다.",
            "The Shadow Monarch who ascends the Spire with his loyal shadow soldier, Igris."),
        ("characters:NECROBINDER.title", "성진우", "Sung Jin-Woo"),
        ("characters:NECROBINDER.description", "그림자를 지배하는 군주. 충실한 그림자 병사 이그리트와 함께 첨탑을 오른다.",
            "The Shadow Monarch who ascends the Spire with his loyal shadow soldier, Igris."),
        ("OSTY.title", "이그리트", "Igris"),
        ("OSTY.name", "이그리트", "Igris"),
        ("OSTY.description", "성진우의 충실한 그림자 병사. 주인을 대신해 피해를 막고 적을 벤다.",
            "Sung Jin-Woo's loyal shadow soldier. Absorbs blows meant for his master and cuts down foes."),
        ("creatures:OSTY.title", "이그리트", "Igris"),
        ("creatures:OSTY.name", "이그리트", "Igris"),
        ("creatures:OSTY.description", "성진우의 충실한 그림자 병사. 주인을 대신해 피해를 막고 적을 벤다.",
            "Sung Jin-Woo's loyal shadow soldier. Absorbs blows meant for his master and cuts down foes."),
        ("monsters:OSTY.title", "이그리트", "Igris"),
        ("keywords:OSTY.title", "이그리트", "Igris"),
        ("keywords:SUMMON.description",
            "이그리트를 X HP로 소환합니다. 이미 소환되어 있으면 이번 전투 동안 최대 HP를 X만큼 올립니다.",
            "Summon Igris with X HP. If already summoned, raise his Max HP by X for this combat."),
        ("keywords:DIE_FOR_YOU.description",
            "이그리트가 살아 있는 동안, 막지 못한 공격 피해를 먼저 이그리트가 받습니다.",
            "While Igris is alive, unblocked attack damage is dealt to Igris first."),
    };

    public static void Apply()
    {
        try
        {
            var locManagerType = AccessToolsType("MegaCrit.Sts2.Core.Localization.LocManager");
            if (locManagerType == null)
            {
                Log.Warn($"[{ModEntry.ModId}] LocManager type not found — relying on PCK localization.");
                return;
            }

            var instanceProp = locManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var instance = instanceProp?.GetValue(null);
            if (instance == null)
            {
                return;
            }

            var languageProp = locManagerType.GetProperty("Language")
                ?? locManagerType.GetProperty("CurrentLanguage");
            var language = (languageProp?.GetValue(instance) as string)?.ToLowerInvariant() ?? "eng";
            bool korean = language.StartsWith("ko", StringComparison.Ordinal);

            foreach (var method in locManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, "Set", StringComparison.Ordinal)
                    && !string.Equals(method.Name, "Add", StringComparison.Ordinal)
                    && !string.Equals(method.Name, "Override", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 2 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(string))
                {
                    continue;
                }

                foreach (var entry in Entries)
                {
                    method.Invoke(instance, new object[] { entry.Key, korean ? entry.Ko : entry.En });
                }

                Log.Info($"[{ModEntry.ModId}] Applied LocManager.{method.Name} overrides (Jin-Woo + Igris).");
                return;
            }

            foreach (var field in locManagerType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!typeof(IDictionary).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                if (field.GetValue(instance) is not IDictionary dict)
                {
                    continue;
                }

                int written = 0;
                foreach (var entry in Entries)
                {
                    if (dict.Contains(entry.Key) || dict.Keys.Count == 0 || FirstKey(dict) is string)
                    {
                        try
                        {
                            dict[entry.Key] = korean ? entry.Ko : entry.En;
                            written++;
                        }
                        catch
                        {
                            // ignore incompatible dictionaries
                        }
                    }
                }

                if (written > 0)
                {
                    Log.Info($"[{ModEntry.ModId}] Wrote {written} localization entries via field {field.Name}.");
                    return;
                }
            }

            Log.Info($"[{ModEntry.ModId}] No LocManager write path found — PCK localization still applies.");
        }
        catch (Exception ex)
        {
            Log.Warn($"[{ModEntry.ModId}] LocalizationBootstrap failed: {ex.Message}");
        }
    }

    private static Type? AccessToolsType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static object? FirstKey(IDictionary dict)
    {
        foreach (var key in dict.Keys)
        {
            return key;
        }

        return null;
    }
}
