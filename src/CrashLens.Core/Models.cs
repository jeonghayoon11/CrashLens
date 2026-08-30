namespace CrashLens.Core;

public enum CrashSeverity { Error, Warning, Information }
public enum CrashEventType { ApplicationCrash, ApplicationHang, WindowsErrorReport }

public sealed record CrashEvent(
    DateTimeOffset Time, CrashSeverity Severity, CrashEventType Type, int EventId,
    string ApplicationName, string? ExecutablePath, string? FaultingModule,
    string? ExceptionCode, int? ProcessId, string RawMessage, string? RawXml = null)
{
    public string ExceptionDisplay => ExceptionCode is null ? "—" : ExceptionCatalog.Describe(ExceptionCode).Display;
}

public sealed record ExceptionMeaning(string Code, string Name, string Description)
{
    public string Display => $"{Code} · {Name}";
}

public sealed record AnalysisFinding(string Title, string Detail, CrashSeverity Severity);

public static class ExceptionCatalog
{
    private static readonly Dictionary<string, ExceptionMeaning> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        ["0xc0000005"] = new("0xc0000005", "Access Violation", "The process attempted to read, write, or execute invalid memory."),
        ["0xc0000409"] = new("0xc0000409", "Stack Buffer Overrun", "Windows terminated the process after detecting stack corruption."),
        ["0xe0434352"] = new("0xe0434352", ".NET Exception", "An unhandled managed exception was recorded by the CLR."),
        ["0xc0000374"] = new("0xc0000374", "Heap Corruption", "The process heap was found to be corrupted.")
    };
    public static ExceptionMeaning Describe(string code) => Values.TryGetValue(code, out var value)
        ? value : new ExceptionMeaning(code, "Unclassified Exception", "No built-in interpretation is available for this exception code.");
}

public interface ICrashParser { CrashEvent? Parse(int eventId, DateTimeOffset time, string message, string? xml); }
public interface ICrashAnalyzer { IReadOnlyList<AnalysisFinding> Analyze(CrashEvent crash); }
public interface IPrivacyMasker { CrashEvent Mask(CrashEvent crash); }
public interface IReportExporter { string ToJson(CrashEvent crash); string ToMarkdown(CrashEvent crash); string ToText(CrashEvent crash); }
