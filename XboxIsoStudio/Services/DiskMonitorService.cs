using System.Diagnostics;
using System.IO;
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

    // P/Invoke for GetDiskFreeSpaceEx which works with UNC paths
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string lpDirectoryName,
        out ulong lpFreeBytesAvailable,
        out ulong lpTotalNumberOfBytes,
        out ulong lpTotalNumberOfFreeBytes);

    public void StartMonitoring(string? path)
    {
        var driveLetter = PathHelper.GetDriveLetter(path);
        if (string.Equals(CurrentDriveLetter, driveLetter, StringComparison.OrdinalIgnoreCase)) return;

        StopMonitoring();

        // Check for network drives - explicitly excluded from speed monitoring
        if (PathHelper.IsNetworkPath(path))
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

            // Initialize read speed counter
            var readCounter =
                new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", perfCounterInstanceName, true);
            readCounter.NextValue(); // Prime the counter
            _diskReadSpeedCounter = readCounter;

            // Initialize write speed counter
            var writeCounter =
                new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", perfCounterInstanceName, true);
            writeCounter.NextValue(); // Prime the counter
            _diskWriteSpeedCounter = writeCounter;

            CurrentDriveLetter = driveLetter;
            StatusMessage = null; // Clear any previous status
            _logger.Information("Monitoring disk speed for drive: {Drive}", perfCounterInstanceName);
        }
        catch (Exception ex)
        {
            StatusMessage = "Disk speed monitoring unavailable - performance counter error";
            _logger.Warning(ex, "Failed to initialize disk monitor for {Drive}", perfCounterInstanceName);
            StopMonitoring();
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
        if (_diskReadSpeedCounter == null) return "N/A";

        try
        {
            var val = _diskReadSpeedCounter.NextValue();
            return Formatter.FormatBytesPerSecond(val);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to read current disk read speed. Stopping monitoring.");
            StopMonitoring();
            return "N/A";
        }
    }

    public string GetCurrentWriteSpeedFormatted()
    {
        if (_diskWriteSpeedCounter == null) return "N/A";

        try
        {
            var val = _diskWriteSpeedCounter.NextValue();
            return Formatter.FormatBytesPerSecond(val);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to read current disk write speed. Stopping monitoring.");
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
            // Handle UNC paths (network shares) using P/Invoke
            if (PathHelper.IsUncPath(path))
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

            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (!drive.IsReady)
                    continue;

                if (drive.DriveType != DriveType.Fixed)
                    continue;

                var root = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (excludedRoot != null && root.Equals(excludedRoot, StringComparison.OrdinalIgnoreCase))
                    continue;

                var requiredWithBuffer = requiredBytes + Math.Max(requiredBytes / 10, 200L * 1024 * 1024);
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