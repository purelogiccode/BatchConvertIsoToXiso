# Architecture

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [**Architecture**](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [Release Notes](Release-Notes.md) |

---

The application is a WPF (.NET 10, `net10.0-windows`) desktop app built on modern software engineering principles: dependency injection, service-oriented design, interface-driven contracts, and a comprehensive xUnit test suite.

## Solution Layout

```text
CSharp_BatchConvertIsoToXiso.sln
├── BatchConvertIsoToXiso/               Main WPF application
│   ├── App.xaml(.cs)                    Entry point, DI composition, global error handlers
│   ├── MainWindow.xaml(.cs)             Shell window + navigation
│   ├── MainWindow.ConversionAndTesting.cs   Convert/Test workflows (UI layer)
│   ├── MainWindow.XIsoExplorerLogic.cs  Explorer workflows (UI layer)
│   ├── MainWindow.ReportBugAsync.cs     In-app bug reporting entry points
│   ├── MainWindow.CheckForUpdatesAsync.cs   Update check integration
│   ├── MainWindow.UIHelpersAndWindowEvents.cs  UI helpers, links, window events
│   ├── AboutWindow.xaml(.cs)            About dialog
│   ├── Interfaces/                      One interface per service (IOrchestratorService, IXisoSharpService, ...)
│   ├── Models/                          DTOs and enums (FileProcessingStatus, BatchOperationProgress, ...)
│   └── Services/                        All business logic
│       ├── OrchestratorService.cs       Batch pipeline coordination
│       ├── XisoSharpService.cs          XISO conversion via the XISOSharp library
│       ├── XisoIntegrityService.cs      Structural audit + deep surface scan via XISOSharp
│       ├── FileExtractorService.cs      Archive handling (zip/7z/rar), locked-file retries
│       ├── FileMoverService.cs          File moves with network/lock retries
│       ├── DiskMonitorService.cs        Read/write speed and free-space monitoring
│       ├── BugReportService.cs          Automatic bug reporting client
│       ├── BugReportSink.cs             Serilog sink: forwards Warning+ events to the bug report API
│       ├── UiLogSink.cs                 Serilog sink: on-screen log pane
│       ├── StatsService.cs              Anonymous usage statistics client
│       ├── UpdateChecker.cs             GitHub release update checks
│       └── ...                          Formatting, path helpers, etc.
└── BatchConvertIsoToXiso.Tests/         xUnit + Moq test suite
```

Bundled helper executables (`bchunk.exe`, `7za.exe`, `7za_arm64.exe`) are copied to the output directory and invoked as isolated child processes. All XISO encoding and decoding is performed in-process by the `XISOSharp` NuGet package.

## Dependency Injection

`App.ConfigureServices` registers every service with `Microsoft.Extensions.DependencyInjection`. All core logic is decoupled from the UI behind interfaces, enabling the service layer to be unit-tested without WPF.

| Service | Lifetime | Responsibility |
|:---|:---|:---|
| Serilog `ILogger` | Singleton | Structured logging pipeline (UI, rolling file, and bug-report sinks) |
| `IDiskMonitorService` | Singleton | Drive throughput counters and free-space queries |
| `IOrchestratorService` | Singleton | Batch pipeline: discovery, per-file dispatch, progress, cancellation |
| `IXisoSharpService` | Singleton | XISO conversion via the XISOSharp library |
| `IXisoIntegrityService` | Singleton | Structural audit + deep surface scan via XISOSharp |
| `IFileExtractor` | Transient | Archive extraction with fallbacks and lock retries |
| `IFileMover` | Transient | Move/copy operations with retry + backoff |
| `IExternalToolService` | Singleton | Child-process lifecycle for bundled tools |
| `IBugReportService` | Singleton | Sends exception reports to the developer endpoint |
| `IStatsService` | Singleton | Anonymous usage statistics |
| `IUpdateChecker` | Singleton | Queries the GitHub releases API |
| `IMessageBoxService`, `IUrlOpener`, `IScreenshotService` | Singleton | UI-adjacent helpers kept testable |

HTTP clients are created through `IHttpClientFactory` with named clients and pooled-connection handlers.

## Conversion Pipeline

```text
MainWindow (Convert tab)
   └─► OrchestratorService
         ├─ discovers inputs (recursive option, extension filter)
         ├─ for each file:
         │    ├─ .cue/.bin ──► bchunk (external) ──► ISO
         │    ├─ .zip/.7z/.rar ──► FileExtractorService ──► temp ISO ──► convert ──► cleanup
         │    └─ .iso ──► XisoSharpService (in-process, XISOSharp library)
         ├─ after each file: optional integrity check, optional original deletion,
         │   file moves (retry-aware), progress + stats updates
         └─ final summary (success/fail/skip counts, elapsed time)
```

Safety characteristics of the pipeline:

- **Pre-flight checks** — output-drive free space and FAT32 file-size limits are verified before conversion starts; failures skip the file with a clear message instead of failing late.
- **Environmental errors are surfaced, not reported** — disk-space and network failures stop or skip with actionable messages and are excluded from automatic bug reports.
- **Transient failures retry** — locked files and network hiccups use exponential backoff (see `FileExtractorService`, `FileMoverService`).
- **Atomic replace-originals** — deletion of inputs happens only after the converted file exists and (optionally) passes validation.
- **Cancellation is cooperative** — child processes and I/O loops observe a `CancellationToken`.

## Logging

Logging uses a single [Serilog](https://serilog.net/) pipeline configured in `App` with three sinks:

1. **UI** (`UiLogSink`) — timestamped lines in the on-screen log pane.
2. **File** — rolling daily log at `%LocalAppData%\BatchConvertIsoToXiso\logs\log-*.txt` (10 MB per file, 14 files retained) with level and exception details.
3. **Bug report** (`BugReportSink`) — every event at **Warning or higher** is forwarded to the bug report API (fire-and-forget, never throws).

Services inject `Serilog.ILogger` and log with structured message templates. Expected user/environmental errors are logged at Information level so they do not generate bug reports; genuine defects log at Warning/Error/Fatal.

## Error Handling and Reporting

Three layers of defense:

1. **Global handlers** in `App` (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) log through Serilog and keep the app alive where possible; fatal shutdown paths also send a blocking report.
2. **Per-operation catches** translate known failure classes (disk full, access denied, FAT32 limits, locked files, invalid images) into user-facing messages and log at an appropriate level.
3. **Automatic bug reports** are sent by the Serilog `BugReportSink` for Warning+ events, with complete environment, error, and exception sections; expected environmental errors stay at Information level and are shown to the user instead.

## Models

| Model | Purpose |
|:---|:---|
| `FileProcessingStatus` | Per-file outcome (success/failed/skipped/…) |
| `BatchOperationProgress` | Progress snapshot used for UI updates |
| `IsoTestResultStatus` | Test-view outcome states |
| `XisoExplorerItem` | Row model for the explorer list |
| `GitHubReleaseInfo` | Deserialized GitHub release payload |
| `CloudRetryResult` | Result of a cloud-hydration retry |

## Testing

The `BatchConvertIsoToXiso.Tests` project (xUnit, Moq) covers models, services, and helper utilities:

```bash
dotnet test CSharp_BatchConvertIsoToXiso.sln
```

The suite includes service tests (e.g., `OrchestratorServiceTests`, `FileExtractorServiceTests`, `XisoSharpServiceTests`, `XisoIntegrityServiceTests`) plus model and helper coverage. Analyzers (Meziantou, Roslynator) enforce code quality on both projects.
