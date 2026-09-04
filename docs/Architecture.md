# Architecture

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [**Architecture**](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

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
│   ├── Interfaces/                      One interface per service (ILogger, IOrchestratorService, ...)
│   ├── Models/                          DTOs and enums (FileProcessingStatus, BatchOperationProgress, ...)
│   └── Services/                        All business logic
│       ├── OrchestratorService.cs       Batch pipeline coordination
│       ├── ExtractXisoService.cs        External-tool conversion driver
│       ├── XdvdfsService.cs             External xdvdfs tool driver
│       ├── XisoWriter.cs                Native trim/rewrite engine
│       ├── XisoServices/XDVDFS/         XDVDFS filesystem parser (XDVDFS.cs, VolumeDescriptor.cs)
│       ├── XisoServices/BinaryOperations/  Sector utilities, FileEntry, integrity service
│       ├── ExtractFiles.cs              Archive handling (zip/7z/rar), locked-file retries
│       ├── MoveFiles.cs                 File moves with network/lock retries
│       ├── DiskMonitorService.cs        Read/write speed and free-space monitoring
│       ├── BugReportService.cs          Automatic bug reporting client
│       ├── StatsService.cs              Anonymous usage statistics client
│       ├── UpdateChecker.cs             GitHub release update checks
│       └── ...                          Logging, formatting, path helpers, etc.
└── BatchConvertIsoToXiso.Tests/         xUnit + Moq test suite (35 files, ~290 tests)
```

Bundled native executables (`extract-xiso.exe`, `xdvdfs.exe`, `bchunk.exe`, `7za.exe`, `7za_arm64.exe`) are copied to the output directory and invoked as isolated child processes.

## Dependency Injection

`App.ConfigureServices` registers every service with `Microsoft.Extensions.DependencyInjection`. All core logic is decoupled from the UI behind interfaces, enabling the service layer to be unit-tested without WPF.

| Service | Lifetime | Responsibility |
|:---|:---|:---|
| `ILogger` / `LoggerService` | Singleton | Timestamped log capture for the UI log pane |
| `IDiskMonitorService` | Singleton | Drive throughput counters and free-space queries |
| `IOrchestratorService` | Singleton | Batch pipeline: discovery, per-file dispatch, progress, cancellation |
| `IExtractXisoService` | Singleton | Drives the external `extract-xiso` tool |
| `IXdvdfsService` | Singleton | Drives the external `xdvdfs` tool |
| `XisoWriter` | Singleton | Native trim engine (uses `XDVDFS` parser) |
| `INativeIsoIntegrityService` | Singleton | Structural validation of XDVDFS images |
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
         │    ├─ .zip/.7z/.rar ──► ExtractFiles ──► temp ISO ──► convert ──► cleanup
         │    └─ .iso ──► selected engine:
         │         ├─ IExtractXisoService  (extract-xiso.exe child process)
         │         ├─ IXdvdfsService       (xdvdfs.exe child process)
         │         └─ XisoWriter           (native trim via XDVDFS parser)
         ├─ after each file: optional integrity check, optional original deletion,
         │   file moves (retry-aware), progress + stats updates
         └─ final summary (success/fail/skip counts, elapsed time)
```

Safety characteristics of the pipeline:

- **Pre-flight checks** — output-drive free space and FAT32 file-size limits are verified before conversion starts; failures skip the file with a clear message instead of failing late.
- **Environmental errors are surfaced, not reported** — disk-space and network failures stop or skip with actionable messages and are excluded from automatic bug reports.
- **Transient failures retry** — locked files and network hiccups use exponential backoff (see `ExtractFiles`, `MoveFiles`).
- **Atomic replace-originals** — deletion of inputs happens only after the converted file exists and (optionally) passes validation.
- **Cancellation is cooperative** — child processes and I/O loops observe a `CancellationToken`.

## Error Handling and Reporting

Three layers of defense:

1. **Global handlers** in `App` (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) report and keep the app alive where possible.
2. **Per-operation catches** translate known failure classes (disk full, access denied, FAT32 limits, locked files, invalid images) into user-facing messages.
3. **Automatic bug reports** are sent for genuine application defects only; environmental errors are filtered out and shown to the user instead.

## Models

| Model | Purpose |
|:---|:---|
| `FileProcessingStatus` | Per-file outcome (success/failed/skipped/…) |
| `BatchOperationProgress` | Progress snapshot used for UI updates |
| `IsoTestResultStatus` | Test-view outcome states |
| `XisoExplorerItem` | Row model for the explorer list |
| `GitHubReleaseInfo` | Deserialized GitHub release payload |
| `CloudRetryResult` | Result of a cloud-hydration retry |
| `XisoFsFileAttributes` | XDVDFS attribute flags |

## Testing

The `BatchConvertIsoToXiso.Tests` project (xUnit, Moq) covers models, services, and the XISO binary layer:

```bash
dotnet test CSharp_BatchConvertIsoToXiso.sln
```

The suite includes end-to-end-ish service tests (e.g., `OrchestratorServiceTests`, `FileExtractorServiceTests`) as well as binary-precision tests for the parser (`XdvdfsTests`, `VolumeDescriptorTests`, `FileEntryTests`, `UtilsTests`). Analyzers (Meziantou, Roslynator) enforce code quality on both projects.
