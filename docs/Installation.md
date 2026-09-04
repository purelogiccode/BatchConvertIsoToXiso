# Installation

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [**Home**](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [**Installation**](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

## System Requirements

| Component | Minimum Requirement |
|:---|:---|
| Operating System | Windows 10 (version 1809) or later / Windows 11 |
| Runtime | [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Architecture | x64 (64-bit) or ARM64 |
| RAM | 4 GB recommended |
| Storage | Varies based on ISO collection size |

### Architecture Notes

| Architecture | Supported Conversion Methods |
|:---|:---|
| **x64 (64-bit)** | All three methods: `extract-xiso`, `xdvdfs`, and the built-in modified Deterous logic |
| **ARM64** | The built-in modified Deterous logic is fully supported; the external-tool methods may also work |

---

## Installing the Application

The application is **fully portable** — there is no installer and no registry footprint.

1. Download the latest release from the [Releases](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases) page.
2. Extract the ZIP file to any folder on a local drive (for example `C:\Tools\BatchConvertIsoToXiso`).
3. Run `BatchConvertIsoToXiso.exe`.

> **Tip:** Avoid placing the application inside `C:\Program Files` or other protected folders. Writing to protected locations requires elevation and can prevent the application from replacing originals, moving files, or writing its logs. See [Troubleshooting — Access Denied](Troubleshooting-and-FAQ.md#access-to-the-path-is-denied).

---

## Deployed Files

After extraction, the application folder contains:

| File | Purpose |
|:---|:---|
| `BatchConvertIsoToXiso.exe` | The main application |
| `extract-xiso.exe` | External conversion tool (XboxDev) used by the *extract-xiso* method |
| `xdvdfs.exe` | External conversion tool (antangelo, Rust) used by the *xdvdfs* method |
| `bchunk.exe` | CUE/BIN to ISO converter used when `.cue`/`.bin` pairs are selected |
| `7za.exe` / `7za_arm64.exe` | 7-Zip CLI fallback used for complex or password-protected `.7z` archives |

Do not delete or rename these executables — the corresponding features will fail if they are missing.

---

## .NET Runtime

The application targets **.NET 10.0** (Windows Desktop). If it fails to start with a message about a missing runtime:

1. Download and install the **.NET 10.0 Desktop Runtime** for your architecture from <https://dotnet.microsoft.com/download/dotnet/10.0>.
2. Re-launch the application.

Alternatively, some releases may be published as self-contained builds that bundle the runtime — check the release notes on the [Releases](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases) page.

---

## First Run

On first start the application:

1. **Cleans orphaned temporary folders** left behind by previous sessions or crashes (see [Safety & Reliability](#safety-and-reliability) below).
2. Sends an **anonymous usage statistic** ping and, when enabled by the user, checks for a **newer version** on GitHub. If a new release exists, the application offers to open the download page. This can be declined; no personal data is collected.
3. Opens the main window with the **Convert** tab active.

---

## Upgrading

1. Close any running instance of the application.
2. Download the new release ZIP.
3. Replace the contents of your existing application folder with the extracted files (or extract to a fresh folder).
4. Your settings are not stored in the application folder, so replacing the folder is always safe.

---

## Uninstalling

Simply delete the application folder. The application is portable and does not write to the registry or system folders.

---

## Safety and Reliability

The installation is designed to be safe for long batch jobs:

- **Atomic operations** — converted files are verified before originals are deleted (when *Replace Originals* is enabled).
- **Automatic cleanup** — orphaned temporary files from interrupted jobs are removed at startup.
- **Fallback temp drives** — when the system temp drive lacks space for archive extraction, alternative local drives are used automatically.
- **Network resilience** — UNC paths and mapped drives are supported with automatic retry logic for transient failures.
- **Cloud-aware** — files stored in OneDrive or similar sync folders are handled with exponential-backoff retries while they hydrate.

See the [Usage Guide](Usage-Guide.md) and [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) for details.
