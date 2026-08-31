using System.Diagnostics.Eventing.Reader;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Globalization;
using Microsoft.Win32;
using CrashLens.Core;
using CrashLens.Infrastructure;
using CrashLens.Desktop;

NativeShell.SetCurrentProcessExplicitAppUserModelID("CrashLens");
ApplicationConfiguration.Initialize();
var capture = Environment.GetCommandLineArgs().Contains("--capture", StringComparer.OrdinalIgnoreCase);
var background = Environment.GetCommandLineArgs().Contains("--background", StringComparer.OrdinalIgnoreCase);
var main = new CrashLensForm(capture, background);
if (capture) Application.Run(main);
else { var context = new SplashContext(main, background); context.Start(); Application.Run(context); }

sealed class SplashContext : ApplicationContext
{
    readonly Form main;
    readonly Form splash = new() { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(440, 260), BackColor = Color.FromArgb(17, 24, 39), ShowInTaskbar = false };
    readonly bool startHidden;
    public SplashContext(Form main, bool startHidden) { this.main = main; this.startHidden = startHidden; }
    protected override void OnMainFormClosed(object? sender, EventArgs e) => ExitThread();
    public void Start()
    {
        if (startHidden) { MainForm = main; main.FormClosed += OnMainFormClosed; main.Show(); main.Hide(); return; }
        splash.Controls.Add(new Label { Text = "CRASHLENS", ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(32, 36) });
        splash.Controls.Add(new Label { Text = "Windows crash analysis", ForeColor = Color.FromArgb(160, 190, 215), Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(34, 104) });
        splash.Controls.Add(new Label { Text = $"Version {AppMetadata.DisplayVersion}", ForeColor = Color.FromArgb(130, 160, 190), Font = new Font("Segoe UI", 9), AutoSize = true, Location = new Point(34, 132) });
        var progress = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Location = new Point(34, 175), Width = 372, Height = 4 };
        splash.Controls.Add(progress); splash.Controls.Add(new Label { Text = "Reading Application Event Log", ForeColor = Color.FromArgb(190, 200, 210), Font = new Font("Segoe UI", 9), AutoSize = true, Location = new Point(34, 198) });
        splash.Shown += (_, _) => { var timer = new System.Windows.Forms.Timer { Interval = 900 }; timer.Tick += (_, _) => { timer.Stop(); splash.Hide(); MainForm = main; main.FormClosed += OnMainFormClosed; main.Show(); }; timer.Start(); };
        splash.Show();
    }
}

sealed class CrashLensForm : Form
{
    readonly bool capture;
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, BackgroundColor = Color.FromArgb(30, 31, 34), ForeColor = Color.White };
    readonly TextBox details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10), BackColor = Color.FromArgb(30, 31, 34), ForeColor = Color.Gainsboro };
    readonly BindingSource source = new();
    readonly ICrashParser parser = new CrashParser();
    readonly Icon windowIcon = LoadIcon(32);
    readonly Icon trayIcon = LoadIcon(16);
    readonly NotifyIcon tray;
    readonly UpdateService updateService = new();
    readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    ReleaseUpdate? availableUpdate;
    BalloonAction balloonAction;
    bool checkingForUpdate;
    EventLogWatcher? watcher;
    enum BalloonAction { OpenWindow, InstallUpdate }

    readonly bool startInBackground;
    public CrashLensForm(bool capture, bool startInBackground)
    {
        this.capture = capture; this.startInBackground = startInBackground;
        tray = new NotifyIcon { Icon = trayIcon, Text = "CrashLens is monitoring crashes", Visible = true };
        Text = "CrashLens - Windows Crash Analysis"; Icon = windowIcon; Width = 1280; Height = 780; BackColor = Color.FromArgb(30, 31, 34); ForeColor = Color.White;
        var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(43, 45, 48), ForeColor = Color.White };
        var refresh = new ToolStripButton("Refresh") { ForeColor = Color.White }; refresh.Click += async (_, _) => await LoadEvents(); tool.Items.Add(refresh); tool.Items.Add(new ToolStripLabel("Application Event Log - Last 24 hours") { ForeColor = Color.Silver });
        foreach (var (title, field, width) in new[] { ("Severity", nameof(CrashEvent.Severity), 80), ("Application", nameof(CrashEvent.ApplicationName), 220), ("Event", nameof(CrashEvent.Type), 150), ("Exception", nameof(CrashEvent.ExceptionCode), 120) }) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = field, Width = width });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = nameof(CrashEvent.Time), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.DataSource = source; grid.SelectionChanged += (_, _) => ShowSelected();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 390, BackColor = Color.FromArgb(63, 65, 69) }; split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(details); Controls.Add(split); Controls.Add(tool); tool.Dock = DockStyle.Top;
        var menu = new ContextMenuStrip(); menu.Items.Add("Open CrashLens", null, (_, _) => OpenWindow()); menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(true)); menu.Items.Add("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); }); tray.ContextMenuStrip = menu; tray.BalloonTipClicked += async (_, _) => await HandleBalloonClickAsync(); tray.DoubleClick += (_, _) => OpenWindow();
        var help = new ToolStripDropDownButton("Help"); help.DropDownItems.Add("About CrashLens", null, (_, _) => ShowAbout()); tool.Items.Add(help);
        updateTimer.Tick += async (_, _) => await CheckForUpdatesAsync(false);
        Shown += async (_, _) => { if (capture) { BeginInvoke(CaptureScreenshot); return; } await LoadEvents(); StartMonitoring(); if (startInBackground) ShowBackgroundStartedNotification(); await CheckForUpdatesAsync(false, true); updateTimer.Start(); };
        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); tray.ShowBalloonTip(2500, "CrashLens", "CrashLens is still monitoring application crashes.", ToolTipIcon.Info); } };
    }

    void CaptureScreenshot()
    {
        using var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(Point.Empty, Size));
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "docs", "images"));
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "docs", "images", "crashlens-main.png"));
        tray.Visible = false;
        Application.Exit();
    }

    void StartMonitoring()
    {
        watcher = new EventLogWatcher(new EventLogQuery("Application", PathType.LogName, "*[System[(EventID=1000 or EventID=1001 or EventID=1002)]]"));
        watcher.EventRecordWritten += (_, e) =>
        {
            if (e.EventRecord is not { } record) return;
            using (record)
            {
                var crash = parser.Parse(record.Id, record.TimeCreated ?? DateTime.Now, record.FormatDescription() ?? "", record.ToXml());
                if (crash is null) return;
                BeginInvoke(() => { balloonAction = BalloonAction.OpenWindow; tray.ShowBalloonTip(8000, "Application crash detected", $"{crash.ApplicationName} stopped unexpectedly. Click to inspect the recorded event.", ToolTipIcon.Error); _ = LoadEvents(); });
            }
        };
        watcher.Enabled = true;
    }

    void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void ShowBackgroundStartedNotification()
    {
        var selectedLanguage = Registry.CurrentUser.OpenSubKey("Software\\CrashLens")?.GetValue("NotificationLanguage")?.ToString();
        var korean = selectedLanguage?.Equals("korean", StringComparison.OrdinalIgnoreCase) == true || (string.IsNullOrEmpty(selectedLanguage) && CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ko", StringComparison.OrdinalIgnoreCase));
        balloonAction = BalloonAction.OpenWindow;
        tray.ShowBalloonTip(7000, korean ? "CrashLens가 백그라운드에서 실행 중입니다." : "CrashLens is running in the background.", korean ? "알림 영역에서 프로그램 충돌을 모니터링합니다." : "CrashLens will monitor application crashes from the notification area.", ToolTipIcon.Info);
    }
    static Icon LoadIcon(int size)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "CrashLens.ico");
        return File.Exists(iconPath) ? new Icon(iconPath, new Size(size, size)) : Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    }
    async Task CheckForUpdatesAsync(bool showNoUpdateMessage, bool showUpdatePopup = false)
    {
        if (checkingForUpdate) return;
        checkingForUpdate = true;
        try
        {
            var current = AppMetadata.Version;
            availableUpdate = await updateService.CheckAsync(current);
            if (availableUpdate is not null)
            {
                balloonAction = BalloonAction.InstallUpdate;
                tray.ShowBalloonTip(10000, "CrashLens update available", $"Version {availableUpdate.Version} is ready. Click this notification to install it.", ToolTipIcon.Info);
                if (showUpdatePopup && MessageBox.Show($"CrashLens {availableUpdate.Version} is available. Update now?", "CrashLens update", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes) await BeginUpdateAsync();
            }
            else if (showNoUpdateMessage) MessageBox.Show("CrashLens is up to date.", "CrashLens", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { if (showNoUpdateMessage) MessageBox.Show("Could not check for updates. Check your internet connection and try again.", "CrashLens", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { checkingForUpdate = false; }
    }
    async Task HandleBalloonClickAsync()
    {
        if (balloonAction != BalloonAction.InstallUpdate || availableUpdate is null) { OpenWindow(); return; }
        if (MessageBox.Show($"Download and install CrashLens {availableUpdate.Version} now?", "CrashLens update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await BeginUpdateAsync();
    }
    async Task BeginUpdateAsync()
    {
        if (availableUpdate is null) return;
        using var progressWindow = new UpdateProgressForm(availableUpdate.Version);
        progressWindow.Show(this);
        try
        {
            var progress = new Progress<int>(progressWindow.SetProgress);
            var installer = await updateService.DownloadInstallerAsync(availableUpdate, progress);
            progressWindow.SetInstalling();
            await Task.Delay(300);
            Process.Start(new ProcessStartInfo { FileName = installer, Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS", UseShellExecute = true });
            tray.Visible = false;
            Application.Exit();
        }
        catch { MessageBox.Show("The update could not be downloaded. Please try again later.", "CrashLens update", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    static void ShowAbout() => MessageBox.Show($"CrashLens\r\nVersion {AppMetadata.DisplayVersion}\r\n\r\nWindows crash analysis utility\r\n\r\nDeveloper: Jeong Hayoon\r\nWebsite: https://jhynx.com\r\nGitHub: https://github.com/jeonghayoon11", "About CrashLens", MessageBoxButtons.OK, MessageBoxIcon.Information);
    void ShowSelected() { if (grid.CurrentRow?.DataBoundItem is CrashEvent c) details.Text = $"APPLICATION\r\n{c.ApplicationName}\r\n{c.ExecutablePath}\r\n\r\nEXCEPTION\r\n{c.ExceptionDisplay}\r\n\r\nFAULTING MODULE\r\n{c.FaultingModule}\r\n\r\nRAW EVENT\r\n{c.RawMessage}\r\n\r\nXML\r\n{c.RawXml}"; }
    async Task LoadEvents()
    {
        try
        {
            var events = await new WindowsEventLogReader(parser).ReadAsync(DateTimeOffset.Now.AddDays(-1));
            source.DataSource = events;
            if (events.Count == 0) details.Text = "NO RECENT CRASHES\r\n\r\nNo Application Error, Windows Error Reporting, or Application Hang events were recorded in the last 24 hours.";
        }
        catch (Exception ex) { details.Text = ex.Message; }
    }
    protected override void Dispose(bool disposing) { if (disposing) { updateTimer.Dispose(); watcher?.Dispose(); tray.Dispose(); trayIcon.Dispose(); windowIcon.Dispose(); } base.Dispose(disposing); }
}

static class AppMetadata
{
    public static Version Version => typeof(CrashLensForm).Assembly.GetName().Version ?? new Version(0, 1, 0);
    public static string DisplayVersion => Version.ToString(3);
}

static class NativeShell
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int SetCurrentProcessExplicitAppUserModelID(string appID);
}

sealed class UpdateProgressForm : Form
{
    readonly Label status = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft };
    readonly ProgressBar progress = new() { Dock = DockStyle.Top, Height = 22, Minimum = 0, Maximum = 100 };
    readonly Label percentage = new() { Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

    public UpdateProgressForm(Version version)
    {
        Text = "Updating CrashLens";
        ClientSize = new Size(390, 125);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(20);
        status.Text = $"Downloading CrashLens {version}...";
        percentage.Text = "0%";
        Controls.Add(percentage);
        Controls.Add(progress);
        Controls.Add(status);
    }

    public void SetProgress(int value)
    {
        value = Math.Clamp(value, 0, 100);
        progress.Value = value;
        percentage.Text = $"{value}%";
    }

    public void SetInstalling()
    {
        progress.Value = 100;
        status.Text = "Applying update...";
        percentage.Text = "100%";
    }
}
