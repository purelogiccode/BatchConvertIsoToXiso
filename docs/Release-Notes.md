# Release Notes

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [**Release Notes**](Release-Notes.md) |

---

## Release Index

| Version | Date | Summary |
|:---|:---|:---|
| [2.9.0](#290) | September 2026 | Serilog structured logging; Warning+ events forwarded to the bug report API with full environment/exception details |
| [2.8.0](#280) | September 2026 | XISOSharp migration: in-process conversion, integrity testing, and exploration; external engines removed |
| [2.7.1](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases/tag/release_2.7.1) | July 2026 | Resource cleanup, cancellation, better error filtering |
| [2.7.0](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases/tag/release_2.7.0) | June 2026 | Improved ISO compatibility, disk-space detection, cancellation and performance |
| [2.6.1](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases/tag/release_2.6.1) | June 2026 | XGD1/XGD2/XGD3 partition offsets, dark-theme tooltip fix |
| [2.6.0](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases/tag/release_2.6.0) | June 2026 | 7-Zip CLI fallback, multilingual network errors, disk-space handling |

---

## 2.9.0

> **The structured logging release.** All logging now runs through
> [Serilog](https://serilog.net/), and every **Warning-or-higher** event is automatically forwarded
> to the Bug Report API with complete environment and exception details.

### New Features

- **Serilog logging pipeline** — three sinks: the on-screen log viewer (`UiLogSink`), a rolling daily
  file log (`%LocalAppData%\BatchConvertIsoToXiso\logs\log-*.txt`, 10 MB per file, 14 files retained),
  and a bug-report sink (`BugReportSink`) that forwards every Warning-or-higher event to the Bug
  Report API. The custom `ILogger`/`LoggerService` abstraction was removed.
- **Complete bug reports** — every report contains `=== Environment Details ===` (date, application
  name/version, OS version, architecture, bitness, Windows version, processor count, base directory,
  temp path), `=== Error Details ===`, and — when an exception is attached — `=== Exception Details ===`
  (type, message, source, and stack trace, including nested and aggregate exceptions). The API's
  `environment` and `stackTrace` fields are populated as well.
- **Fatal-error reporting** — global handlers (`AppDomain.UnhandledException`,
  `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) report through the same
  pipeline, and fatal shutdown paths send a blocking report before the process exits.

### Improvements

- **Every catch block now logs** — previously silent cleanup, retry, and ignore paths log at an
  appropriate level, and public service methods log failures with context.
- **Fewer false reports** — expected user/environmental problems (corrupt or password-protected
  archives, missing files, unsupported images, disk-space/network errors) are logged at Information
  level so they never generate bug reports; genuine failures are logged at Warning/Error/Fatal.
- **Diagnostics on disk** — the rolling log file records the startup version and full session log for
  troubleshooting.

### Breaking Changes

- None for end users. The application and test projects report version **2.9.0**
  (`AssemblyVersion`/`FileVersion`) so the update checker sees this release over 2.8.0.

### Internal

- Added `Serilog` 4.4.0 and `Serilog.Sinks.File` 7.0.0, plus new `UiLogSink`, `BugReportSink`, and
  `LoggingSinkExtensions` (`WriteTo.Ui()` / `WriteTo.BugReport()` configuration).
- Services and windows now inject `Serilog.ILogger`; `Interfaces/ILogger` and `Services/LoggerService`
  were deleted.
- Tests migrated from `Mock<ILogger>` to a capturing `TestLogger` sink; added `BugReportSinkTests`.

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.8.0...release_2.9.0>

---

## 2.8.0

> **The XISOSharp release.** All XISO encoding, decoding, integrity testing, and exploration now
> run in-process through the [XISOSharp](https://github.com/purelogiccode/XISOSharp) library.
> The bundled `extract-xiso.exe`/`xdvdfs.exe` engines and the in-repo XDVDFS implementation were removed.

### New Features

- **In-process XISOSharp conversion engine** — `XisoSharpService` converts via `XisoReader.Rewrite`.
  Redump/XGD1/XGD2/XGD3 and hybrid partition offsets are auto-detected; video partitions, padding, and
  inter-file gaps are removed. Already-optimized images (optimized tag present) are skipped automatically.
- **XISOSharp integrity service** — `XisoIntegrityService` performs a deep structural audit of the
  XDVDFS tree (`XisoReader.AuditXiso`) and reports per-entry issues, plus the optional sequential
  deep surface scan.
- **Explorer migrated to `XisoExplorer`** — browsing, double-click to open, and drag-and-drop
  extraction now use `XisoExplorer`/`ExplorerNode`, with ordinal-ignore-case sorting.
- **Smaller distribution** — `extract-xiso.exe` (40 KB) and `xdvdfs.exe` (3.6 MB) are no longer bundled.

### Improvements

- **ARM64 CUE/BIN handling now matches the documentation** — `.cue`/`.bin` inputs are skipped with a
  clear message on ARM64 (the bundled `bchunk.exe` is x64-only), instead of attempting to launch it.
  The ARM64 bundle therefore no longer ships `bchunk.exe`.
- Output free-space and FAT32 4 GB limit pre-checks before conversion, with partial-output cleanup on failure.
- Output integrity check (when enabled) validates the produced XISO before reporting success.
- Device I/O errors (`ERROR_IO_DEVICE` and localized variants) stop the batch with a drive-health
  message and are excluded from automatic bug reports.
- Locked archives are retried with exponential backoff (antivirus/download-client locks).
- Cross-volume file moves fall back to copy + delete when a direct move fails.
- Local file moves now use the same retry/backoff logic as network moves.
- More languages in network/disk-space/device error detection (English, German, French, Spanish,
  Italian, and Czech device-not-ready from bug reports).

### Bug Fixes

- **The main window is no longer delayed by drive probing** — startup cleanup used to run before the
  window was shown; probing every drive with `DriveInfo.IsReady` can block for ~20 seconds while an
  idle/spinning disk wakes up, which made the app look like it never started. Cleanup now runs after
  the window is visible and off the UI thread (the same fix removes the pause before each batch, since
  the pre-operation cleanup shares the code path).
- **Cloud/OneDrive files keep their original name** — when the source is a temporary working copy,
  the converted XISO is written using the original file name instead of the internal temporary name.
- **Skipped (already-optimized) inputs no longer delete an existing output file** — the optimized
  check now runs before the output is touched.
- **Replace Originals no longer loses data on skipped conversions** — `.iso`, archive, and CUE/BIN
  originals are deleted only when a converted file was actually produced. Cloud-backed originals are
  now deleted too (previously they were always kept, so Replace Originals silently did nothing for them).
- **Device I/O errors are no longer misclassified as network errors** — localized "I/O device error"
  messages now fall through to the drive-health path instead of the network retry path.
- **Temp-folder cleanup failures no longer mask the original error** — a folder that remains locked
  after all retries logs a warning instead of throwing from `finally` blocks.
- **The 7-Zip CLI fallback for unsupported ZIP compression honors cancellation** — the shared async
  helper terminates the child process when the batch is cancelled.
- **FAT32 and access-denied move failures** are reported with actionable guidance.
- **Better `DirectoryNotFoundException`/cloud-provider handling** across conversion and extraction.

### UI Changes

- The **Conversion Method** radio buttons were removed; conversion is always performed by XISOSharp.
- The Convert tab shows a **Conversion Engine — Powered by XISOSharp** panel with a link to the project.
- About window, instructions, and links updated to the XISOSharp engine and the new repository URL.

### Breaking Changes

- `extract-xiso.exe`, `xdvdfs.exe`, the native XISO writer, and the in-repo XDVDFS parser were removed.
- The application and test projects report version **2.8.0** (`AssemblyVersion`/`FileVersion`) so the
  update checker sees this release over 2.7.x. The file layout is otherwise the same.

### Documentation

- Documentation reorganized into the [docs folder](index.md) and published as the repository wiki.
- New [What's New](https://github.com/purelogiccode/BatchConvertIsoToXiso/blob/master/WhatsNew.md) and Release Notes pages.
- Release ZIPs now include `LICENSE.txt`, `ReadMe.md`, and `WhatsNew.md` alongside the application and bundled tools.
- [Repository](Repository.md), [Architecture](Architecture.md), [Conversion Methods](Conversion-Methods.md),
  [Usage Guide](Usage-Guide.md), [Installation](Installation.md), and
  [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) updated for the single-engine design.

### Internal

- Added `.editorconfig` and enabled Meziantou/Roslynator analyzers on both projects; project builds
  with zero warnings.
- Renamed services for clarity: `ExtractFiles` → `FileExtractorService`, `MoveFiles` → `FileMoverService`,
  `Logger` → `LoggerService`.
- Consolidated interfaces: `IXisoSharpService`, `IXisoIntegrityService`; removed `IExtractXisoService`,
  `IXdvdfsService`, `INativeIsoIntegrityService`.
- Test suite reorganized around the new services (`XisoSharpServiceTests`, `XisoIntegrityServiceTests`).

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.7.1...release_2.8.0>

---

## 2.7.1

*July 2026*

### New Features

- Added unit tests for `DiskMonitorService`, `FileExtractorService`, `OrchestratorService`, and `StatsService`.

### Improvements

- Better `DirectoryNotFoundException` handling in `ExtractXisoService` (clear message for missing drive/path).
- Better `EndOfStreamException` handling in `ExtractFiles` (corrupt/incomplete archives suggest re-download).
- Better xdvdfs error messages for non-zero exit codes (invalid/corrupt ISO hint, no bug report).
- Encrypted archives no longer trigger bug reports; treated as environmental.
- `CancellationToken` is passed to temporary-folder cleanup.

### UI Changes

- Terminal text color changed from green to white; Start button green → cyan; Exit button slate gray → purple.

### Internal

- `static` annotations on eligible lambdas; `Microsoft.NET.Test.Sdk` 18.6.0 → 18.7.0.

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.7.0...release_2.7.1>

---

## 2.7.0

*June 2026*

### Improved ISO Compatibility

- Detection of XDVDFS volumes at **Sector 0** for rebuilt XISO images without a video-partition header.
- Secondary signature validation at `+0x7EC`, matching extract-xiso/xdvdfs reference behavior.
- Relaxed partition validation to reduce false negatives (accept empty root directories, removed
  overly aggressive first-entry checks).
- Expanded fallback signature scan range to **64 KB–512 MB** (was 8 MB–512 MB), catching XGD3
  partitions closer to the start of the file.
- **Windows-1252 filename decoding** so accented Xbox filenames (`é`, `ü`, `ñ`) display correctly.

### Disk Space & Error Handling

- Centralized `IsDiskSpaceError`/`IsNetworkError` in `PathHelper`, with inner-exception checks.
- Multilingual network error detection (German, French, Spanish, Italian).
- Device-not-ready (`ERROR_NOT_READY`) no longer misclassified as a network error.
- Centralized `PathHelper.ResolveTempDirectory` used by orchestrator and conversion services.

### Cancellation, Memory & Performance

- `TaskCompletionSource`-based shutdown detection, safer CTS disposal, corrected cancellation tokens
  for process termination.
- `ArrayPool<byte>` buffers for surface scans and file verification.
- `Stopwatch`-based elapsed-time measurement.
- `IHttpClientFactory` for bug reports, stats, and update checks (connection pooling/DNS refresh).

### UI Improvements

- Subfolder search option added to the Test tab.
- Responsive XISO Explorer Name column and delayed (30 s) temp-file cleanup.
- Async log reading in bug reporting to avoid UI-thread deadlocks.

### Code Quality

- Fixed `Utils.WriteBytes` buffer-offset bug, output filename for renamed files, `Math.Abs` on HResult,
  `JsonContent`/`HttpResponseMessage` disposal, `BinaryReader` leave-open, Zip Slip false positives,
  `AggregateException` formatting, and sharing-violation detection.
- 222 tests passing with zero warnings.

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.6.1...release_2.7.0>

---

## 2.6.1

*June 2026*

### Features

- Support for multiple Xbox disc formats (XGD1, XGD2, XGD3, and an additional variant) by trying
  multiple known partition offsets when reading the XDVDFS volume descriptor.

### Bug Fixes

- Added the missing `0x89D80000` offset to the fallback signature scan, matching the offset used
  for Redump type 5 ISOs.
- Added a ToolTip style to prevent white-on-white text in the dark theme.

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.6.0...release_2.6.1>

---

## 2.6.0

*June 2026*

### New Features

- **7-Zip CLI fallback** for ZIP archives using unsupported compression methods (e.g. ZSTD/method 21),
  with install guidance when 7-Zip is not found.
- Multilingual network error detection.
- `ResolveTempDirectory` automatically searches for alternative temp drives with free space.
- `TempFolderCleanupHelper` scans all fixed drives for orphaned temp folders.

### Bug Fixes

- Fixed bug #56363 (7-Zip fallback) and #56306 (archive path validation).
- Improved disk-space detection for negative HResults, inner exceptions, and French messages.
- Retry logic with exponential backoff for transient I/O errors during copy and directory enumeration.
- `EndOfStreamException` in XDVDFS traversal returns partial results for corrupted ISOs.

### Dependencies

- `Microsoft.Extensions.DependencyInjection` 10.0.9; `SharpCompress` 0.49.1.

**Full Changelog**: <https://github.com/purelogiccode/BatchConvertIsoToXiso/compare/release_2.5.0...release_2.6.0>

---

Older releases are available on the [Releases page](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases).
