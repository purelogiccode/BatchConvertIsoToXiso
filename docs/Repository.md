# Repository

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [**Repository**](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | [**Release Notes**](Release-Notes.md) |

---

This page describes the repository itself: where things live, how releases are managed, and how to contribute.

- **Repository:** <https://github.com/purelogiccode/XboxIsoStudio>
- **Issues:** <https://github.com/purelogiccode/XboxIsoStudio/issues>
- **Releases:** <https://github.com/purelogiccode/XboxIsoStudio/releases>
- **Website:** <https://www.purelogiccode.com>
- **License:** [GNU GPL v3.0](https://github.com/purelogiccode/XboxIsoStudio/blob/master/LICENSE.txt)

---

## Repository Layout

```text
├── docs/                                  This documentation (repository wiki)
│   ├── index.md                           Home page
│   ├── Installation.md
│   ├── Usage-Guide.md
│   ├── Conversion-Methods.md
│   ├── XISO-Explorer.md
│   ├── Troubleshooting-and-FAQ.md
│   ├── Architecture.md
│   ├── XDVDFS-Technical-Documentation.md
│   ├── Building-from-Source.md
│   ├── Release-Notes.md                   Full version history
│   ├── Repository.md                      This page
│   └── _Sidebar.md                        Sidebar menu (GitHub wiki)
├── XboxIsoStudio/                 Main Avalonia application project
│   ├── Program.cs                         Entry point (Avalonia AppBuilder)
│   ├── App.axaml(.cs)                     Theme/styles + DI composition
│   ├── MainWindow.axaml(.cs)              Shell window
│   ├── MainWindow*.cs                     Partial classes for the main window
│   ├── AboutWindow.axaml(.cs)             About dialog
│   ├── Dialogs/                           Cross-platform message box dialog
│   ├── Interfaces/                        Service contracts
│   ├── Models/                            DTOs and enums
│   ├── Services/                          All business logic
│   │   ├── XisoSharpService.cs            XISO/ZAR/CSO conversion via the XISOSharp library
│   │   ├── ChdService.cs                  CHD conversion via the CHDSharp library
│   │   ├── XisoIntegrityService.cs        Integrity validation via XISOSharp / ZArchiveSharp / CHDSharp
│   │   ├── ImageExplorerFactory.cs        Explorer over ISO/CSO (XisoExplorer), ZAR (ZArchiveReader) or CHD (ChdImageExplorer)
│   │   └── ChdImageExplorer.cs            Explorer over CHD via CHDSharp + XISOSharp
│   ├── 7za.exe, 7za_arm64.exe             Bundled helper tools (Windows builds only)
│   └── XboxIsoStudio.csproj
├── XboxIsoStudio.Tests/           xUnit + Moq test project
├── CSharp_XboxIsoStudio.sln       Solution file
├── global.json                            Pins the .NET SDK version
├── ReadMe.md                              Repository front page
├── WhatsNew.md                            Highlights of the latest release
├── LICENSE.txt                            GNU GPL v3.0
└── screenshot*.png                        Screenshots used by the ReadMe
```

## Branching and Releases

- The primary branch is **`master`**.
- **Releases** are tagged on GitHub and published on the [Releases](https://github.com/purelogiccode/XboxIsoStudio/releases) page with ready-to-run archives for Windows, Linux, and macOS (x64 and ARM64).
- **Versioning:** `MAJOR.MINOR.PATCH` (currently 3.0.0, September 2026). Releases are tagged `release_MAJOR.MINOR.PATCH` (for example `release_3.0.0`), and release archives follow the `release_MAJOR.MINOR.PATCH_<rid>.zip` naming used by previous releases. The application's update checker extracts the numeric version from the latest release tag, so keep the `MAJOR.MINOR.PATCH` part intact.

## Contributing

Contributions are welcome!

### Reporting Bugs

1. Check [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) first — many reported issues are environmental (disk space, permissions, antivirus locks) and already handled with clear messages.
2. Search existing [issues](https://github.com/purelogiccode/XboxIsoStudio/issues) to avoid duplicates.
3. Open a new issue including:
   - Application version (visible in the About window)
   - Operating system, version, and architecture (x64/ARM64)
   - The input format used (`.iso`, `.cso`, `.zar`, `.chd`, or archive)
   - The relevant lines from the in-application log

### Submitting Changes

1. Fork the repository and create a feature branch from `master`.
2. Follow the existing code style — the build enforces **Meziantou** and **Roslynator** analyzer rules as warnings; keep the build clean.
3. Add or update tests in `XboxIsoStudio.Tests` for behavioral changes.
4. Verify with:

   ```bash
   dotnet build CSharp_XboxIsoStudio.sln
   dotnet test CSharp_XboxIsoStudio.sln
   ```

5. Open a pull request describing the motivation and the change.

### Coding Conventions

- Services live behind interfaces in `Interfaces/` and are registered in `App.ConfigureServices` (dependency injection — no manual `new` in UI code).
- UI code (partial `MainWindow` classes) contains no business logic; it orchestrates services and updates the UI.
- User-facing error messages should be actionable; environmental errors (disk space, network) must not be sent as automatic bug reports.

## Documentation

The `docs/` folder doubles as the repository wiki:

- On GitHub, `docs/_Sidebar.md` provides the side menu when the pages are imported into the repository wiki (GitHub renders `_Sidebar.md` next to every wiki page automatically).
- Every page also embeds the same navigation table at the top so the documentation is fully navigable when browsed inside the repository.

When adding features, please update the relevant documentation pages in the same pull request.

## Continuous Integration

| Workflow | Trigger | Purpose |
|:---|:---|:---|
| **CI** (`.github/workflows/ci.yml`) | Push / PR to `master` | Restores, builds in Release, and runs the xUnit suite on **Windows, Linux, and macOS**, then uploads portable publish artifacts for all six RIDs (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`) |
| **Update Wiki** (`.github/workflows/wiki.yml`) | Push to `docs/**` (or manual) | Copies the documentation into the GitHub wiki and rewrites internal links. Requires a `WIKI_TOKEN` repository secret (PAT with `repo` scope); skips with a notice when the secret is absent |

**GitHub Pages** is built automatically by GitHub from the `master` branch `/docs` folder and is published at <https://purelogiccode.github.io/XboxIsoStudio/> — no workflow is required for it.

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE.txt](https://github.com/purelogiccode/XboxIsoStudio/blob/master/LICENSE.txt) for the full text. By contributing, you agree that your contributions are licensed under the same license.

## Acknowledgements

- **[Avalonia](https://avaloniaui.net/)** — cross-platform UI framework powering the Windows/Linux/macOS interface
- **[XISOSharp](https://github.com/purelogiccode/XISOSharp)** — XISO/XDVDFS library powering XISO/ZAR/CSO conversion, integrity testing, and exploration
- **[ZArchiveSharp](https://github.com/purelogiccode/ZArchiveSharp)** — ZArchive/zstd library powering ZAR output and reading
- **[CHDSharp](https://github.com/purelogiccode/CHDSharp)** — CHD reading, verification, and creation powering CHD conversion, testing, and exploration
- **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** — archive extraction
