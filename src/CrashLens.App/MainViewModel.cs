using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using CrashLens.Core;
using CrashLens.Infrastructure;

namespace CrashLens.App;
public sealed record FieldRow(string Key, string Value);
public sealed record FindingRow(string Title, string Detail);
public sealed record CrashRow(CrashEvent Crash)
{
    public string Application => Crash.ApplicationName;
    public string Detail => $"{Crash.Type}  ·  {Crash.ExceptionCode ?? "No exception code"}";
    public string When => Crash.Time.LocalDateTime.ToString("MMM dd HH:mm");
    public SolidColorBrush SeverityBrush => new(Crash.Severity == CrashSeverity.Error ? ColorHelper.FromArgb(255, 239, 92, 92) : ColorHelper.FromArgb(255, 220, 169, 77));
}
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ICrashAnalyzer _analyzer = new CrashAnalyzer();
    private readonly IReportExporter _exporter = new ReportExporter();
    public ObservableCollection<CrashRow> Crashes { get; } = new();
    public ObservableCollection<FieldRow> Overview { get; } = new(); public ObservableCollection<FieldRow> Application { get; } = new(); public ObservableCollection<FieldRow> Exception { get; } = new(); public ObservableCollection<FieldRow> Module { get; } = new(); public ObservableCollection<FindingRow> Findings { get; } = new();
    [ObservableProperty] private CrashEvent selectedCrash = SampleData.Events[0];
    [ObservableProperty] private string status = "Ready · mock data loaded";
    [ObservableProperty] private string reportPreview = "";
    public MainViewModel() { foreach (var crash in SampleData.Events) Crashes.Add(new CrashRow(crash)); Populate(); }
    partial void OnSelectedCrashChanged(CrashEvent value) => Populate();
    private void Populate()
    {
        void Set(ObservableCollection<FieldRow> destination, params FieldRow[] items) { destination.Clear(); foreach (var i in items) destination.Add(i); }
        Set(Overview, new("Time", SelectedCrash.Time.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss")), new("Event ID", SelectedCrash.EventId.ToString()), new("Type", SelectedCrash.Type.ToString()), new("Process ID", SelectedCrash.ProcessId?.ToString() ?? "Not recorded"));
        Set(Application, new("Application", SelectedCrash.ApplicationName), new("Path", SelectedCrash.ExecutablePath ?? "Not recorded"));
        Set(Exception, new("Code", SelectedCrash.ExceptionCode ?? "Not recorded"), new("Meaning", SelectedCrash.ExceptionCode is null ? "—" : ExceptionCatalog.Describe(SelectedCrash.ExceptionCode).Name));
        Set(Module, new("Module", SelectedCrash.FaultingModule ?? "Not recorded"), new("Location", "See raw event for module path"));
        Findings.Clear(); foreach (var item in _analyzer.Analyze(SelectedCrash)) Findings.Add(new(item.Title, item.Detail));
        ReportPreview = _exporter.ToMarkdown(SelectedCrash);
    }
    [RelayCommand] private async Task Refresh()
    {
        Status = "Reading Windows Application event log…";
        try { var reader = new WindowsEventLogReader(new CrashParser()); var events = await reader.ReadAsync(DateTimeOffset.Now.AddDays(-1)); Crashes.Clear(); foreach (var item in events) Crashes.Add(new CrashRow(item)); if (events.Count > 0) SelectedCrash = events[0]; Status = $"Ready · {events.Count} crash-related events in the last 24 hours"; }
        catch (Exception ex) { Status = $"Event log unavailable: {ex.Message}"; }
    }
    [RelayCommand] private void Export() => Status = "Report preview updated. Use the File menu export actions in the next MVP iteration.";
}
public static class SampleData
{
    public static readonly CrashEvent[] Events = [
        new(DateTimeOffset.Now.AddMinutes(-18), CrashSeverity.Error, CrashEventType.ApplicationCrash, 1000, "RenderHost.exe", @"C:\Users\<user>\AppData\Local\RenderHost\RenderHost.exe", "nvwgf2umx.dll", "0xc0000005", 14820, "Faulting application name: RenderHost.exe\r\nFaulting module name: nvwgf2umx.dll\r\nException code: 0xc0000005", "<Event><System><EventID>1000</EventID></System></Event>"),
        new(DateTimeOffset.Now.AddHours(-3), CrashSeverity.Warning, CrashEventType.ApplicationHang, 1002, "DataIndexer.exe", @"C:\Program Files\DataIndexer\DataIndexer.exe", null, null, 9236, "Application DataIndexer.exe stopped interacting with Windows and was closed.", "<Event><System><EventID>1002</EventID></System></Event>"),
        new(DateTimeOffset.Now.AddDays(-1).AddMinutes(15), CrashSeverity.Error, CrashEventType.WindowsErrorReport, 1001, "Inventory.Client.exe", @"C:\Users\<user>\source\Inventory\Inventory.Client.exe", "KERNELBASE.dll", "0xe0434352", 19104, "Windows Error Reporting recorded an unhandled CLR exception.", "<Event><System><EventID>1001</EventID></System></Event>") ];
}
