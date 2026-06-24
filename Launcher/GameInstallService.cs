using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;

namespace FightLauncher;

internal sealed class GameInstallService
{
    private readonly LauncherConfig config;
    private readonly string installDirectory;

    public GameInstallService(LauncherConfig config)
    {
        this.config = config;
        installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            config.InstallFolderName);
    }

    public string InstallDirectory => installDirectory;

    public string GameExecutablePath => Path.Combine(installDirectory, config.GameExeName);

    public string LocalVersionPath => Path.Combine(installDirectory, "installed-version.txt");

    public bool IsGameInstalled => File.Exists(GameExecutablePath);

    public string ReadLocalVersion()
    {
        if (!File.Exists(LocalVersionPath))
        {
            return "미설치";
        }

        return File.ReadAllText(LocalVersionPath).Trim();
    }

    public async Task DownloadAndInstallAsync(
        ReleaseManifest manifest,
        IProgress<string> status,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(installDirectory);
        string tempZipPath = Path.Combine(Path.GetTempPath(), "2dfight-update-" + Guid.NewGuid().ToString("N") + ".zip");
        string tempExtractPath = Path.Combine(Path.GetTempPath(), "2dfight-update-" + Guid.NewGuid().ToString("N"));

        try
        {
            status.Report("다운로드 중...");
            progress.Report(0);
            await DownloadFileAsync(manifest.DownloadUrl, tempZipPath, progress, cancellationToken).ConfigureAwait(false);

            status.Report("압축 해제 중...");
            progress.Report(90);
            if (Directory.Exists(tempExtractPath))
            {
                Directory.Delete(tempExtractPath, true);
            }

            Directory.CreateDirectory(tempExtractPath);
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath, true);

            string sourceDirectory = FindGameRoot(tempExtractPath);
            CopyDirectory(sourceDirectory, installDirectory);

            File.WriteAllText(LocalVersionPath, manifest.Version);
            status.Report("설치 완료");
            progress.Report(100);
        }
        finally
        {
            TryDelete(tempZipPath);
            TryDeleteDirectory(tempExtractPath);
        }
    }

    public void LaunchGame()
    {
        if (!File.Exists(GameExecutablePath))
        {
            throw new FileNotFoundException("게임 실행 파일을 찾을 수 없습니다.", GameExecutablePath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = GameExecutablePath,
            WorkingDirectory = installDirectory,
            UseShellExecute = true
        });
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        using HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(30);
        using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        await using Stream sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using FileStream destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;
            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                int percent = (int)(totalRead * 85 / totalBytes.Value);
                progress.Report(Math.Clamp(percent, 0, 85));
            }
        }
    }

    private static string FindGameRoot(string extractedPath)
    {
        if (File.Exists(Path.Combine(extractedPath, "2D Fighting Game.exe")))
        {
            return extractedPath;
        }

        foreach (string directory in Directory.GetDirectories(extractedPath))
        {
            if (File.Exists(Path.Combine(directory, "2D Fighting Game.exe")))
            {
                return directory;
            }

            foreach (string nested in Directory.GetDirectories(directory))
            {
                if (File.Exists(Path.Combine(nested, "2D Fighting Game.exe")))
                {
                    return nested;
                }
            }
        }

        throw new DirectoryNotFoundException("zip 안에서 2D Fighting Game.exe 를 찾지 못했습니다.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, file);
            string targetPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }
}
