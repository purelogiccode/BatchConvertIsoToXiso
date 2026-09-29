using System.Diagnostics;
using System.Runtime.InteropServices;
using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class DiskMonitorService : IDiskMonitorService, IDisposable
{
    private readonly ILogger _logger;
    private PerformanceCounter? _diskReadSpeedCounter;
    private PerformanceCounter? _diskWriteSpeedCounter;

    public string? CurrentDriveLetter { get; private set; }
    public string? StatusMessage { get; private set; }

    public DiskMonitorService(ILogger logger)
    {
        _logger = logger.ForContext<DiskMonitorService>();
    }

    // P/Invoke for GetDiskFreeSpaceEx which works with UNC paths (Windows only)
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    public void StartMonitoring(string? path)
    {
        var driveLetter = PathHelper.GetDriveLetter(path);
        var isNetworkPath = PathHelper.IsNetworkPath(path);

        // Never treat a UNC path (which has no drive letter, so both values are null) as
        // "same drive" before the network check below; otherwise its status is never shown.
        if (!isNetworkPath &&
            string.Equals(CurrentDriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase)) return;

        StopMonitoring();

        if (!OperatingSystem.IsWindows())
        {
            // Windows performance counters are not available on Linux/macOS; the UI shows N/A.
            return;
        }

        // Check for network drives - explicitly excluded from speed monitoring
        if (isNetworkPath)
        {
            StatusMessage = "Disk speed monitoring unavailable for network drives";
            _logger.Information("Disk speed monitoring unavailable for network drives.");
            return;
        }

        if (string.IsNullOrEmpty(driveLetter))
        {
            StatusMessage = "Disk speed monitoring unavailable - unable to determine drive letter";
            return;
        }

        var perfCounterInstanceName = driveLetter.EndsWith(':') ? driveLetter : driveLetter + ":";

        try
        {
            // Check if LogicalDisk category exists
            if (!PerformanceCounterCategory.Exists("LogicalDisk"))
            {
                StatusMessage = "Disk speed monitoring unavailable - performance counters disabled";
                _logger.Information(
                    "Performance counter category 'LogicalDisk' not available. Performance counters may be disabled.");
                return;
            }

            // Check if drive instance exists
            if (!PerformanceCounterCategory.InstanceExists(perfCounterInstanceName, "LogicalDisk"))
            {
                StatusMessage = $"Disk speed monitoring unavailable for drive {perfCounterInstanceName}";
                _logger.Information("Performance counter for drive {Drive} not available.", perfCounterInstanceName);
                return;
            }

            // Initialize read speed counter. Assign the field before priming so the catch
            // path (StopMonitoring) disposes the handle when NextValue throws.
            var readCounter =
                new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", perfCounterInstanceName, true);
            _diskReadSpeedCounter = readCounter;
            readCounter.NextValue(); // Prime the counter

            // Initialize write speed counter
            var writeCounter =
                new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", perfCounterInstanceName, true);
            _diskWriteSpeedCounter = writeCounter;
            writeCounter.NextValue(); // Prime the counter

            CurrentDriveLetter = driveLetter;
            StatusMessage = null; // Clear any previous status
            _logger.Information("Monitoring disk speed for drive: {Drive}", perfCounterInstanceName);
        }
        catch (Exception ex)
        {
            // StopMonitoring clears the status message, so set it afterwards — otherwise the
            // UI can never show the reason why monitoring is unavailable.
            StopMonitoring();
            StatusMessage = "Disk speed monitoring unavailable - performance counter error";
            // Performance counters being unavailable/broken is environmental, not a defect.
            _logger.Information(ex, "Failed to initialize disk monitor for {Drive}", perfCounterInstanceName);
        }
    }

    public void StopMonitoring()
    {
        _diskReadSpeedCounter?.Dispose();
        _diskReadSpeedCounter = null;
        _diskWriteSpeedCounter?.Dispose();
        _diskWriteSpeedCounter = null;
        CurrentDriveLetter = null;
        StatusMessage = null;
    }

    public string GetCurrentReadSpeedFormatted()
    {
        if (!OperatingSystem.IsWindows()) return "N/A";
        if (_diskReadSpeedCounter == null) return "N/A";

        try
        {
            var val = _diskReadSpeedCounter.NextValue();
            return Formatter.FormatBytesPerSecond(val);
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Failed to read current disk read speed. Stopping monitoring.");
            StopMonitoring();
            return "N/A";
        }
    }

    public string GetCurrentWriteSpeedFormatted()
    {
        if (!OperatingSystem.IsWindows()) return "N/A";
        if (_diskWriteSpeedCounter == null) return "N/A";

        try
        {
            var val = _diskWriteSpeedCounter.NextValue();
            return Formatter.FormatBytesPerSecond(val);
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Failed to read current disk write speed. Stopping monitoring.");
            StopMonitoring();
            return "N/A";
        }
    }

    public long GetAvailableFreeSpace(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return 0;
        }

        try
        {
            // Handle UNC paths (network shares) using P/Invoke (Windows only)
            if (OperatingSystem.IsWindows() && PathHelper.IsUncPath(path))
            {
                // For UNC paths, use GetDiskFreeSpaceEx which works with network shares
                // We need to pass the share root (\\server\share) not a subdirectory
                var shareInfo = PathHelper.TryGetUncShareInfo(path);
                if (shareInfo.HasValue)
                {
                    var shareRoot = $@"\\{shareInfo.Value.Server}\{shareInfo.Value.Share}";
                    if (GetDiskFreeSpaceEx(shareRoot, out var freeBytesAvailable, out _, out _))
                    {
                        return (long)freeBytesAvailable;
                    }
                }

                // Fallback: try the path as-is
                if (GetDiskFreeSpaceEx(path, out var freeBytes, out _, out _))
                {
                    return (long)freeBytes;
                }

                return 0;
            }

            // Handle mapped network drives and local drives
            var driveLetter = PathHelper.GetDriveLetter(path);
            if (!string.IsNullOrEmpty(driveLetter))
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (driveInfo.IsReady)
                {
                    return driveInfo.AvailableFreeSpace;
                }
            }

            // Fallback for other cases
            var fallbackDriveInfo = new DriveInfo(path);
            if (fallbackDriveInfo.IsReady)
            {
                return fallbackDriveInfo.AvailableFreeSpace;
            }
        }
        catch (Exception ex)
        {
            // Ignore errors and return 0
            _logger.Information(ex, "Failed to determine available free space for path: {Path}", path);
        }

        return 0;
    }

    public string? FindDriveWithFreeSpace(long requiredBytes, string? excludeDrive = null)
    {
        try
        {
            var excludedRoot = excludeDrive?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var requiredWithBuffer = PathHelper.AddSafetyBuffer(requiredBytes);

            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady)
                    continue;

                // Windows: only fixed local drives are eligible. Unix: DriveType is not
                // meaningful, so accept anything that is not removable or networked.
                if (OperatingSystem.IsWindows())
                {
                    if (drive.DriveType != DriveType.Fixed)
                        continue;
                }
                else if (drive.DriveType is DriveType.Removable or DriveType.Network)
                {
                    continue;
                }

                var root = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (excludedRoot != null && root.Equals(excludedRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (drive.AvailableFreeSpace >= requiredWithBuffer)
                    return drive.Name;
            }
        }
        catch (Exception ex)
        {
            // Ignore errors during drive enumeration
            _logger.Information(ex, "Failed to enumerate drives while searching for free space.");
        }

        return null;
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}