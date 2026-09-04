# Usage Guide

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [**Usage Guide**](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

The main window is organized into three views, selectable from the navigation buttons at the top:

| View | Purpose |
|:---|:---|
| **Convert** | Batch-convert ISOs (and archives containing ISOs) into trimmed XISO files |
| **Test Integrity** | Validate the filesystem structure of ISO files and organize results |
| **Explorer** | Browse the contents of an Xbox ISO without extracting it |

Below the three views, a shared **status bar** shows a live log, progress bar, cancel button, statistics (total / success / fail / skipped / processing time), and per-drive read/write speed indicators.

---

## The Convert Tab

### 1. Select Folders

- **Input folder** — the folder containing your source files. Supported inputs: `.iso`, `.cue`/`.bin` pairs, `.zip`, `.7z`, `.rar`.
- **Output folder** — the folder where converted `.iso` (XISO) files are written.

> The system temporary folder (or a subfolder of it) cannot be selected as an input or output folder. The application refuses it deliberately to avoid recursive processing and cleanup conflicts.

Enable **Search Subfolders** to recurse into nested directories; every matching file found is queued.

### 2. Choose a Conversion Method

Three radio buttons select the engine:

| Option | Engine | Summary |
|:---|:---|:---|
| **extract-xiso** | External tool | Full repack; smallest output |
| **xdvdfs** | External tool (Rust) | Full repack; smallest output, modern implementation |
| **Built-in logic** | Native C# trim | Fast trim preserving the original layout; highest safety |

A detailed comparison is available on the [Conversion Methods](Conversion-Methods.md) page.

### 3. Options

| Option | Behavior when enabled |
|:---|:---|
| **Skip $SystemUpdate** | Excludes the `$SystemUpdate` folder from the output for extra space savings (~100–300 MB). Supported by the built-in engine and `extract-xiso`. |
| **Delete Originals** | Replaces each input file with its converted version. Deletion happens **only after** the output has been produced and verified. |
| **Check Output Integrity** | Runs the XDVDFS validation on each newly created XISO before reporting success. |
| **Search Subfolders** | Includes ISOs found in subdirectories of the input folder. |

### 4. Start, Monitor, Cancel

- Click **Start Conversion** to begin the batch.
- The progress bar shows per-file and overall progress; the log pane records every action with timestamps.
- The statistics panel updates live: **Total**, **Success**, **Failed**, **Skipped**, **Processing Time**, plus **read/write speed** with a drive-letter indicator for the currently active disk.
- Click **Cancel** at any time. The current file operation is cancelled as soon as possible; already-converted files remain valid.

### 5. What Happens to Each File

The orchestrator decides per input file:

1. **`.iso`** — converted directly with the selected method.
2. **`.cue`/`.bin`** — converted to ISO with the bundled `bchunk` tool, then converted normally.
3. **`.zip` / `.7z` / `.rar`** — extracted to a temporary folder (with automatic drive fallback if the temp drive is short on space), the ISO inside is converted, then temporaries are cleaned up.
   - Password-protected/encrypted archives are detected and **skipped with a clear message** instead of a cryptic failure.
4. At the end, a **summary** is logged (files processed, succeeded, failed, skipped) and the operation is finalized.

Files stored in cloud-sync folders (OneDrive, etc.) that are not hydrated locally are detected and retried automatically with exponential backoff while the cloud provider downloads them.

---

## The Test Integrity Tab

Use this view to validate ISO images without converting them.

### Options

| Option | Behavior when enabled |
|:---|:---|
| **Move Passed Files** | Moves ISOs that pass validation into a `Passed` subfolder |
| **Move Failed Files** | Moves ISOs that fail validation into a `Failed` subfolder |
| **Search Subfolders** | Recurses into subdirectories of the input folder |
| **Perform Deep Scan** | Reads every sector of the image sequentially to detect physical corruption / bad sectors (slower, but thorough) |

### Workflow

1. Select the **input folder** containing the ISOs to test.
2. Choose any of the options above.
3. Click **Start Test**.
4. Review the log: each file is reported as passed or failed, with the reason for failure where applicable.

> File moves performed by the test view use the same retry logic as conversion: transient locks (antivirus scans, cloud hydration, network hiccups) are retried with exponential backoff before being reported as failures.

---

## The Explorer Tab

The built-in XISO Explorer lets you inspect the contents of an Xbox ISO without extraction. See the dedicated [XISO Explorer](XISO-Explorer.md) page for the full walkthrough.

---

## Statistics Panel

The shared statistics panel at the bottom of the window is active during conversion and testing:

| Field | Meaning |
|:---|:---|
| **Total Files** | Number of files discovered in the batch |
| **Success** | Files processed successfully |
| **Failed** | Files that could not be processed (reason logged) |
| **Skipped** | Files intentionally skipped (e.g., unsupported, encrypted archives, insufficient disk space) |
| **Processing Time** | Elapsed time for the current/last batch |
| **Read Speed / Write Speed** | Live throughput of the drives involved, with the drive letter as an indicator |

The **memory indicator** shows the current process memory usage, which is useful when processing very large batches of large images.

---

## Logging

Every operation is written to the in-application log pane. The log includes:

- File-by-file decisions (which engine, source, destination)
- Warnings for skipped files with clear reasons (disk space, FAT32 size limit, permissions, locked files)
- Retry attempts for transient failures (file locks, network errors)
- A final batch summary

When an unexpected error occurs, the application can send an automatic bug report (exception message and stack trace) to the developer. **Environmental errors — such as full disks or network failures — are not reported as bugs**; they are shown to you with actionable messages instead.

---

## Practical Tips

- **Test one file first** when trying a new conversion method, then run the whole batch.
- **Keep 10–15% free space** on the output drive; the application pre-checks free space but headroom avoids edge cases.
- **Use the built-in logic** if you want to preserve the original layout (useful for debugging or archival fidelity); use **xdvdfs** or **extract-xiso** for the smallest files.
- **Antivirus software** can temporarily lock newly created files. The application waits and retries automatically, but real-time scanning of large ISO folders slows batches down — consider adding your working folders to the exclusion list.
- **Network shares** are fully supported (UNC paths and mapped drives). Wired connections reduce retry-related slowdowns.
- **FAT32 output drives** cannot hold files larger than 4 GB; the application detects this in advance and skips those files with a clear message. Use NTFS or exFAT for modern game images.
