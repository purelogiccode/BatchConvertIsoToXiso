# What's New in Batch ISO to XISO Converter

<!-- Keep this file focused on the newest release. Full history lives in docs/Release-Notes.md. -->

## Version 2.8.0

**Release date:** September 2026

Version 2.8.0 is the **XISOSharp release**: all XISO encoding, decoding, integrity testing, and exploration now run in-process through the [XISOSharp](https://github.com/purelogiccode/XISOSharp) library. The external conversion engines and the in-repo XDVDFS implementation have been removed, making the app smaller, simpler, and easier to maintain.

### Highlights

#### One conversion engine — XISOSharp
- All `.iso` files are converted in-process by `XisoReader.Rewrite` — no more `extract-xiso.exe` or `xdvdfs.exe` child processes.
- Redump/XGD1/XGD2/XGD3 images and hybrid partition offsets are detected automatically; video partitions, padding, and inter-file gaps are removed.
- Images that already carry the optimized XISO tag are detected and skipped.
- The **Conversion Method** selection was removed from the UI — there is nothing to choose.

#### Integrity testing rebuilt on XISOSharp
- `XisoIntegrityService` performs a deep structural audit of the entire XDVDFS directory tree (`XisoReader.AuditXiso`) and reports exactly which entries failed.
- The optional **deep surface scan** still reads every sector sequentially to catch physical media errors before the audit.

#### XISO Explorer migrated to XISOSharp
- The explorer now uses `XisoExplorer`/`ExplorerNode`, with the same features as before: browsing, double-click to open, and drag-and-drop extraction. Long paths and ordinal-ignore-case sorting are handled natively.

#### Smaller download
- `extract-xiso.exe` and `xdvdfs.exe` are no longer bundled. The release ZIP is roughly 3.6 MB smaller and contains only the helper tools (`bchunk.exe`, `7za.exe`, `7za_arm64.exe`).

### Bug fixes
- **The main window now appears immediately** — startup temp-folder cleanup probes every drive (`DriveInfo.IsReady`), which can block for ~20 seconds while an idle/spinning disk wakes up. Cleanup now runs *after* the window is shown and on a background thread, so the UI is never delayed (this also unblocks the pre-operation cleanup before a batch).
- **Cloud/OneDrive files keep their original name** — when a cloud placeholder is copied to a temporary working file, the converted XISO is now written with the original game's file name instead of the internal `iso_000000.iso` temporary name.
- **Skipped files no longer delete existing output** — an already-optimized input is skipped *before* the output folder is touched, so an existing converted file is never removed by a no-op run.
- **Replace Originals no longer loses data on skipped conversions** — originals (including archives and CUE/BIN pairs, and cloud-backed files) are deleted only when a converted file was actually produced; a skipped "already optimized" input keeps its originals.
- **Device I/O errors are treated as environmental** — hardware errors (`ERROR_IO_DEVICE`, localized equivalents) stop the batch with a drive-health message, are no longer misclassified as network errors, and are excluded from automatic bug reports.
- **Temp-folder cleanup can no longer mask errors** — a folder that stays locked after all retries logs a warning instead of throwing from `finally` blocks.
- **ARM64 CUE/BIN handling matches the docs** — `.cue`/`.bin` inputs are skipped with a clear message on ARM64 (the bundled `bchunk.exe` is x64-only); the ARM64 bundle no longer ships `bchunk.exe`.
- **Locked archives are retried** — archives held briefly by antivirus/download clients are waited for with exponential backoff instead of failing immediately.
- **Cross-volume moves fall back to copy + delete** — moving converted files between volumes no longer fails with "The parameter is incorrect".
- **Better access-denied, FAT32, and disk-space handling** — conversions pre-check free space and the FAT32 4 GB limit and clean up partial output on failure.

### Documentation
- All docs were reorganized into the [docs folder](docs/index.md) (repository wiki): Installation, Usage Guide, Conversion Methods, XISO Explorer, Architecture, Building from Source, Troubleshooting & FAQ, Repository, and the XDVDFS Technical Documentation.
- New [Release Notes](docs/Release-Notes.md) page with the full version history.
- The XDVDFS Technical Documentation now reflects that the format is handled by XISOSharp.

### Breaking changes
- The **Conversion Method** radio buttons were removed; conversion is always performed by XISOSharp.
- `extract-xiso.exe` and `xdvdfs.exe` are no longer shipped. If you scripted around them, use the XISOSharp library directly.
- The repository moved to <https://github.com/purelogiccode/BatchConvertIsoToXiso>; update feeds and shortcuts that point at the old `drpetersonfernandes` URL.

### Upgrading
No action is required. Converted XISOs remain compatible; existing optimized images are detected and skipped. The app still requires the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and remains fully portable.

---

See the complete history in [docs/Release-Notes.md](docs/Release-Notes.md).
