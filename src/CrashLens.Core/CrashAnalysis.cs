using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CrashLens.Core;

public sealed class CrashParser : ICrashParser
{
    public CrashEvent? Parse(int eventId, DateTimeOffset time, string message, string? xml)
    {
        var type = eventId switch { 1000 => CrashEventType.ApplicationCrash, 1001 => CrashEventType.WindowsErrorReport, 1002 => CrashEventType.ApplicationHang, _ => (CrashEventType?)null };
        if (type is null) return null;

        var fields = ReadXmlFields(xml);
        string Get(params string[] names) => names.Select(name =>
            fields.TryGetValue(name, out var value) ? value : ReadMessageField(message, name))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

        var app = Get("Faulting application name", "Application Name", "AppName", "Application");
        var appPath = Get("Faulting application path", "Application Path", "AppPath", "ApplicationPath");
        var module = Get("Faulting module name", "Module Name", "ModuleName", "Faulting module path");
        var exception = NormalizeExceptionCode(Get("Exception code", "ExceptionCode"));
        if (string.IsNullOrEmpty(exception)) exception = NormalizeExceptionCode(Regex.Match(message, @"0x[0-9a-fA-F]{8}").Value);
        var processText = Get("Faulting process id", "ProcessId", "Process ID");

        // Event 1000 has the most reliable values in positional EventData fields.
        if (eventId == 1000)
        {
            app = First(app, GetPositionalXmlField(xml, 0));
            module = First(module, GetPositionalXmlField(xml, 3));
            exception = First(exception, NormalizeExceptionCode(GetPositionalXmlField(xml, 6)));
            appPath = First(appPath, GetPositionalXmlField(xml, 10));
            processText = First(processText, GetPositionalXmlField(xml, 8));
        }
        else if (eventId == 1001)
        {
            // Windows Error Reporting stores the fault details as P1/P4/P8 in its problem signature.
            app = First(app, Get("P1"));
            module = First(module, Get("P4"));
            exception = First(exception, NormalizeExceptionCode(Get("P8")));
        }

        return new CrashEvent(time, eventId == 1002 ? CrashSeverity.Warning : CrashSeverity.Error, type.Value, eventId,
            string.IsNullOrEmpty(app) ? "Unknown application" : app, NullIfEmpty(appPath),
            NullIfEmpty(module), NullIfEmpty(exception), ParseProcessId(processText), message, xml);
    }

    static Dictionary<string, string> ReadXmlFields(string? xml)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(xml)) return fields;
        try
        {
            var document = XDocument.Parse(xml);
            foreach (var data in document.Descendants().Where(item => item.Name.LocalName == "Data"))
            {
                var name = data.Attribute("Name")?.Value;
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(data.Value)) fields[name] = data.Value.Trim();
            }
        }
        catch (System.Xml.XmlException) { }
        return fields;
    }

    static string GetPositionalXmlField(string? xml, int index)
    {
        if (string.IsNullOrWhiteSpace(xml)) return "";
        try { return XDocument.Parse(xml).Descendants().Where(item => item.Name.LocalName == "Data").ElementAtOrDefault(index)?.Value.Trim() ?? ""; }
        catch (System.Xml.XmlException) { return ""; }
    }

    static string ReadMessageField(string message, string label)
    {
        var match = Regex.Match(message, $@"(?:^|\r?\n)\s*{Regex.Escape(label)}\s*:\s*(?<value>[^,\r\n]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value.Trim() : "";
    }

    static string NormalizeExceptionCode(string value) => Regex.Match(value, @"0x[0-9a-fA-F]{8}").Value.ToLowerInvariant();
    static int? ParseProcessId(string value)
    {
        value = value.Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex)) return hex;
        return int.TryParse(value, out var decimalValue) ? decimalValue : null;
    }
    static string First(string primary, string fallback) => string.IsNullOrWhiteSpace(primary) ? fallback : primary;
    static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
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
