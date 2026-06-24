namespace FightLauncher;

internal sealed class LauncherForm : Form
{
    private readonly Label titleLabel = new();
    private readonly Label localVersionLabel = new();
    private readonly Label latestVersionLabel = new();
    private readonly Label notesLabel = new();
    private readonly Label statusLabel = new();
    private readonly ProgressBar progressBar = new();
    private readonly Button playButton = new();
    private readonly Button updateButton = new();
    private readonly Button refreshButton = new();

    private readonly LauncherConfig config;
    private readonly GameInstallService installService;
    private ReleaseManifest? latestManifest;
    private CancellationTokenSource? activeOperation;

    public LauncherForm()
    {
        string launcherDirectory = AppContext.BaseDirectory;
        config = LauncherConfig.Load(launcherDirectory);
        installService = new GameInstallService(config);

        Text = config.WindowTitle;
        ClientSize = new Size(520, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        titleLabel.Text = "2D Fighting Game";
        titleLabel.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
        titleLabel.AutoSize = false;
        titleLabel.TextAlign = ContentAlignment.MiddleCenter;
        titleLabel.SetBounds(24, 20, 472, 36);

        localVersionLabel.SetBounds(24, 72, 472, 24);
        latestVersionLabel.SetBounds(24, 100, 472, 24);
        notesLabel.SetBounds(24, 132, 472, 72);
        notesLabel.ForeColor = Color.DimGray;

        progressBar.SetBounds(24, 214, 472, 24);
        progressBar.Style = ProgressBarStyle.Continuous;

        statusLabel.SetBounds(24, 246, 472, 24);
        statusLabel.ForeColor = Color.DarkSlateGray;

        playButton.Text = "게임 시작";
        playButton.SetBounds(24, 286, 150, 42);
        playButton.Click += async (_, _) => await PlayAsync().ConfigureAwait(true);

        updateButton.Text = "업데이트";
        updateButton.SetBounds(184, 286, 150, 42);
        updateButton.Click += async (_, _) => await UpdateAsync().ConfigureAwait(true);

        refreshButton.Text = "새로고침";
        refreshButton.SetBounds(344, 286, 152, 42);
        refreshButton.Click += async (_, _) => await RefreshAsync().ConfigureAwait(true);

        Controls.AddRange(new Control[]
        {
            titleLabel,
            localVersionLabel,
            latestVersionLabel,
            notesLabel,
            progressBar,
            statusLabel,
            playButton,
            updateButton,
            refreshButton
        });

        Shown += async (_, _) => await RefreshAsync().ConfigureAwait(true);
        FormClosing += (_, _) => activeOperation?.Cancel();
    }

    private async Task RefreshAsync()
    {
        SetBusy(true, "최신 버전 확인 중...");
        try
        {
            activeOperation?.Cancel();
            activeOperation = new CancellationTokenSource();
            latestManifest = await ReleaseManifest.FetchAsync(config.VersionUrl, activeOperation.Token).ConfigureAwait(true);
            UpdateUiState();
            statusLabel.Text = "버전 확인 완료";
        }
        catch (Exception exception)
        {
            latestManifest = null;
            latestVersionLabel.Text = "최신 버전: 확인 실패";
            notesLabel.Text = exception.Message;
            statusLabel.Text = "인터넷 연결 또는 version.json URL 을 확인하세요.";
            playButton.Enabled = installService.IsGameInstalled;
            updateButton.Enabled = false;
        }
        finally
        {
            progressBar.Value = 0;
            SetBusy(false, statusLabel.Text);
        }
    }

    private async Task UpdateAsync()
    {
        if (latestManifest == null)
        {
            await RefreshAsync().ConfigureAwait(true);
            if (latestManifest == null)
            {
                return;
            }
        }

        SetBusy(true, "업데이트 준비 중...");
        try
        {
            activeOperation?.Cancel();
            activeOperation = new CancellationTokenSource();
            Progress<string> status = new(message => statusLabel.Text = message);
            Progress<int> progress = new(value => progressBar.Value = Math.Clamp(value, 0, 100));
            await installService.DownloadAndInstallAsync(
                latestManifest,
                status,
                progress,
                activeOperation.Token).ConfigureAwait(true);
            UpdateUiState();
            MessageBox.Show(this, "업데이트가 완료되었습니다.", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "업데이트 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = "업데이트 실패";
        }
        finally
        {
            progressBar.Value = 0;
            SetBusy(false, statusLabel.Text);
        }
    }

    private Task PlayAsync()
    {
        try
        {
            installService.LaunchGame();
            statusLabel.Text = "게임 실행 중";
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "실행 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return Task.CompletedTask;
    }

    private void UpdateUiState()
    {
        string localVersion = installService.ReadLocalVersion();
        localVersionLabel.Text = "설치된 버전: " + localVersion;

        if (latestManifest == null)
        {
            latestVersionLabel.Text = "최신 버전: -";
            notesLabel.Text = string.Empty;
            playButton.Enabled = installService.IsGameInstalled;
            updateButton.Enabled = false;
            return;
        }

        latestVersionLabel.Text = "최신 버전: " + latestManifest.Version;
        notesLabel.Text = string.IsNullOrWhiteSpace(latestManifest.Notes)
            ? "업데이트 메모 없음"
            : latestManifest.Notes;

        bool needsUpdate = !string.Equals(localVersion, latestManifest.Version, StringComparison.OrdinalIgnoreCase)
            || !installService.IsGameInstalled;
        playButton.Enabled = installService.IsGameInstalled;
        updateButton.Enabled = needsUpdate;
        updateButton.Text = installService.IsGameInstalled ? "업데이트" : "게임 설치";
    }

    private void SetBusy(bool busy, string status)
    {
        playButton.Enabled = !busy && installService.IsGameInstalled;
        refreshButton.Enabled = !busy;
        updateButton.Enabled = !busy && latestManifest != null;
        statusLabel.Text = status;
        UseWaitCursor = busy;
    }
}
