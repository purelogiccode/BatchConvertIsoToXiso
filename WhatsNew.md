# What's New in Xbox ISO Studio

<!-- Keep this file focused on the newest release. Full history lives in docs/Release-Notes.md. -->

## Version 2.10.0 (unreleased)

**Release date:** TBD

Version 2.10.0 is the **CHD release**: the [CHDSharp](https://github.com/purelogiccode/CHDSharp) library is now built in, so Xbox and Xbox 360 images can be converted to **CHD** (`.chd`, Compressed Hunks of Data), and Xbox DVD CHD files can be integrity-tested and explored without extraction.

### Highlights

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

### Upgrading
No action is required. CHD support adds the **CHDSharp** 1.4.3 package (pure managed code, no native dependencies); all existing formats keep working unchanged.

---

## Version 2.9.0

**Release date:** September 2026

Version 2.9.0 is the **logging & output formats release**: all logging runs through Serilog with automatic bug reporting, the Convert and Test views now show every supported file found in the selected folder, each with a checkbox, and the Convert tab can produce optimized **XISO**, compressed **ZAR**, or compressed **CSO** output.

### Highlights

#### Structured logging with automatic bug reports
- All logging now runs through [Serilog](https://serilog.net/): the on-screen log viewer, a rolling daily file log (`%LocalAppData%\XboxIsoStudio\logs`), and a bug-report sink that forwards every **Warning-or-higher** event to the Bug Report API.
- Reports include complete Environment, Error, and Exception sections (type, message, source, and stack trace, including nested exceptions); expected user/environmental errors are logged at Information level so they never generate noise.

#### Pick exactly which files to process
- After choosing an input folder, the Convert view lists every supported file (`.iso`, `.zip`, `.7z`, `.rar`) and the Test view lists every ISO, with a **Select** checkbox, file name, and size.
- Use **Select All** / **Deselect All** to toggle the whole list, then click **Start** — only ticked files are processed.
- **Search Subfolders** now rescans the list immediately when toggled, and the list refreshes automatically after each batch (for example after originals are deleted or tested files are moved to `_success`/`_failed`).

#### Choose the output format
- A new **Output Format** selector on the Convert tab produces **XISO** (default), **ZAR** (`.zar`, ZArchive/zstd — Xenia canary loads it directly), or **CSO** (`.cso`, CISO v2/LZ4, byte-identical to `xdvdfs compress`).
- **Skip $SystemUpdate**, **Delete Originals**, and integrity checking work for all three formats — for ZAR/CSO the integrity check validates the source image that gets packed.

#### Consistent, responsive lists
- The lists use the same dark terminal styling as the rest of the app and load in chunks, so folders with thousands of files stay responsive.
- The Convert and Test panels (folder pickers, options, and file list) now sit on the **left**, with the log viewer / XISO explorer on the **right** and a draggable splitter between them — the same layout used by the other BatchConvert tools.
- The **Explorer** tab shows the file picker at the top with the explorer list directly below it and uses the full window width — the log panel is hidden on this tab.
- The **Options** panel on both tabs is collapsible: click its header to hide the checkboxes and give the file list more room.
- File names are shown relative to the selected input folder (subfolders included), and sizes are formatted for readability.
- The engine applies the same extension filters as the lists, so the UI can never offer a file the converter cannot handle.

#### CUE/BIN support removed
- The bundled `bchunk.exe` and `.cue` input support were removed. The application now supports only `.iso` (Redump full-disc images) and already-optimized XISO files, plus archives (`.zip`, `.7z`, `.rar`). CUE/BIN images are no longer listed or converted.

### Upgrading
No action is required for ISO/XISO users. The workflow changed slightly: previously everything found in the folder was processed automatically; now tick the files you want (all files are ticked by default). CUE/BIN images are no longer supported — convert them to ISO with another tool first. The app still requires the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and remains fully portable.

---

See the complete history in [docs/Release-Notes.md](docs/Release-Notes.md).
