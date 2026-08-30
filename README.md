# CrashLens

CrashLens is a Windows desktop utility for inspecting recent application crashes, hangs, and Windows Error Reporting records without working directly in Event Viewer. It is designed as a compact technical workstation: the crash list is always visible, structured evidence is central, and raw event data remains one tab away.

## Architecture

| Project | Responsibility |
|---|---|
| `CrashLens.App` | WinUI 3 desktop UI, MVVM view models, commands, and sample data |
| `CrashLens.Core` | Domain models, event parsing, exception interpretation, and analysis |
| `CrashLens.Infrastructure` | Windows Application Event Log reader and report exporters |
| `CrashLens.Cli` | Command-line listing of recent crash records |

The MVP listens for Application log event IDs 1000 (Application Error), 1001 (Windows Error Reporting), and 1002 (Application Hang). The parser is intentionally conservative: its Possible Cause statements identify evidence and likelihood, never assert a root cause.

## Run

Requirements: Windows 10 (19041+) or Windows 11, the .NET 10 SDK, and the Windows App SDK development workload.

```powershell
dotnet restore
dotnet build CrashLens.sln
dotnet run --project src/CrashLens.App
```

Use **Refresh** to read the prior 24 hours of the Application log. Until then, the app starts with realistic sample records so the inspector can be evaluated immediately. The CLI accepts an optional number of hours:

```powershell
dotnet run --project src/CrashLens.Cli -- 168
```

## Public releases

End users do not need Visual Studio or the .NET runtime. Push a tag such as `v0.1.0` to GitHub. The included GitHub Actions workflow builds a self-contained `CrashLens.App.exe` on a Windows runner and attaches it to the GitHub Release. Users download and run that EXE.

## MVP scope

- Dark, dense, resizable WinUI layout with menu, toolbar, crash list, structured inspector, technical tabs, and status bar.
- Event parsing and common exception-code interpretation.
- Initial live Windows Event Log integration.
- Raw message/XML tabs and Markdown report preview.
- JSON, Markdown, and text exporter services; path/user masking contract.

## Roadmap

- Wire File menu actions to native save pickers and all export formats.
- Add real time range, search, sort, context menu, and privacy settings controls.
- Correlate WER buckets, reliability-history records, and related event sequences.
- Add parser tests using captured, anonymized event fixtures.

## License

CrashLens is released under the [MIT License](LICENSE).
