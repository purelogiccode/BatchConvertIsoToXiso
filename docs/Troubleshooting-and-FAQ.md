# Troubleshooting & FAQ

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [**Troubleshooting & FAQ**](Troubleshooting-and-FAQ.md) | |

---

## Common Messages and How to Resolve Them

### Not enough disk space

**Message pattern:** *"Not enough disk space ... Required: ... Available: ..."*

The output drive does not have room for the converted file (with a safety margin).

- Free up space on the output drive, or
- Select a different output folder on another drive.

The application checks the output drive **before** converting, so a full disk skips the file with this message instead of failing halfway and leaving a corrupt partial file. If a write does still hit a full disk (for example, another program filled the drive concurrently), any partial output file is deleted automatically.

### File is too large for FAT32

**Message pattern:** *"parameter is incorrect"* on move, or a FAT32 limit message.

FAT32 cannot store files larger than 4 GB. Modern game images routinely exceed this.

- Use an **NTFS** or **exFAT** drive as the output destination.

### Access to the path is denied

The output folder (or the application folder itself) does not grant write permission.

- Do **not** run the application from, or write output to, protected folders such as `C:\Program Files`.
- Pick a normal user-writable folder (for example `D:\Games\Out`).
- If you must write to a protected location, adjust the folder's permissions — running the application as administrator is not required for normal use.

### File locked / being used by another process

Another program (most commonly antivirus real-time scanning, a cloud-sync client, or an indexer) temporarily holds the file.

- The application **waits and retries automatically** with exponential backoff (up to six attempts) before giving up.
- If the message persists, add your working folders to your antivirus exclusion list or close the program that holds the file.
- Files created moments ago are especially prone to this while the antivirus scans them; the retry logic exists precisely for this case.

### Network errors (UNC paths and mapped drives)

Network paths are fully supported. Transient network failures are retried automatically with exponential backoff.

- Use wired connections for large batches.
- Ensure the share stays reachable (no scheduled disconnects/sleep) during the run.

### Cloud files (OneDrive / Dropbox)

Files stored in cloud-sync folders may not be physically present on disk (online-only).

- The application detects this and retries with exponential backoff while the provider hydrates the file.
- For very large batches, right-click the folder in Explorer and choose **Always keep on this device** first.

### Encrypted or password-protected archives

**Message pattern:** an encrypted-archive notice; the file is skipped.

The archive cannot be extracted without the password.

- Extract the archive manually with 7-Zip/WinRAR, then run the application on the extracted ISO.

### Not a valid Xbox ISO image

The file was read, but no XDVDFS volume descriptor with the `MICROSOFT*XBOX*MEDIA` signature was found at any known partition offset.

- Verify the file is an Xbox or Xbox 360 game image (not a PC/DVD/Blu-ray ISO).
- Re-dump or re-download the image if it came from an unreliable source; a truncated dump fails validation.
- Test the image on the **Test Integrity** tab for a more detailed diagnosis.

### Font / rendering error at startup (Wine, Steam Deck, old Windows)

**Message pattern:** a startup error mentioning fonts or rendering.

This happens when required system fonts are missing — commonly under Wine/Proton (Steam Deck, Linux) or stripped-down Windows installs.

- Windows: run `sfc /scannow` to repair system files; ensure *Segoe UI* and *Arial* are installed.
- Linux/Steam Deck: install core fonts via `winetricks corefonts` and update your Wine/Proton version.

### The output file was not found although the tool reported success

This can happen when antivirus software quarantines the freshly created file. The application automatically searches the working and input directories and, if the file is found elsewhere, moves it to the expected output path; otherwise it reports the failure with an antivirus hint. Check your antivirus quarantine list.

---

## Frequently Asked Questions

**Which conversion method should I use?**
See [Conversion Methods](Conversion-Methods.md) — in short: built-in logic for safety and preserved layout; `xdvdfs` or `extract-xiso` for the smallest files.

**Why is the built-in method's output larger than the repack tools?**
It preserves the original gaps between files exactly as laid out on the disc. The repack tools reposition files to remove those gaps.

**Does the tool modify my source files?**
Only when **Delete Originals** (Replace Originals) is enabled — and even then, originals are removed only after the converted file has been produced and verified.

**Are Xbox 360 images supported?**
The XDVDFS parsing supports Xbox and Xbox 360 images; conversion trims the game partition of Redump-style dumps.

**Where are temporary files stored?**
In the system temp folder, in dedicated subfolders. They are cleaned automatically after each file and at startup (orphaned leftovers from crashes are removed too). If the temp drive lacks space, other local drives are used as fallback.

**Does the application collect my data?**
It sends an anonymous usage ping and, on unexpected internal errors, a bug report containing the exception details. Environmental errors (disk space, network) are never reported. No personal data or file contents are collected.

**How do I report a bug?**
Use the in-app **Report Bug** option from the About window, or open an issue at <https://github.com/purelogiccode/BatchConvertIsoToXiso/issues>. Include the relevant lines from the log pane.

**Where do I download new versions?**
From the [Releases](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases) page. The application checks for updates automatically and offers to open the page when a new version exists.

**Is there an installer?**
No. The application is portable — extract and run (see [Installation](Installation.md)).

**Can I run multiple instances at once?**
Not recommended: parallel instances compete for disk bandwidth and the replace-originals workflow becomes risky if they share folders.
