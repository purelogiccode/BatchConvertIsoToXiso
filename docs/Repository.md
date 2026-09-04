# Repository

| Getting Started | Using the App | Technical Reference | Project |
|---|---|---|---|
| [Home](index.md) | [Usage Guide](Usage-Guide.md) | [Architecture](Architecture.md) | [**Repository**](Repository.md) |
| [Installation](Installation.md) | [Conversion Methods](Conversion-Methods.md) | [XDVDFS Technical Docs](XDVDFS-Technical-Documentation.md) | [Building from Source](Building-from-Source.md) |
| | [XISO Explorer](XISO-Explorer.md) | [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) | |

---

This page describes the repository itself: where things live, how releases are managed, and how to contribute.

- **Repository:** <https://github.com/purelogiccode/BatchConvertIsoToXiso>
- **Issues:** <https://github.com/purelogiccode/BatchConvertIsoToXiso/issues>
- **Releases:** <https://github.com/purelogiccode/BatchConvertIsoToXiso/releases>
- **Website:** <https://www.purelogiccode.com>
- **License:** [GNU GPL v3.0](https://github.com/purelogiccode/BatchConvertIsoToXiso/blob/master/LICENSE.txt)

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
│   ├── Repository.md                      This page
│   └── _Sidebar.md                        Sidebar menu (GitHub wiki)
├── BatchConvertIsoToXiso/                 Main WPF application project
│   ├── Interfaces/                        Service contracts
│   ├── Models/                            DTOs and enums
│   ├── Services/                          All business logic
│   │   └── XisoServices/                  XDVDFS parser + native engine
│   ├── MainWindow*.cs                     Partial classes for the main window
│   ├── extract-xiso.exe, xdvdfs.exe       Bundled external conversion tools
│   ├── bchunk.exe, 7za.exe                Bundled helper tools
│   └── BatchConvertIsoToXiso.csproj
├── BatchConvertIsoToXiso.Tests/           xUnit + Moq test project
├── CSharp_BatchConvertIsoToXiso.sln       Solution file
├── global.json                            Pins the .NET SDK version
├── ReadMe.md                              Repository front page
├── LICENSE.txt                            GNU GPL v3.0
└── screenshot*.png                        Screenshots used by the ReadMe
```

## Branching and Releases

- The primary branch is **`master`**.
- **Releases** are tagged on GitHub and published on the [Releases](https://github.com/purelogiccode/BatchConvertIsoToXiso/releases) page with ready-to-run ZIP archives.
- **Versioning:** `MAJOR.MINOR.PATCH` (currently 2.7.2). The application's update checker compares its version against the latest release tag on GitHub, so published tags must follow the `vMAJOR.MINOR.PATCH` convention.

## Contributing

Contributions are welcome!

### Reporting Bugs

1. Check [Troubleshooting & FAQ](Troubleshooting-and-FAQ.md) first — many reported issues are environmental (disk space, permissions, antivirus locks) and already handled with clear messages.
2. Search existing [issues](https://github.com/purelogiccode/BatchConvertIsoToXiso/issues) to avoid duplicates.
3. Open a new issue including:
   - Application version (visible in the About window)
   - Windows version and architecture (x64/ARM64)
   - The conversion method used
   - The relevant lines from the in-application log

### Submitting Changes

1. Fork the repository and create a feature branch from `master`.
2. Follow the existing code style — the build enforces **Meziantou** and **Roslynator** analyzer rules as warnings; keep the build clean.
3. Add or update tests in `BatchConvertIsoToXiso.Tests` for behavioral changes.
4. Verify with:

   ```bash
   dotnet build CSharp_BatchConvertIsoToXiso.sln
   dotnet test CSharp_BatchConvertIsoToXiso.sln
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

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE.txt](https://github.com/purelogiccode/BatchConvertIsoToXiso/blob/master/LICENSE.txt) for the full text. By contributing, you agree that your contributions are licensed under the same license.

## Acknowledgements

- **[extract-xiso](https://github.com/XboxDev/extract-xiso)** — XboxDev team
- **[xdvdfs](https://github.com/antangelo/xdvdfs)** — antangelo
- **[XboxKit by Deterous](https://github.com/Deterous/XboxKit)** — basis of the native engine, significantly enhanced
- **[bchunk](https://github.com/extramaster/bchunk)** — CUE/BIN conversion
- **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** — archive extraction
