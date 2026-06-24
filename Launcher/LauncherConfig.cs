using System.Text.Json;

namespace FightLauncher;

internal sealed class LauncherConfig
{
    public string VersionUrl { get; set; } = string.Empty;
    public string GameExeName { get; set; } = "2D Fighting Game.exe";
    public string InstallFolderName { get; set; } = "2DFightGame";
    public string WindowTitle { get; set; } = "2D Fighting Game Launcher";

    public static LauncherConfig Load(string launcherDirectory)
    {
        string configPath = Path.Combine(launcherDirectory, "launcher-config.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("launcher-config.json 을 찾을 수 없습니다.", configPath);
        }

        string json = File.ReadAllText(configPath);
        LauncherConfig? config = JsonSerializer.Deserialize<LauncherConfig>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (config == null || string.IsNullOrWhiteSpace(config.VersionUrl))
        {
            throw new InvalidDataException("launcher-config.json 설정이 올바르지 않습니다.");
        }

        return config;
    }
}
