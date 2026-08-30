using System.Diagnostics.Eventing.Reader;
using CrashLens.Core;
using CrashLens.Infrastructure;

ApplicationConfiguration.Initialize();
Application.Run(new CrashLensForm(Environment.GetCommandLineArgs().Contains("--capture", StringComparer.OrdinalIgnoreCase)));

sealed class CrashLensForm : Form
{
    readonly bool capture;
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = false, BackgroundColor = Color.FromArgb(30, 31, 34), ForeColor = Color.White };
    readonly TextBox details = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10), BackColor = Color.FromArgb(30, 31, 34), ForeColor = Color.Gainsboro };
    readonly BindingSource source = new();
    readonly ICrashParser parser = new CrashParser();
    readonly NotifyIcon tray = new() { Icon = SystemIcons.Error, Text = "CrashLens is monitoring crashes", Visible = true };
    EventLogWatcher? watcher;

    public CrashLensForm(bool capture)
    {
        this.capture = capture;
        Text = "CrashLens - Windows Crash Analysis"; Width = 1280; Height = 780; BackColor = Color.FromArgb(30, 31, 34); ForeColor = Color.White;
        var tool = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, BackColor = Color.FromArgb(43, 45, 48), ForeColor = Color.White };
        var refresh = new ToolStripButton("Refresh") { ForeColor = Color.White }; refresh.Click += async (_, _) => await LoadEvents(); tool.Items.Add(refresh); tool.Items.Add(new ToolStripLabel("Application Event Log - Last 24 hours") { ForeColor = Color.Silver });
        foreach (var (title, field, width) in new[] { ("Severity", nameof(CrashEvent.Severity), 80), ("Application", nameof(CrashEvent.ApplicationName), 220), ("Event", nameof(CrashEvent.Type), 150), ("Exception", nameof(CrashEvent.ExceptionCode), 120) }) grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = field, Width = width });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = nameof(CrashEvent.Time), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        grid.DataSource = source; grid.SelectionChanged += (_, _) => ShowSelected();
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 390, BackColor = Color.FromArgb(63, 65, 69) }; split.Panel1.Controls.Add(grid); split.Panel2.Controls.Add(details); Controls.Add(split); Controls.Add(tool); tool.Dock = DockStyle.Top;
        var menu = new ContextMenuStrip(); menu.Items.Add("Open CrashLens", null, (_, _) => OpenWindow()); menu.Items.Add("Exit", null, (_, _) => { tray.Visible = false; Application.Exit(); }); tray.ContextMenuStrip = menu; tray.BalloonTipClicked += (_, _) => OpenWindow(); tray.DoubleClick += (_, _) => OpenWindow();
        Shown += async (_, _) => { if (capture) { BeginInvoke(CaptureScreenshot); return; } await LoadEvents(); StartMonitoring(); };
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
                BeginInvoke(() => { tray.ShowBalloonTip(8000, "Application crash detected", $"{crash.ApplicationName} stopped unexpectedly. Click to inspect the recorded event.", ToolTipIcon.Error); _ = LoadEvents(); });
            }
        };
        watcher.Enabled = true;
    }

    void OpenWindow() { Show(); WindowState = FormWindowState.Normal; Activate(); }
    void ShowSelected() { if (grid.CurrentRow?.DataBoundItem is CrashEvent c) details.Text = $"APPLICATION\r\n{c.ApplicationName}\r\n{c.ExecutablePath}\r\n\r\nEXCEPTION\r\n{c.ExceptionDisplay}\r\n\r\nFAULTING MODULE\r\n{c.FaultingModule}\r\n\r\nRAW EVENT\r\n{c.RawMessage}\r\n\r\nXML\r\n{c.RawXml}"; }
    async Task LoadEvents() { try { source.DataSource = await new WindowsEventLogReader(parser).ReadAsync(DateTimeOffset.Now.AddDays(-1)); } catch (Exception ex) { details.Text = ex.Message; } }
    protected override void Dispose(bool disposing) { if (disposing) { watcher?.Dispose(); tray.Dispose(); } base.Dispose(disposing); }
}
