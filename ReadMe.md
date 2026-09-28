# Batch ISO to XISO Converter

[![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)](https://www.microsoft.com/windows)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6.svg?logo=windows&logoColor=white)](https://www.microsoft.com/windows)
[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64%20%7C%20ARM64-blue)](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE.txt)
[![GitHub release](https://img.shields.io/github/v/release/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases)
[![GitHub release date](https://img.shields.io/github/release-date/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases)
[![Downloads](https://img.shields.io/github/downloads/purelogiccode/BatchConvertIsoToXiso/total)](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases)
[![GitHub stars](https://img.shields.io/github/stars/purelogiccode/BatchConvertIsoToXiso?style=social)](https://github.com/purelogiccode/BatchConvertIsoToXiso/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/purelogiccode/BatchConvertIsoToXiso?style=social)](https://github.com/purelogiccode/BatchConvertIsoToXiso/forks)
[![GitHub issues](https://img.shields.io/github/issues/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso/issues)
[![GitHub last commit](https://img.shields.io/github/last-commit/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso/commits/master)
[![Repo size](https://img.shields.io/github/repo-size/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso)
[![Top language](https://img.shields.io/github/languages/top/purelogiccode/BatchConvertIsoToXiso)](https://github.com/purelogiccode/BatchConvertIsoToXiso)
[![CI](https://github.com/purelogiccode/BatchConvertIsoToXiso/actions/workflows/ci.yml/badge.svg)](https://github.com/purelogiccode/BatchConvertIsoToXiso/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-purelogiccode.github.io-blue)](https://purelogiccode.github.io/BatchConvertIsoToXiso/)
[![Wiki](https://img.shields.io/badge/wiki-GitHub-181717?logo=github)](https://github.com/purelogiccode/BatchConvertIsoToXiso/wiki)
[![Powered by XISOSharp](https://img.shields.io/badge/Powered%20by-XISOSharp-8A2BE2.svg)](https://github.com/purelogiccode/XISOSharp)
[![Code analyzers](https://img.shields.io/badge/analyzers-Meziantou%20%7C%20Roslynator-blueviolet)](docs/Architecture.md)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](docs/Repository.md#contributing)

A high-performance Windows WPF utility for the Xbox preservation and emulation community. Convert, verify, and explore Xbox and Xbox 360 images (ISO, CSO, and ZAR), powered by the XISOSharp library.

---

## 📋 Table of Contents

- [Overview](#overview)
- [What's New](#whats-new)
- [Screenshots](#screenshots)
- [Key Features](#key-features)
- [Installation](#installation)
- [Usage](#usage)
- [Supported Formats](#supported-formats)
- [Architecture](#architecture)
- [System Requirements](#system-requirements)
- [Safety & Reliability](#safety--reliability)
- [Acknowledgements](#acknowledgements)
- [License](#license)

---

## Overview

**Batch ISO to XISO Converter** streamlines the process of converting standard Xbox and Xbox 360 ISOs into the optimized, trimmed **XISO** format. All encoding and decoding is delegated to the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library, which repacks the XDVDFS game partition, delivering superior performance and modern features like real-time disk write monitoring.

Whether you're managing a large collection of Xbox game backups or verifying the integrity of your dumps, this application provides a user-friendly interface with powerful batch processing capabilities.

---

## What's New

### v2.9.0 — structured logging, file selection & output formats

- **Serilog pipeline** — all logging now runs through [Serilog](https://serilog.net/): the on-screen viewer, a rolling daily log file (`%LocalAppData%\BatchConvertIsoToXiso\logs`), and automatic forwarding of every Warning-or-higher event to the bug report API.
- **Complete bug reports** — every report includes Environment Details (app name/version, OS and Windows version, architecture, bitness, processor count, base directory, temp path), Error Details, and Exception Details (type, message, source, stack trace, including nested exceptions).
- **Quieter and more reliable** — expected user/environmental errors log at Information level so they never generate spurious reports, and previously silent `catch` blocks now log at an appropriate level.
- **Selectable file lists** — after choosing an input folder, the Convert and Test views list every supported file with a **Select** checkbox, file name, and size. Tick the files you want and click **Start**; only ticked files are processed.
- **Select All / Deselect All** toggle the whole list in one click, and **Search Subfolders** rescans the list immediately.
- **Always in sync** — the list refreshes after each batch (deleted originals, files moved to `_success`/`_failed`) and uses the same extension filters as the conversion engine.
- **Compressed output formats** — the Convert tab now produces **ZAR** (`.zar`, ZArchive/zstd, loadable in Xenia canary) and **CSO** (`.cso`, CISO v2/LZ4, byte-identical to `xdvdfs compress`) in addition to optimized XISO; pick the format in the Options panel.
- **ISO/XISO only** — the bundled `bchunk.exe` and `.cue`/`.bin` input support were removed; supported inputs are `.iso` (Redump full-disc images or optimized XISO files) and archives (`.zip`, `.7z`, `.rar`).

### v2.8.0 — powered entirely by XISOSharp

- **One conversion engine** — all ISO → XISO conversion runs in-process through [XISOSharp](https://github.com/purelogiccode/XISOSharp); the bundled `extract-xiso.exe`, `xdvdfs.exe`, and the native writer were removed (smaller download, no external processes).
- **Integrity testing and XISO Explorer** are now backed by the same library (`AuditXiso` and `XisoExplorer`).
- **Fixes** — the main window now appears immediately (startup drive probing no longer blocks it); cloud/OneDrive sources keep their original output name; already-optimized files no longer delete an existing output; **Replace Originals** no longer deletes originals (including archives) when a conversion was skipped; device I/O errors stop the batch with a drive-health message.
- **Hardening** — free-space/FAT32 pre-checks, partial-output cleanup, cross-volume move fallback, locked-archive retries.
- **Bundles** — release ZIPs include `LICENSE.txt`, `ReadMe.md`, and `WhatsNew.md`.

Read the full [What's New](WhatsNew.md) or browse the [Release Notes](docs/Release-Notes.md).

---

## Screenshots

![Convert Tab](screenshot.png)
*Batch conversion interface with real-time progress monitoring*

![Test Tab](screenshot2.png)
*ISO integrity testing with batch organization*

![Explorer Tab](screenshot3.png)
*XISO file browser*

---

## Key Features

### 🔄 Batch Conversion
- **XISOSharp Engine**: All conversion is performed in-process by the [XISOSharp](https://github.com/purelogiccode/XISOSharp) library — no external conversion binaries required
- **Output Formats**: Optimized **XISO** (`.iso`), **ZAR** (`.zar`, ZArchive/zstd — Xenia canary loads it directly), or **CSO** (`.cso`, CISO v2/LZ4) — selected per batch on the Convert tab
- **Smart Processing**: Removes video partitions and padding, converting Redump ISOs to playable XISO format
- **Archive Support**: Process `.zip`, `.7z`, and `.rar` files directly with high-performance extraction via SharpCompress, with automatic 7-Zip CLI fallback for complex `.7z` archives
- **Encrypted Archive Detection**: Automatically detects password-protected archives and provides clear guidance for manual extraction, preventing cryptic extraction failures
- **System Update Removal**: Option to skip the `$SystemUpdate` folder for additional space savings
- **Skip Already Optimized**: Images that already carry the optimized XISO tag are detected and skipped

### ✅ Integrity Testing
- **Structural Validation**: Deep traversal of the XDVDFS file tree (ISO and CSO) or the ZAR archive tree to ensure file system validity
- **Deep Surface Scan**: Optional sequential read of the entire image — every sector for ISO/CSO, every decompressed block for ZAR — to detect physical data corruption or bad sectors
- **All Output Formats**: Test the optimized `.iso`, `.cso`, and `.zar` files this app produces (split `.1.cso` part sets are recognized too)
- **Batch Organization**: Automatically organize "Passed" or "Failed" images into dedicated subfolders

### 🔍 Image Explorer
- **Native Browsing**: Open any Xbox ISO, CSO, or ZAR to browse files and directories without extraction
- **Metadata View**: View file sizes, attributes, and directory structures directly in the UI
- **Double-Click to Open**: Open files directly from the image with their default associated applications
- **Drag & Drop Extraction**: Drag files out of the explorer to extract them to any folder (Windows Explorer, Desktop, etc.)

### 📊 Advanced Monitoring
- **Real-time Statistics**: Track success/fail counts, elapsed time, and processed files
- **Disk Monitor**: Live monitoring of write speeds and drive activity to identify hardware bottlenecks
- **Cloud-Aware**: Automatic detection and handling of cloud-stored files (e.g., OneDrive)
- **Structured Logging**: [Serilog](https://serilog.net/) pipeline with an on-screen viewer, a rolling daily log file, and automatic bug-report forwarding for Warning-or-higher events

---

## Installation

### Prerequisites
- **Operating System**: Windows 10 (version 1809) or later / Windows 11
- **Runtime**: [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Architecture Support**:
    - **x64 (64-bit)**: Fully supported.
    - **ARM64**: Fully supported.

### Steps
1. Download the latest release from the [Releases](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases) page
2. Extract the ZIP file to your desired location
3. Run `BatchConvertIsoToXiso.exe`

No installation required – the application is fully portable.

---

## Usage

### Converting ISOs
1. Launch the application
2. Click **"Select Input Folder"** and choose the directory containing your ISO files
3. Click **"Select Output Folder"** to specify where converted files will be saved
4. Configure options:
    - **Remove System Update**: Skip `$SystemUpdate` folder to save space
    - **Replace Originals**: Replace input files with converted versions
    - **Test After Conversion**: Automatically verify converted ISOs
    - **Check Output Integrity**: Structurally validate each converted XISO before reporting success
5. Click **"Convert"** to start the batch process

### Testing Image Integrity
1. Switch to the **"Test Integrity"** tab
2. Select your input folder containing `.iso`, `.cso`, or `.zar` files
3. Enable **"Move Passed Files"** and/or **"Move Failed Files"** to organize results
4. Click **"Start Integrity Test"** to begin validation

### Exploring Image Contents
1. Switch to the **"Explorer"** tab
2. Click **"Browse..."** and select an Xbox `.iso`, `.cso`, or `.zar` file
3. Browse the file tree to view contents without extraction
4. **Open Files**: Double-click any file to open it with its default application
5. **Extract Files**: Drag and drop files from the explorer to Windows Explorer, Desktop, or any folder to extract them

---

## Conversion Engine

All conversion is performed by the **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** library:

- **Approach**: Repack — reads the XDVDFS game partition and writes a new optimized XISO with files packed tightly together
- **Output Size**: Smallest — video partition, padding, and inter-file gaps are removed
- **No External Tool**: The engine is a managed library bundled with the application
- **Redump-Aware**: Automatically detects XGD1/XGD2/XGD3 and hybrid partition offsets
- **Already Optimized**: Images carrying the optimized tag are skipped automatically

### What Gets Removed

- ✅ **Video Partition** (DVD movie/demonstration) - **~7-387 MB removed**
- ✅ **End Padding** (empty sectors after last file) - **Variable**
- ✅ **System Update** (optional) - **~100-300 MB removed**

### Visual Comparison

```
Redump ISO (Original):
[Video Partition][XDVDFS: Header][Dir][File A][gap][File B][gap][File C][Padding]

XISOSharp Output:
[XDVDFS: Header][Dir][File A][File B][File C] (gaps removed, tightly packed)
                      ↑    ↑    ↑
                 Files repositioned for maximum compression
```

---

## Supported Formats

| Operation      | Supported Formats                              |
|:---------------|:-----------------------------------------------|
| **Conversion** | `.iso` (Redump or optimized XISO), `.zip`, `.7z`, `.rar` |
| **Output**     | `.iso` (XISO), `.zar` (ZAR), `.cso` (CSO)      |
| **Testing**    | `.iso`, `.cso` (CISO, incl. split `.1.cso` part sets), `.zar` (ZAR) |
| **Explorer**   | `.iso`, `.cso` (CISO), `.zar` (ZAR)            |

---

## Architecture

The application follows modern software engineering principles with a clean, maintainable architecture based on **Dependency Injection** and **Service-Oriented Design**.

### Dependency Injection
Utilizes `Microsoft.Extensions.DependencyInjection` for comprehensive service management. All core logic is decoupled from the UI, enabling easier testing and modular updates.

### Logging
Logging runs through a single [Serilog](https://serilog.net/) pipeline with three sinks: the on-screen log viewer (`UiLogSink`), a rolling daily file log (`%LocalAppData%\BatchConvertIsoToXiso\logs`), and a bug-report sink (`BugReportSink`) that forwards every **Warning-or-higher** event to the Bug Report API. Reports include complete environment, error, and exception sections; expected user/environmental errors are logged at Information level so they never generate noise.

### Testing
A comprehensive [xUnit](https://xunit.net/) test suite (`BatchConvertIsoToXiso.Tests`) covers models, services, and image services with 340+ tests, using [Moq](https://github.com/devlooped/moq) for mocking.

### Technical Documentation
For a deep dive into the XDVDFS format, binary file structures, and the conversion algorithm, see the [XDVDFS Technical Documentation](docs/XDVDFS-Technical-Documentation.md). The full documentation (including installation, usage, troubleshooting, architecture, and [release notes](docs/Release-Notes.md)) lives in the [docs folder](docs/index.md) and doubles as the repository wiki. Highlights of the latest release are summarized in [What's New](WhatsNew.md).

---

## System Requirements

| Component    | Minimum Requirement                 |
|:-------------|:------------------------------------|
| OS           | Windows 10 (1809) or Windows 11     |
| .NET Runtime | .NET 10.0 Desktop Runtime           |
| Processor    | x64 or arm64 architecture           |
| RAM          | 4 GB recommended                    |
| Storage      | Varies based on ISO collection size |

---

## Safety & Reliability

- **Atomic Operations**: Converted files are verified before originals are deleted
- **Automatic Cleanup**: [`TempFolderCleanupHelper`](BatchConvertIsoToXiso/Services/TempFolderCleanupHelper.cs) removes orphaned temporary files on startup or after crashes
- **Fallback Temp Drives**: Automatically searches alternative local drives when the system temp drive lacks sufficient space for archive extraction
- **Robust Error Handling**: Comprehensive exception handling with [Serilog](https://serilog.net/) structured logging; every Warning-or-higher event is automatically forwarded to the bug report API with full environment and exception details
- **Network Resilience**: Full support for UNC paths and mapped network drives with automatic retry logic for transient network failures
- **Cloud-Aware Retry**: Automatic retries with exponential backoff for cloud-synced files (OneDrive, etc.)
- **Encrypted Archive Handling**: Gracefully detects password-protected and encrypted archives, providing clear user guidance instead of cryptic errors
- **Process Isolation**: The 7-Zip CLI fallback for archive extraction runs in an isolated process with cancellation support

---

## Acknowledgements

- **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** - XISO/XDVDFS reading, writing, and conversion library that powers all conversion, integrity testing, and exploration
- **[Serilog](https://serilog.net/)** - Structured logging pipeline (log viewer, rolling file log, and bug report sinks)
- **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** - High-performance archive extraction

---

## License

This project is licensed under the GNU General Public License v3.0 – see the [LICENSE.txt](LICENSE.txt) file for details.

---

<p>
  ⭐ <strong>If you find this tool useful, please give us a Star on GitHub!</strong> ⭐
</p>

<p>
  <a href="https://www.purelogiccode.com">Pure Logic Code</a>
</p>