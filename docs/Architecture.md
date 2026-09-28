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
CSharp_XboxIsoStudio.sln
├── XboxIsoStudio/               Main WPF application
│   ├── App.xaml(.cs)                    Entry point, DI composition, global error handlers
│   ├── MainWindow.xaml(.cs)             Shell window + navigation
│   ├── MainWindow.ConversionAndTesting.cs   Convert/Test workflows (UI layer)
│   ├── MainWindow.FileSelection.cs      Folder scanning + selectable file lists (UI layer)
│   ├── MainWindow.XIsoExplorerLogic.cs  Explorer workflows (UI layer)
│   ├── MainWindow.CheckForUpdatesAsync.cs   Update check integration
│   ├── MainWindow.UIHelpersAndWindowEvents.cs  UI helpers, links, window events
│   ├── AboutWindow.xaml(.cs)            About dialog
│   ├── Interfaces/                      One interface per service (IOrchestratorService, IXisoSharpService, ...)
│   ├── Models/                          DTOs and enums (FileProcessingStatus, FileItem, BatchOperationProgress, ...)
│   └── Services/                        All business logic
│       ├── OrchestratorService.cs       Batch pipeline coordination
│       ├── SupportedFiles.cs            Extension filters shared by the UI lists and folder scans
│       ├── XisoSharpService.cs          XISO conversion via the XISOSharp library
│       ├── XisoIntegrityService.cs      Structural audit + deep scan via XISOSharp / ZArchiveSharp
│       ├── ImageExplorerFactory.cs      Opens the right IImageExplorer (XISO/CISO or ZAR)
│       ├── XisoImageExplorer.cs         IImageExplorer over ISO/CSO via XisoExplorer
│       ├── ZarImageExplorer.cs          IImageExplorer over ZAR via ZArchiveReader (zip-slip safe)
│       ├── ImagePaths.cs                Shared internal-path normalization helpers
│       ├── FileExtractorService.cs      Archive handling (zip/7z/rar), locked-file retries
│       ├── FileMoverService.cs          File moves with network/lock retries
│       ├── DiskMonitorService.cs        Read/write speed and free-space monitoring
│       ├── BugReportService.cs          Automatic bug reporting client
│       ├── BugReportSink.cs             Serilog sink: forwards Warning+ events to the bug report API
│       ├── UiLogSink.cs                 Serilog sink: on-screen log pane
│       ├── StatsService.cs              Anonymous usage statistics client
│       ├── UpdateChecker.cs             GitHub release update checks
│       └── ...                          Formatting, path helpers, etc.
└── XboxIsoStudio.Tests/         xUnit + Moq test suite
```

Bundled helper executables (`7za.exe`, `7za_arm64.exe`) are copied to the output directory and invoked as isolated child processes. All XISO encoding and decoding is performed in-process by the `XISOSharp` NuGet package.

## Dependency Injection

`App.ConfigureServices` registers every service with `Microsoft.Extensions.DependencyInjection`. All core logic is decoupled from the UI behind interfaces, enabling the service layer to be unit-tested without WPF.

| Service | Lifetime | Responsibility |
|:---|:---|:---|
| Serilog `ILogger` | Singleton | Structured logging pipeline (UI, rolling file, and bug-report sinks) |
| `IDiskMonitorService` | Singleton | Drive throughput counters and free-space queries |
| `IOrchestratorService` | Singleton | Batch pipeline: per-file dispatch for the selected files, progress, cancellation |
| `IXisoSharpService` | Singleton | XISO/ZAR/CSO conversion via the XISOSharp library |
| `IXisoIntegrityService` | Singleton | Structural audit + deep scan via XISOSharp (ISO/CSO) and ZArchiveSharp (ZAR) |
| `IImageExplorer` | Per open image | Explorer over ISO/CSO (`XisoExplorer`) or ZAR (`ZArchiveReader`), built by `ImageExplorerFactory` |
| `IFileExtractor` | Transient | Archive extraction with fallbacks and lock retries |
| `IFileMover` | Transient | Move/copy operations with retry + backoff |
| `IBugReportService` | Singleton | Sends exception reports to the developer endpoint |
| `IStatsService` | Singleton | Anonymous usage statistics |
| `IUpdateChecker` | Singleton | Queries the GitHub releases API |
| `IMessageBoxService`, `IUrlOpener`, `IScreenshotService` | Singleton | UI-adjacent helpers kept testable |

HTTP clients are created through `IHttpClientFactory` with named clients and pooled-connection handlers.

## Conversion Pipeline

```text
MainWindow (Convert tab)
   ├─ scans the input folder for supported files (SupportedFiles filter, recursive option)
   ├─ user ticks the files to process (selectable DataGrid list)
   └─► OrchestratorService (ConvertFilesAsync)
         ├─ for each selected file:
         │    ├─ .zip/.7z/.rar ──► FileExtractorService ──► temp ISO ──► convert ──► cleanup
         │    └─ .iso ──► XisoSharpService (in-process: XISO / ZAR / CSO)
         ├─ after each file: optional integrity check, optional original deletion,
         │   file moves (retry-aware), progress + stats updates
         └─ final summary (success/fail/skip counts, elapsed time)
```

The requested output format flows from the UI through `ConvertFilesAsync`/`ConvertAsync` to
`IXisoSharpService.ConvertIsoAsync`: **XISO** uses `XisoReader.Rewrite`, **ZAR** streams the
game-partition tree via `XisoZarchive.CreateZar` (Redump partition offsets detected with
`XgdTables`), and **CSO** repacks non-optimized inputs to a temporary XISO and calls
`CisoWriter.CompressToCso` (CISO v2/LZ4).

The folder-scanning `ConvertAsync`/`TestAsync` overloads remain available for callers that want the
orchestrator to discover files itself; the UI always passes the explicit list of ticked files.

The test pipeline (`TestAsync`/`TestFilesAsync` → `IXisoIntegrityService`) dispatches by extension:
`.iso` and `.cso` (single or split `.1.cso`) go through `XisoReader.AuditXiso(...,
requireOptimizedTag: false)` and, with the deep scan enabled, a sequential read of the whole
decompressed image; `.zar` is opened, tree-walked, and (deep scan) fully decompressed through
`ZArchiveReader`. Split CISO continuation parts are hidden from the list and move together with
part 1.

Safety characteristics of the pipeline:

- **Pre-flight checks** — output-drive free space and FAT32 file-size limits are verified before conversion starts; failures skip the file with a clear message instead of failing late.
- **Environmental errors are surfaced, not reported** — disk-space and network failures stop or skip with actionable messages and are excluded from automatic bug reports.
- **Transient failures retry** — locked files and network hiccups use exponential backoff (see `FileExtractorService`, `FileMoverService`).
- **Atomic replace-originals** — deletion of inputs happens only after the converted file exists and (optionally) passes validation.
- **Cancellation is cooperative** — child processes and I/O loops observe a `CancellationToken`.

## Logging

Logging uses a single [Serilog](https://serilog.net/) pipeline configured in `App` with three sinks:

1. **UI** (`UiLogSink`) — timestamped lines in the on-screen log pane.
2. **File** — rolling daily log at `%LocalAppData%\XboxIsoStudio\logs\log-*.txt` (10 MB per file, 14 files retained) with level and exception details.
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
| `ImageEntry` | One file/directory inside an image or archive (name, path, size, type) |
| `XisoExplorerItem` | Row model for the explorer list (wraps an `ImageEntry`) |
| `GitHubReleaseInfo` | Deserialized GitHub release payload |
| `CloudRetryResult` | Result of a cloud-hydration retry |

## Testing

The `XboxIsoStudio.Tests` project (xUnit, Moq) covers models, services, and helper utilities:

```bash
dotnet test CSharp_XboxIsoStudio.sln
```

The suite includes service tests (e.g., `OrchestratorServiceTests`, `FileExtractorServiceTests`, `XisoSharpServiceTests`, `XisoIntegrityServiceTests`) plus model and helper coverage. Analyzers (Meziantou, Roslynator) enforce code quality on both projects.
