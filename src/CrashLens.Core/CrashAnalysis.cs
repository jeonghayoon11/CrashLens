using System.Text.RegularExpressions;

namespace CrashLens.Core;

public sealed class CrashParser : ICrashParser
{
    public CrashEvent? Parse(int eventId, DateTimeOffset time, string message, string? xml)
    {
        var type = eventId switch { 1000 => CrashEventType.ApplicationCrash, 1001 => CrashEventType.WindowsErrorReport, 1002 => CrashEventType.ApplicationHang, _ => (CrashEventType?)null };
        if (type is null) return null;
        string Get(string label) => Regex.Match(message, $@"{Regex.Escape(label)}\s*[:：]\s*([^\r\n]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
        var app = Get("Faulting application name");
        if (string.IsNullOrEmpty(app)) app = Get("Application Name");
        var processText = Get("Faulting process id").Replace("0x", "", StringComparison.OrdinalIgnoreCase);
        return new CrashEvent(time, eventId == 1002 ? CrashSeverity.Warning : CrashSeverity.Error, type.Value, eventId,
            string.IsNullOrEmpty(app) ? "Unknown application" : app, Get("Faulting application path"),
            Get("Faulting module name"), Get("Exception code"), int.TryParse(processText, System.Globalization.NumberStyles.HexNumber, null, out var pid) ? pid : null, message, xml);
    }
}

public sealed class CrashAnalyzer : ICrashAnalyzer
{
    public IReadOnlyList<AnalysisFinding> Analyze(CrashEvent crash)
    {
        var result = new List<AnalysisFinding>();
        if (crash.ExceptionCode is not null)
        {
            var meaning = ExceptionCatalog.Describe(crash.ExceptionCode);
            result.Add(new("Exception analysis", $"{meaning.Name}: {meaning.Description}", crash.Severity));
        }
        if (!string.IsNullOrWhiteSpace(crash.FaultingModule))
            result.Add(new("Faulting module", $"The recorded module is {crash.FaultingModule}. This may identify the application component, plugin, or dependency involved.", CrashSeverity.Information));
        if (crash.Type == CrashEventType.ApplicationHang)
            result.Add(new("Hang recorded", "Windows recorded an application hang. Review related events and wait-chain information if available.", CrashSeverity.Warning));
        return result;
    }
}

public sealed class PrivacyMasker : IPrivacyMasker
{
    public CrashEvent Mask(CrashEvent crash) => crash with { ExecutablePath = MaskText(crash.ExecutablePath), RawMessage = MaskText(crash.RawMessage) ?? "", RawXml = MaskText(crash.RawXml) };
    private static string? MaskText(string? value) => value is null ? null : Regex.Replace(value, @"C:\\Users\\[^\\\r\n]+", "C:\\Users\\<user>", RegexOptions.IgnoreCase);
}
