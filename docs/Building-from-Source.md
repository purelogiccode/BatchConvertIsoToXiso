# Building from Source

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [**Building from Source**](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [Release Notes](Release-Notes.md) |

---

## Prerequisites

| Requirement | Notes |
|:---|:---|
| **Windows 10/11, Linux, or macOS** (x64 or ARM64) | Avalonia application; builds and runs on all three platforms |
| **.NET 10 SDK** | Matches the `net10.0` target; `global.json` pins the required SDK version |
| **Git** | To clone the repository |

Verify your SDK:

```bash
dotnet --version
```

## Cloning

```bash
git clone https://github.com/purelogiccode/XboxIsoStudio.git
cd XboxIsoStudio
```

## Building

Build the full solution (application + test project):

```bash
dotnet build CSharp_XboxIsoStudio.sln
```

Or build and run the application directly:

```bash
dotnet run --project XboxIsoStudio
```

### Bundled Helper Tools

XISO conversion is performed by the `XISOSharp` NuGet package and CHD encoding by the `CHDSharp` NuGet package — no conversion binaries are bundled. The application project bundles Windows helper executables that are copied to the output directory on Windows builds only:

- `7za.exe`, `7za_arm64.exe` — optional 7-Zip CLI fallback for archive extraction

These are committed to the repository, so no extra download steps are needed. On Linux and macOS the fallback uses the system `7z` command from `PATH` (install it with your package manager, e.g. `sudo apt install 7zip` or `brew install sevenzip`); SharpCompress handles `.zip`/`.rar`/most `.7z` archives without it.

## Running the Tests

The test suite uses xUnit with Moq:

```bash
dotnet test CSharp_XboxIsoStudio.sln
```

The suite covers models, services (orchestrator, XISO conversion, integrity, extractor, movers, path helpers, update checker, and more).

## Code Analysis

Both projects enforce analyzer rules (**Meziantou.Analyzer**, **Roslynator**) as build warnings. Treat new warnings in changed code as errors in practice — keep the build clean.

## Publishing a Release Build

Example for a framework-dependent x64 publish (single file, with the native Avalonia/Skia
libraries embedded and extracted to a temporary folder on first run):

```bash
dotnet publish XboxIsoStudio -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

For a self-contained build (no .NET runtime requirement for end users):

```bash
dotnet publish XboxIsoStudio -c Release -r win-x64 --self-contained true
```

Supported runtime identifiers:

| Platform | RIDs |
|:---|:---|
| Windows | `win-x64`, `win-arm64` |
| Linux | `linux-x64`, `linux-arm64` |
| macOS | `osx-x64`, `osx-arm64` |

All RIDs can be cross-published from any OS (the native Avalonia/Skia assets come from NuGet). After extracting a Linux/macOS build, mark the executable runnable with `chmod +x XboxIsoStudio`.

## Project Notes

- **Target framework:** `net10.0` with **Avalonia** (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Controls.DataGrid`).
- **Nullable + implicit usings** are enabled.
- The `References/` folder (vendored sources such as the xdvdfs Rust workspace, if present) is excluded from compilation.
- Version numbers are maintained in `XboxIsoStudio.csproj` (`AssemblyVersion` / `FileVersion`); the update checker compares against GitHub release tags.
- Windows-only features degrade gracefully on Linux/macOS: the disk read/write speed monitor shows `N/A`, and the 7-Zip fallback uses the system `7z`.
