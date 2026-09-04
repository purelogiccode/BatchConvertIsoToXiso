# Building from Source

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [Repository](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [**Building from Source**](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

## Prerequisites

| Requirement | Notes |
|:---|:---|
| **Windows 10/11** (x64 or ARM64) | WPF application; Windows-only |
| **.NET 10 SDK** | Matches the `net10.0-windows` target; `global.json` pins the required SDK version |
| **Git** | To clone the repository |

Verify your SDK:

```bash
dotnet --version
```

## Cloning

```bash
git clone https://github.com/purelogiccode/BatchConvertIsoToXiso.git
cd BatchConvertIsoToXiso
```

## Building

Build the full solution (application + test project):

```bash
dotnet build CSharp_BatchConvertIsoToXiso.sln
```

Or build and run the application directly:

```bash
dotnet run --project BatchConvertIsoToXiso
```

### Bundled Native Tools

The application project bundles Windows executables that are copied to the output directory on every build:

- `extract-xiso.exe`, `xdvdfs.exe` — external conversion engines
- `bchunk.exe` — CUE/BIN to ISO conversion
- `7za.exe`, `7za_arm64.exe` — 7-Zip CLI fallback for archive extraction

These are committed to the repository, so no extra download steps are needed.

## Running the Tests

The test suite uses xUnit with Moq:

```bash
dotnet test CSharp_BatchConvertIsoToXiso.sln
```

The suite covers models, services (orchestrator, extractor, movers, path helpers, update checker, and more), and the XISO binary layer (`XDVDFS`, `VolumeDescriptor`, `FileEntry`, `Utils`).

## Code Analysis

Both projects enforce analyzer rules (**Meziantou.Analyzer**, **Roslynator**) as build warnings. Treat new warnings in changed code as errors in practice — keep the build clean.

## Publishing a Release Build

Example for a framework-dependent x64 publish:

```bash
dotnet publish BatchConvertIsoToXiso -c Release -r win-x64 --self-contained false
```

For a self-contained single-folder build (no .NET runtime requirement for end users):

```bash
dotnet publish BatchConvertIsoToXiso -c Release -r win-x64 --self-contained true
```

Use `-r win-arm64` for ARM64 builds. Ensure the bundled executables end up next to the published `BatchConvertIsoToXiso.exe`.

## Project Notes

- **Target framework:** `net10.0-windows` with `<UseWPF>true</UseWPF>`.
- **Nullable + implicit usings** are enabled.
- The `References/` folder (vendored sources such as the xdvdfs Rust workspace, if present) is excluded from compilation.
- Version numbers are maintained in `BatchConvertIsoToXiso.csproj` (`AssemblyVersion` / `FileVersion`); the update checker compares against GitHub release tags.
