# What's New in Batch ISO to XISO Converter

<!-- Keep this file focused on the newest release. Full history lives in docs/Release-Notes.md. -->

## Version 2.9.0

**Release date:** September 2026

Version 2.9.0 is the **structured logging release**: all logging now runs through [Serilog](https://serilog.net/), and every Warning-or-higher event is automatically forwarded to the Bug Report API with complete environment and exception details.

### Highlights

#### Serilog logging pipeline
- Three sinks: the on-screen log viewer, a rolling daily log file (`%LocalAppData%\BatchConvertIsoToXiso\logs`, 10 MB per file, 14 files retained), and automatic forwarding of every **Warning-or-higher** event to the Bug Report API.
- The custom `ILogger`/`LoggerService` abstraction was removed — every service and window now logs through Serilog.

#### Complete, actionable bug reports
- Every report contains **Environment Details** (date, app name/version, OS version, architecture, bitness, Windows version, processor count, base directory, temp path), **Error Details**, and **Exception Details** (type, message, source, and stack trace, including nested and aggregate exceptions).
- The API's `environment` and `stackTrace` fields are populated as well, and fatal shutdown paths send a blocking report before the process exits.

#### Quieter logs, fewer false reports
- Expected user/environmental problems (corrupt or password-protected archives, missing files, unsupported images, disk-space/network errors) are logged at Information level, so they never generate bug reports.
- Previously silent `catch` blocks (cleanup, retries, ignored I/O failures) now log at an appropriate level, and all global exception handlers report through the same pipeline.

### Upgrading
No action is required. A rolling log file is now written under `%LocalAppData%\BatchConvertIsoToXiso\logs`, and bug reports are sent automatically for warnings and errors (previously only selected failures were reported). The app still requires the [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) and remains fully portable.

---

See the complete history in [docs/Release-Notes.md](docs/Release-Notes.md).
