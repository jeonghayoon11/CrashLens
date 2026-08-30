using System.Diagnostics.Eventing.Reader;
using System.Diagnostics;
using CrashLens.Core;
using CrashLens.Infrastructure;
using CrashLens.Desktop;

ApplicationConfiguration.Initialize();
var capture = Environment.GetCommandLineArgs().Contains("--capture", StringComparer.OrdinalIgnoreCase);
var main = new CrashLensForm(capture);
if (capture) Application.Run(main);
else { var context = new SplashContext(main); context.Start(); Application.Run(context); }

sealed class SplashContext : ApplicationContext
{
    readonly Form main;
    readonly Form splash = new() { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.CenterScreen, ClientSize = new Size(440, 260), BackColor = Color.FromArgb(17, 24, 39), ShowInTaskbar = false };
    public SplashContext(Form main) => this.main = main;
    protected override void OnMainFormClosed(object? sender, EventArgs e) => ExitThread();
    public void Start()
    {
        splash.Controls.Add(new Label { Text = "CRASHLENS", ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold), AutoSize = true, Location = new Point(32, 45) });
        splash.Controls.Add(new Label { Text = "Windows crash analysis", ForeColor = Color.FromArgb(160, 190, 215), Font = new Font("Segoe UI", 10), AutoSize = true, Location = new Point(34, 84) });
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
    readonly NotifyIcon tray = new() { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Error, Text = "CrashLens is monitoring crashes", Visible = true };
    readonly UpdateService updateService = new();
    ReleaseUpdate? availableUpdate;
    BalloonAction balloonAction;
    EventLogWatcher? watcher;
    enum BalloonAction { OpenWindow, InstallUpdate }

    public CrashLensForm(bool capture)
    {
        this.capture = capture;
        Text = "CrashLens - Windows Crash Analysis"; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Width = 1280; Height = 780; BackColor = Color.FromArgb(30, 31, 34); ForeColor = Color.White;
        var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(43, 45, 48), ForeColor = Color.White };
        var refresh = new ToolStripButton("Refresh") { ForeColor = Color.White }; refresh.Click += async (_, _) => await LoadEvents(); tool.Items.Add(refresh); tool.Items.Add(new ToolStripLabel("Application Event Log - Last 24 hours") { ForeColor = Color.Silver });
        foreach (var (title, field, width) in new[] { ("Severity", nameof(CrashEvent.Severity), 80), ("Application", nameof(CrashEvent.ApplicationName), 220), ("Event", nameof(CrashEvent.Type), 150), ("Exception", nameof(CrashEvent.ExceptionCode), 120) }) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = field, Width = width });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = nameof(CrashEvent.Time), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.DataSource = source; grid.SelectionChanged += (_, _) => ShowSelected();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 390, BackColor = Color.FromArgb(63, 65, 69) }; split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(details); Controls.Add(split); Controls.Add(tool); tool.Dock = DockStyle.Top;
        var menu = new ContextMenuStrip(); menu.Items.Add("Open CrashLens", null, (_, _) => OpenWindow()); menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(true)); menu.Items.Add("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); }); tray.ContextMenuStrip = menu; tray.BalloonTipClicked += async (_, _) => await HandleBalloonClickAsync(); tray.DoubleClick += (_, _) => OpenWindow();
        Shown += async (_, _) => { if (capture) { BeginInvoke(CaptureScreenshot); return; } await LoadEvents(); StartMonitoring(); _ = CheckForUpdatesAsync(false); };
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
    async Task CheckForUpdatesAsync(bool showNoUpdateMessage)
    {
        try
        {
            var current = typeof(CrashLensForm).Assembly.GetName().Version ?? new Version(0, 1, 0);
            availableUpdate = await updateService.CheckAsync(current);
            if (availableUpdate is not null)
            {
                balloonAction = BalloonAction.InstallUpdate;
                tray.ShowBalloonTip(10000, "CrashLens update available", $"Version {availableUpdate.Version} is ready. Click this notification to install it.", ToolTipIcon.Info);
            }
            else if (showNoUpdateMessage) MessageBox.Show("CrashLens is up to date.", "CrashLens", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch when (showNoUpdateMessage) { MessageBox.Show("Could not check for updates. Check your internet connection and try again.", "CrashLens", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    async Task HandleBalloonClickAsync()
    {
        if (balloonAction != BalloonAction.InstallUpdate || availableUpdate is null) { OpenWindow(); return; }
        if (MessageBox.Show($"Download and install CrashLens {availableUpdate.Version} now?", "CrashLens update", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            var installer = await updateService.DownloadInstallerAsync(availableUpdate);
            Process.Start(new ProcessStartInfo { FileName = installer, UseShellExecute = true });
            tray.Visible = false;
            Application.Exit();
        }
        catch { MessageBox.Show("The update could not be downloaded. Please try again later.", "CrashLens update", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    void ShowSelected() { if (grid.CurrentRow?.DataBoundItem is CrashEvent c) details.Text = $"APPLICATION\r\n{c.ApplicationName}\r\n{c.ExecutablePath}\r\n\r\nEXCEPTION\r\n{c.ExceptionDisplay}\r\n\r\nFAULTING MODULE\r\n{c.FaultingModule}\r\n\r\nRAW EVENT\r\n{c.RawMessage}\r\n\r\nXML\r\n{c.RawXml}"; }
    async Task LoadEvents() { try { source.DataSource = await new WindowsEventLogReader(parser).ReadAsync(DateTimeOffset.Now.AddDays(-1)); } catch (Exception ex) { details.Text = ex.Message; } }
    protected override void Dispose(bool disposing) { if (disposing) { watcher?.Dispose(); tray.Dispose(); } base.Dispose(disposing); }
}
