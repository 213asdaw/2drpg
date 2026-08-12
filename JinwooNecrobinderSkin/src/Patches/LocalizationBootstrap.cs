using System;
using System.Collections;
using System.Reflection;
using MegaCrit.Sts2.Core.Logging;

namespace JinwooNecrobinderSkin.Patches;

/// <summary>
/// Best-effort runtime rename for Necrobinder → Sung Jin-Woo.
/// PCK localization tables are preferred; this covers hosts that ignore mod loc merges.
/// </summary>
public static class LocalizationBootstrap
{
    private static readonly (string Key, string Ko, string En)[] Entries =
    {
        ("NECROBINDER.title", "성진우", "Sung Jin-Woo"),
        ("NECROBINDER.description", "그림자를 지배하는 군주. 죽은 자를 부하로 부리며 첨탑을 오른다.",
            "The Shadow Monarch who commands the dead and ascends the Spire."),
        ("characters:NECROBINDER.title", "성진우", "Sung Jin-Woo"),
        ("characters:NECROBINDER.description", "그림자를 지배하는 군주. 죽은 자를 부하로 부리며 첨탑을 오른다.",
            "The Shadow Monarch who commands the dead and ascends the Spire."),
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

            // Try common table injection hooks without hard-binding to private APIs.
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

                Log.Info($"[{ModEntry.ModId}] Applied LocManager.{method.Name} overrides.");
                return;
            }

            // Fallback: poke string dictionaries on the manager if present.
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
                    if (!dict.Contains(entry.Key) && dict.Keys.Count > 0)
                    {
                        // Only write when dictionary already looks like a string table.
                        var sampleKey = FirstKey(dict);
                        if (sampleKey is not string)
                        {
                            break;
                        }
                    }

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
