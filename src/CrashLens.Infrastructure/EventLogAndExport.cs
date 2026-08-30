using System.Diagnostics.Eventing.Reader;
using System.Text.Json;
using CrashLens.Core;

namespace CrashLens.Infrastructure;
public interface IEventLogReader { Task<IReadOnlyList<CrashEvent>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken = default); }
public sealed class WindowsEventLogReader(ICrashParser parser) : IEventLogReader
{
    public Task<IReadOnlyList<CrashEvent>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var query = new EventLogQuery("Application", PathType.LogName, "*[System[(EventID=1000 or EventID=1001 or EventID=1002)]]");
        using var reader = new EventLogReader(query); var crashes = new List<CrashEvent>(); EventRecord? record;
        while (!cancellationToken.IsCancellationRequested && (record = reader.ReadEvent()) is not null)
        using (record) { if (record.TimeCreated is not { } time || time < from) continue; var item = parser.Parse(record.Id, time, record.FormatDescription() ?? "", record.ToXml()); if (item is not null) crashes.Add(item); }
        return (IReadOnlyList<CrashEvent>)crashes.OrderByDescending(x => x.Time).ToList();
    }, cancellationToken);
}
public sealed class ReportExporter : IReportExporter
{
    public string ToJson(CrashEvent crash) => JsonSerializer.Serialize(crash, new JsonSerializerOptions { WriteIndented = true });
    public string ToMarkdown(CrashEvent c) => $"# CrashLens report\n\n| Field | Value |\n|---|---|\n| Application | {c.ApplicationName} |\n| Time | {c.Time:u} |\n| Event | {c.EventId} ({c.Type}) |\n| Exception | `{c.ExceptionCode ?? "—"}` |\n| Module | `{c.FaultingModule ?? "—"}` |\n\n## Raw Event\n\n```text\n{c.RawMessage}\n```";
    public string ToText(CrashEvent c) => $"CrashLens report\r\nApplication: {c.ApplicationName}\r\nTime: {c.Time:u}\r\nEvent: {c.EventId}\r\nException: {c.ExceptionCode}\r\nModule: {c.FaultingModule}\r\n\r\n{c.RawMessage}";
}
