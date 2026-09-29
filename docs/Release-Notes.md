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
| [3.0.0 (unreleased)](#300-unreleased) | — | Cross-platform Avalonia port (Windows/Linux/macOS); CHD output, Xbox CHD integrity testing, and CHD exploration; Serilog logging with automatic bug reporting; per-file selection lists; XISO/ZAR/CSO output formats; extensive reliability and bug-fix pass (38 fixes) |
| [2.8.0](#280) | September 2026 | XISOSharp migration: in-process conversion, integrity testing, and exploration; external engines removed |
| [2.7.1](https://github.com/purelogiccode/XboxIsoStudio/releases/tag/release_2.7.1) | July 2026 | Resource cleanup, cancellation, better error filtering |
| [2.7.0](https://github.com/purelogiccode/XboxIsoStudio/releases/tag/release_2.7.0) | June 2026 | Improved ISO compatibility, disk-space detection, cancellation and performance |
| [2.6.1](https://github.com/purelogiccode/XboxIsoStudio/releases/tag/release_2.6.1) | June 2026 | XGD1/XGD2/XGD3 partition offsets, dark-theme tooltip fix |
| [2.6.0](https://github.com/purelogiccode/XboxIsoStudio/releases/tag/release_2.6.0) | June 2026 | 7-Zip CLI fallback, multilingual network errors, disk-space handling |

---

## 3.0.0 (unreleased)

> **The cross-platform CHD & logging release (in development).** The application was ported from WPF to
> **Avalonia** and now runs on Windows, Linux, and macOS (x64 and ARM64), and
> [CHDSharp](https://github.com/purelogiccode/CHDSharp) is integrated as a second in-process engine:
> Xbox and Xbox 360 ISOs can be converted to CHD v5, and Xbox DVD CHD files can be integrity-tested
> and explored without extraction. All logging runs through [Serilog](https://serilog.net/) with
> automatic bug reporting, and the Convert and Test views now offer per-file selection lists with
> XISO/ZAR/CSO/CHD output formats.

### New Features

- **Cross-platform UI** — WPF replaced with **Avalonia** (same dark theme, colors, fonts, layout,
  and control styling) with one `net10.0` codebase. Six release archives are produced:
  `win-x64`/`win-arm64`, `linux-x64`/`linux-arm64`, and `osx-x64`/`osx-arm64`. All imaging
  libraries are pure managed code, so conversion, integrity testing, and exploration work
  identically everywhere. Windows-only integrations degrade gracefully: the read/write speed
  monitor shows `N/A` (Windows performance counters), the 7-Zip CLI fallback uses the system `7z`
  from `PATH`, and file/folder pickers and links use the OS-native dialogs.
- **CHD output format** — the Convert tab's Output Format selector gains **CHD** (`.chd`,
  Compressed Hunks of Data). The optimized game partition is encoded by `ChdEncoder.EncodeRaw`
  (CHDSharp) with the chdman `createdvd` preset — 4096-byte hunks, 2048-byte units, the
  `lzma,zlib,huff,flac` codec list, and the `DVD ` metadata tag. Non-optimized (Redump) inputs are
  rewritten to the game partition first, so `Skip $SystemUpdate` and `Delete Originals` work exactly
  as they do for XISO/ZAR/CSO. `Check Output Integrity` deep-verifies the new CHD — every hunk is
  decompressed and every checksum and hash is validated — before the conversion is reported as
  successful.
- **CHD integrity testing** — `.chd` files are testable, limited to Xbox DVD images. The container
  is verified with `Chd.CheckFile` (header-only, or every hunk and checksum with `Perform Deep
  Scan`), and the Xbox filesystem is audited over the decompressed image through XISOSharp
  (`AuditXiso(IBlockDevice)`). CD/GD-ROM/hard-disk CHDs and differential child CHDs are rejected
  with a clear message.
- **CHD exploration** — the Explorer opens `.chd` files through a keep-open `ChdImageStream`
  (hunks decompressed on demand) and lists and copies out entries with the XISOSharp stream APIs.
  Both game-partition-only CHDs (produced by this app) and full Redump-image CHDs (produced by
  `chdman createdvd`) are supported — partition offsets are auto-detected.
- **Serilog logging pipeline** — three sinks: the on-screen log viewer (`UiLogSink`), a rolling daily
  file log (`%LocalAppData%\XboxIsoStudio\logs\log-*.txt`, 10 MB per file, 14 files retained),
  and a bug-report sink (`BugReportSink`) that forwards every Warning-or-higher event to the Bug
  Report API. Avalonia's internal diagnostics are routed through the same pipeline
  (`AvaloniaSerilogSink`) instead of the Trace-based default. The custom `ILogger`/`LoggerService`
  abstraction was removed.
- **Complete bug reports** — every report contains `=== Environment Details ===` (date, application
  name/version, OS version, architecture, bitness, Windows version, processor count, base directory,
  temp path), `=== Error Details ===`, and — when an exception is attached — `=== Exception Details ===`
  (type, message, source, and stack trace, including nested and aggregate exceptions). The API's
  `environment` and `stackTrace` fields are populated as well.
- **Fatal-error reporting** — global handlers (`AppDomain.UnhandledException`,
  `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) report through the same
  pipeline, and fatal shutdown paths send a blocking report before the process exits.
- **Selectable file lists** — after choosing an input folder, the Convert view lists every supported
  file (`.iso`, `.zip`, `.7z`, `.rar`) and the Test view lists every ISO, each with a
  **Select** checkbox, file name (relative to the input folder), and formatted size. Only ticked
  files are processed. **Select All** / **Deselect All** buttons toggle the whole list in one click,
  and toggling **Search Subfolders** refreshes the list immediately (the list also refreshes after
  each batch so it reflects deleted originals and files moved to `_success`/`_failed`).
- **Compressed output formats** — the Convert tab now produces **ZAR** (`.zar`, ZArchive/zstd,
  loadable directly in Xenia canary) and **CSO** (`.cso`, CISO v2/LZ4, byte-identical to
  `xdvdfs compress`) in addition to optimized XISO. ZAR streams the game-partition tree straight
  into the archive; CSO repacks non-optimized inputs to a temporary XISO first. `Skip $SystemUpdate`,
  `Delete Originals`, and integrity checking apply to all formats, and the file list stays
  ISO/archive-only.

### Improvements

- **Every catch block now logs** — previously silent cleanup, retry, and ignore paths log at an
  appropriate level, and public service methods log failures with context.
- **Fewer false reports** — expected user/environmental problems (corrupt or password-protected
  archives, missing files, unsupported images, disk-space/network errors) are logged at Information
  level so they never generate bug reports; genuine failures are logged at Warning/Error/Fatal.
- **Diagnostics on disk** — the rolling log file records the startup version and the full session log
  for troubleshooting.
- **Single source of truth for supported files** — the `SupportedFiles` filter is shared by the UI
  lists and the orchestrator folder scans, so the UI can never offer a file the converter cannot handle.
- **Responsive with large folders** — list items are added in chunks of 100 on the UI thread, so
  folders with thousands of files stay responsive.
- **New orchestrator API** — `IOrchestratorService.ConvertFilesAsync` and `TestFilesAsync` process an
  explicit file list; the folder-scanning `ConvertAsync`/`TestAsync` remain for compatibility.
- **Side-by-side layout** — the Convert and Test views (folder pickers, options, and the selectable
  file list) now occupy the left panel, while the log viewer / XISO explorer fills the right panel,
  with a draggable splitter between them. On the Explorer tab the file picker and the explorer list
  share the full window width and the log panel is hidden.
- **Collapsible Options** — the Options panel on both tabs is an `Expander` styled to match the
  theme; click its header to collapse or expand it and give the file list more room.
- **Theme-consistent list styling** — new `FileListDataGridStyle` and related styles match the dark
  terminal theme.

### Breaking Changes

- **CUE/BIN input support was removed.** The bundled `bchunk.exe` and the `.cue` file type are gone:
  the application now supports only `.iso` (Redump full-disc images) and already-optimized XISO
  files, plus the archive formats (`.zip`, `.7z`, `.rar`). `.cue`/`.bin` files are no longer listed
  or processed. The application and test projects report version **3.0.0**
  (`AssemblyVersion`/`FileVersion`) so the update checker sees this release over 2.8.0.

### Bug Fixes

A full review of the `XboxIsoStudio` and `XboxIsoStudio.Tests` projects found and fixed
**38 verified defects**. The most significant ones:

**Data safety**

- **Archive deletion predicate was too loose** — an archive could be deleted when only one of its
  images was converted, losing the unprocessed contents. The extractor now reports skipped entries
  (`ArchiveExtractionResult`), and the archive is removed only when every entry was extracted *and*
  every extracted image was converted or explicitly skipped by the user.
- **Same-named inputs overwrote each other** — two inputs sharing a base name (for example
  `Disc1/game.iso` and `Disc2/game.iso`) mapped to one output path; the engines delete a pre-existing
  output, so the second conversion destroyed the first and both originals were then deleted. Output
  paths are now reserved per batch (`game.iso`, `game (2).iso`, …).
- **Invalid inputs were treated as failures and could count toward deletion** — engines now return a
  distinct `FileProcessingStatus.InvalidInput`, which never deletes the source and keeps archives alive.
- **Test temp copies leaked** when the cloud copy failed or the user canceled the cloud retry; the
  cleanup `finally` now covers every exit path.

**Conversion correctness**

- **`Skip $SystemUpdate` was ignored for already-optimized inputs written as CSO/CHD** — those inputs
  are now rewritten through the `$SystemUpdate` filter before compression.
- **Wrong XGD partition offset** for Redump-type-5 images with an unknown/unreadable video PVD — the
  dead `GetXgdType` fallback is now reachable, so XGD2 offsets are selected correctly.
- **Split CISO cloud copies dropped the `.1` part marker** and only copied part 1; every part is now
  copied with its original numbering, and a split set is recognized as such.
- **Free-space pre-checks used the uncompressed size for compressed outputs**, rejecting conversions
  that would easily fit; compressed formats now use a size-aware estimate.
- **Temp-path and zip-slip path comparisons were case-insensitive on case-sensitive file systems**;
  they now follow the platform (`PathHelper.PathComparison`).
- **7-Zip arguments were built by string interpolation**, so quotes in paths could split or inject
  arguments; the fallback now uses `ProcessStartInfo.ArgumentList`.
- **Required-space arithmetic could overflow** for extreme sizes; the buffer addition now saturates at
  `long.MaxValue`.

**Robustness**

- **One unreadable file aborted the whole integrity-test batch** — each test iteration now has the
  same per-file error handling as conversion.
- **File-list scans failed entirely if one file vanished or locked mid-scan** — sizes are read through
  a safe helper.
- **Concurrent file-list refreshes could clear or overwrite the current list** — stale scans are
  discarded with a generation counter.
- **Temp cleanup deleted any `XboxIsoStudio_*` folder** (no ownership or age check); it now removes
  only app-created GUID work folders older than six hours, so user folders and another instance's
  active folders are left alone.
- **Canceled cleanup could mask the original error** — `finally` cleanup passes
  `CancellationToken.None` and the retry helper swallows cancellation.
- **Explorer could be disposed while a background copy-out was using it** — copy-outs now hold an
  explorer lease (`SemaphoreSlim`) and disposal is deferred until they finish.
- **Exit button bypassed the close confirmation** (`desktop.Shutdown()` forces the window closed even
  when `Closing` cancels); it now calls `Close()`, and the closing flow is reentrancy-guarded so two
  prompts cannot appear and discarded-task exceptions are observed.
- **Completion dialogs appeared for canceled or never-started batches** and the UI could stay disabled
  if a dialog failed; operation state is reset before summaries and the summary is tailored to the
  outcome.
- **Failed explorer opens kept a disposed explorer and stale UI**; the reference is cleared before
  opening and the grid is emptied on failure.
- **Same-second screenshots overwrote each other** — names now include milliseconds plus a collision
  probe.
- **Non-DVD CHDs were accepted by the explorer** and only failed later; they are now rejected at open
  time via `Chd.Classify`, matching the integrity service.
- **Disk-monitor error status was erased immediately** by `StopMonitoring()`, and a
  `PerformanceCounter` could leak when priming failed; both are fixed.
- **UNC paths never reached the network-status branch** because `GetDriveLetter` returns `null` for
  them and `CurrentDriveLetter` starts as `null`.

**Logging and reporting**

- **Environmental/transient events (locked files, full disks, offline networks, permission problems,
  update checks, URL opening, performance counters) were logged at Warning/Error and auto-uploaded**
  as bug reports. They now log at Information, and retries only retry genuinely transient I/O errors.
- **Cloud-file error codes were wrong/dead** (`0x80070146` instead of `0x8007016A`, and a raw Win32
  code compared against a full HRESULT); detection now compares the masked HRESULT.
- **Bug-report send failures were completely silent** — they are now recorded in the log at
  Information level.
- **Fatal-error reporting blocked the UI for a full 5 seconds and could fail to send** — the HTTP call
  uses `ConfigureAwait(false)` so the report completes while the UI thread is waiting.

**Platform and infrastructure**

- **Constructors mutated the injected `HttpClient`** (timeout, default headers); headers and timeouts
  are now applied per request, so a shared or reused client cannot be corrupted.
- **`ProcessTerminatorHelper` could throw from its initial `HasExited` probe** — it now also catches
  `Win32Exception`.
- **The "Invalid ISO" counter counted every failure** (disk errors, access denied, move failures);
  engines and the integrity service now report a dedicated `InvalidIsoCount`, so the "Many files were
  not valid Xbox ISOs" warning only counts genuinely invalid images.

### Internal

- Added `Models/ArchiveExtractionResult` (extraction success + skipped entries), the
  `FileProcessingStatus.InvalidInput` value, `BatchOperationProgress.InvalidIsoCount`, and
  `PathHelper.PathComparison`/`PathHelper.AddSafetyBuffer`.
- `IFileExtractor.ExtractArchiveAsync` now returns `ArchiveExtractionResult` instead of a bool.
- The xUnit suite grew to **398 tests**, including regression tests for every fixed defect. Test
  parallelization is disabled (`CollectionBehavior(DisableTestParallelization = true)`) because
  XISOSharp uses process-wide static state and the process working directory during extract/pack.
- Ported `MainWindow`, `AboutWindow`, `App`, and theming from XAML/WPF to Avalonia
  (`Program.cs`, `App.axaml`, `MainWindow.axaml`, `AboutWindow.axaml`); added
  `Dialogs/MessageBoxWindow` (cross-platform modal dialogs replacing `System.Windows.MessageBox`)
  and rewrote the screenshot service with `RenderTargetBitmap`.
- `IMessageBoxService` is now async (`ShowAsync`/`ShowErrorAsync`/`ShowWarningAsync`) with
  framework-neutral result/button/icon enums; file and folder pickers use Avalonia's
  `StorageProvider`; drag-out of explorer entries uses the Avalonia `DataTransfer` API.
- Platform guards: `DiskMonitorService` returns `N/A` on non-Windows (the `System.Diagnostics.PerformanceCounter`
  package is referenced but only used under `OperatingSystem.IsWindows()`), bundled `7za.exe` files
  are copied to Windows outputs only, and the Unix fallback searches `7z`/`7za`/`7zz` on `PATH`.
- Retargeted both projects from `net10.0-windows` to `net10.0`; CI now builds/tests on
  `windows-latest`, `ubuntu-latest`, and `macos-latest` and publishes all six RIDs.
- Added the **CHDSharp** 1.4.3 package (pure managed code, no native dependencies).
- New `IChdService`/`ChdService` (conversion and deep verification), `ChdImageExplorer`,
  `OutputFormat.Chd`, `.chd` filters in `SupportedFiles`, and a `ChdBlockDevice` adapter
  (`IBlockDevice` over `ChdImageStream`) for the XISOSharp filesystem audit.
- Tests for the new service (valid/optimized/non-optimized inputs, cancellation-safe cleanup,
  invalid images), CHD integrity testing (valid, deep scan, non-DVD rejection, corruption), the CHD
  explorer (listing, copy-out, missing paths, invalid files), and orchestrator CHD routing.
- Added `Serilog` 4.4.0 and `Serilog.Sinks.File` 7.0.0, plus `UiLogSink`, `BugReportSink`,
  `AvaloniaSerilogSink`, and `LoggingSinkExtensions` (`WriteTo.Ui()` / `WriteTo.BugReport()`
  configuration).
- Services and windows inject `Serilog.ILogger`; `Interfaces/ILogger` and `Services/LoggerService`
  were deleted.
- Tests migrated from `Mock<ILogger>` to a capturing `TestLogger` sink; added `BugReportSinkTests`.
- Added `Models/FileItem` (selectable list item with `INotifyPropertyChanged`) and
  `Services/SupportedFiles` (shared extension filters), plus `MainWindow.FileSelection.cs` for
  scanning/populating the lists.
- Removed `ExternalToolService`/`IExternalToolService`, the `GetReferencedBinFilesFromCue` parser,
  and the `ProcessCueAsync`/`ProcessCueInternalAsync` pipeline; the archive extraction loop now
  filters with `SupportedFiles.IsIso`.
- `IXisoSharpService.ConvertIsoAsync` replaces `ConvertIsoToXisoAsync` and dispatches to
  `XisoReader.Rewrite` (XISO), `XisoZarchive.CreateZar` (ZAR), or `CisoWriter.CompressToCso` (CSO);
  new `OutputFormat` enum, Redump partition-offset detection via `XgdTables`, and throttled progress
  adapters. `IOrchestratorService.ConvertAsync`/`ConvertFilesAsync` take the format and derive the
  `.iso`/`.zar`/`.cso` output name.
- Added tests for `FileItem`, `SupportedFiles`, the new `ConvertFilesAsync`/`TestFilesAsync`
  orchestrator overloads (file-list filtering, empty lists, pass/fail moves), ZAR/CSO output
  (round-trip extraction/decompression, `$SystemUpdate` exclusion, format/extension plumbing).
- Enabled XML documentation generation for the application project; every public type and member
  is documented and the build reports zero warnings.

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.8.0...release_3.0.0>

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
- New [What's New](https://github.com/purelogiccode/XboxIsoStudio/blob/master/WhatsNew.md) and Release Notes pages.
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

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.7.1...release_2.8.0>

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

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.7.0...release_2.7.1>

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

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.6.1...release_2.7.0>

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

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.6.0...release_2.6.1>

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

**Full Changelog**: <https://github.com/purelogiccode/XboxIsoStudio/compare/release_2.5.0...release_2.6.0>

---

Older releases are available on the [Releases page](https://github.com/purelogiccode/XboxIsoStudio/releases).
