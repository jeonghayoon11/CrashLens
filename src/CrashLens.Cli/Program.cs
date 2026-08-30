using CrashLens.Core;
using CrashLens.Infrastructure;
var hours = args.Length > 0 && int.TryParse(args[0], out var value) ? value : 24;
var reader = new WindowsEventLogReader(new CrashParser());
var crashes = await reader.ReadAsync(DateTimeOffset.Now.AddHours(-hours));
foreach (var crash in crashes) Console.WriteLine($"{crash.Time:u}\t{crash.ApplicationName}\t{crash.ExceptionCode ?? "—"}\t{crash.FaultingModule ?? "—"}");
