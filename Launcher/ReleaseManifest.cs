using System.Text.Json;

namespace FightLauncher;

internal sealed class ReleaseManifest
{
    public string Version { get; set; } = "0.0.0";
    public string DownloadUrl { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public static async Task<ReleaseManifest> FetchAsync(string versionUrl, CancellationToken cancellationToken)
    {
        using HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        string json = await client.GetStringAsync(versionUrl, cancellationToken).ConfigureAwait(false);
        ReleaseManifest? manifest = JsonSerializer.Deserialize<ReleaseManifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version) || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            throw new InvalidDataException("version.json 형식이 올바르지 않습니다.");
        }

        return manifest;
    }
}
