# What's New in XISO Studio

<!-- Keep this file focused on the newest release. Full history lives in docs/Release-Notes.md. -->

## Version 3.0.0

**Release date:** September 2026

Version 3.0.0 is the **CHD, cross-platform & logging release**: the application was ported from WPF to **Avalonia** and now runs on **Windows, Linux, and macOS**, the [CHDSharp](https://github.com/purelogiccode/CHDSharp) library is now built in, so Xbox and Xbox 360 images can be converted to **CHD** (`.chd`, Compressed Hunks of Data) and Xbox DVD CHD files can be integrity-tested and explored without extraction, and all logging runs through [Serilog](https://serilog.net/) with automatic bug reporting.

### Highlights

#### Cross-platform: Windows, Linux, and macOS
- The UI was ported from WPF to **[Avalonia](https://avaloniaui.net/)** with the same dark theme, colors, fonts, layout, and controls — one codebase now produces binaries for **Windows, Linux, and macOS** on **x64 and ARM64** (six release archives).
- All imaging libraries (XISOSharp, ZArchiveSharp, CHDSharp, SharpCompress) are pure managed code with no native dependencies, so every feature — conversion, testing, and exploration — works identically on all three platforms.
- Windows-specific integrations degrade gracefully: the disk read/write speed monitor uses Windows performance counters and shows **N/A** on Linux/macOS, the archive fallback uses the system `7z` from `PATH` instead of the bundled Windows executable, and links/pickers use the native OS dialogs.
- CI now builds and tests on **Windows, Linux, and macOS** and publishes all six platform archives.

#### CHD output format
- The Convert tab's **Output Format** selector now includes **CHD** (`.chd`, CHD v5). The optimized game partition is encoded with the chdman `createdvd` preset — 4096-byte hunks, 2048-byte units, and the `lzma,zlib,huff,flac` codec list — and tagged as a DVD image.
- Non-optimized (Redump) inputs are rewritten to the game partition first, so **Skip $SystemUpdate** and **Delete Originals** work exactly as they do for XISO/ZAR/CSO.
- **Check Output Integrity** deep-verifies the new CHD — every hunk is decompressed and every checksum and hash is validated — before the conversion is reported as successful.

#### Test Xbox CHD files
- The Test tab now accepts `.chd` files, limited to Xbox DVD images: CD/GD-ROM/hard-disk CHDs and differential child CHDs are rejected with a clear message.
- The CHD container is verified first (header-only, or every hunk and checksum with **Perform Deep Scan**), and the Xbox filesystem structure inside the decompressed image is then audited with XISOSharp.

#### Explore Xbox CHD files
- The Explorer opens `.chd` files, decompressing hunks on demand, and browses and copies out entries through XISOSharp just like ISO/CSO/ZAR.
- Both game-partition-only CHDs (produced by this app) and full Redump-image CHDs (produced by `chdman createdvd`) are supported — partition offsets are auto-detected.

#### Structured logging with automatic bug reports
- All logging now runs through [Serilog](https://serilog.net/): the on-screen log viewer, a rolling daily file log (`%LocalAppData%\XISOStudio\logs`), and a bug-report sink that forwards every **Warning-or-higher** event to the Bug Report API.
- Reports include complete Environment, Error, and Exception sections (type, message, source, and stack trace, including nested exceptions); expected user/environmental errors are logged at Information level so they never generate noise.
- Every `catch` block logs at an appropriate level, Avalonia's internal diagnostics are routed through the same pipeline, and global handlers (`AppDomain.UnhandledException`, `DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`) report through the same path.

#### Pick exactly which files to process
- After choosing an input folder, the Convert view lists every supported file (`.iso`, `.zip`, `.7z`, `.rar`) and the Test view lists every ISO, with a **Select** checkbox, file name, and size.
- Use **Select All** / **Deselect All** to toggle the whole list, then click **Start** — only ticked files are processed.
- **Search Subfolders** now rescans the list immediately when toggled, and the list refreshes automatically after each batch (for example after originals are deleted or tested files are moved to `_success`/`_failed`).

#### Choose the output format
- A new **Output Format** selector on the Convert tab produces **XISO** (default), **ZAR** (`.zar`, ZArchive/zstd — Xenia canary loads it directly), **CSO** (`.cso`, CISO v2/LZ4, byte-identical to `xdvdfs compress`), or **CHD** (`.chd`).
- **Skip $SystemUpdate**, **Delete Originals**, and integrity checking work for all formats — for ZAR/CSO the integrity check validates the source image that gets packed.

#### Consistent, responsive lists
- The lists use the same dark terminal styling as the rest of the app and load in chunks, so folders with thousands of files stay responsive.
- The Convert and Test panels (folder pickers, options, and file list) now sit on the **left**, with the log viewer / XISO explorer on the **right** and a draggable splitter between them — the same layout used by the other BatchConvert tools.
- The **Explorer** tab shows the file picker at the top with the explorer list directly below it and uses the full window width — the log panel is hidden on this tab.
- The **Options** panel on both tabs is collapsible: click its header to hide the checkboxes and give the file list more room.
- File names are shown relative to the selected input folder (subfolders included), and sizes are formatted for readability.
- The engine applies the same extension filters as the lists, so the UI can never offer a file the converter cannot handle.

#### ISO/XISO only (CUE/BIN support removed)
- The bundled `bchunk.exe` and `.cue` input support were removed. The application now supports only `.iso` (Redump full-disc images) and already-optimized XISO files, plus archives (`.zip`, `.7z`, `.rar`). CUE/BIN images are no longer listed or converted.

#### Reliability and bug fixes
- **No more silent data loss** — with **Delete Originals** enabled, an archive is removed only when every entry was extracted and every extracted image was converted; skipped entries (extra ISOs, non-ISO images, already-optimized files) keep the archive.
- **No output collisions** — two inputs with the same file name (for example `Disc1/game.iso` and `Disc2/game.iso`) now produce `game.iso` and `game (2).iso` instead of overwriting each other.
- **`Skip $SystemUpdate` works everywhere** — already-optimized inputs are rewritten through the filter before CSO/CHD packing, so the option is honored for every output format.
- **Cloud and split images are handled correctly** — split CISO sets copied to a temporary working folder keep their `.1.cso`/`.2.cso` part markers and travel together; the cloud-failure path cleans up its temporary folder.
- **One bad file no longer aborts a batch** — files that vanish, lock, or cannot be read mid-scan or mid-test are reported individually and the remaining files continue.
- **Fewer false bug reports** — locked files, full disks, offline networks, permission problems, and update-check failures log at Information level; retries only retry genuinely transient errors.
- **Safer cleanup and shutdown** — temporary-folder cleanup only removes app-created GUID work folders older than six hours, the Exit confirmation can't be bypassed or shown twice, and the explorer is never disposed while a copy-out is still reading from it.
- **Cross-platform correctness** — path comparisons respect case-sensitive file systems, 7-Zip arguments are escaped per platform, and compressed outputs use a size-aware free-space estimate instead of the uncompressed size.
- **A pre-release review added more fixes** — explorer extractions run off the UI thread again, drag-and-drop files are kept until the drop target finishes copying, failed extractions clean up their temp folders, split CISO sets keep their original extension casing when moved, and cross-mount-point moves on Linux/macOS check destination free space. The Serilog pipeline is now configured before Avalonia starts (capturing platform-startup diagnostics), Avalonia framework warnings are no longer auto-reported, and the log viewer can no longer feed failures back into the logging pipeline.

### Upgrading
Download the archive that matches your platform and architecture. No action is required for existing Windows users — the portable layout and all existing formats keep working unchanged. Version 3.0.0 adds the Serilog logging pipeline, per-file selection lists, ZAR/CSO output, and CHD support (the **CHDSharp** 1.4.3 package, pure managed code), and changes the framework the window is drawn with (WPF → Avalonia). CUE/BIN images are no longer supported — convert them to ISO with another tool first. The workflow changed slightly: previously everything found in the folder was processed automatically; now tick the files you want (all files are ticked by default).

---

See the complete history in [docs/Release-Notes.md](docs/Release-Notes.md).
