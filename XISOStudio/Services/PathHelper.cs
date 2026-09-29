using XISOStudio.Interfaces;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Provides helpers for working with filesystem paths, including drive letters, UNC shares,
/// and classification of network, disk-space, and device I/O errors.
/// </summary>
public static class PathHelper
{
    /// <summary>
    ///     Comparison to use for filesystem paths: case-insensitive on Windows and macOS,
    ///     ordinal on case-sensitive file systems (Linux).
    /// </summary>
    public static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Extracts the drive letter (e.g., "C:") from a given path.
    /// </summary>
    public static string? GetDriveLetter(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            // Handle UNC paths (network shares) which don't have traditional drive letters
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return null;

            var fullPath = Path.GetFullPath(path);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot)) return null;

            var driveInfo = new DriveInfo(pathRoot);
            return driveInfo.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to determine drive letter for path: {Path}", path);
            return null;
        }
    }

    /// <summary>
    /// Determines if the given path is a UNC (Universal Naming Convention) network path.
    /// Examples: \\server\share, \\server\share\folder\file.txt
    /// </summary>
    public static bool IsUncPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        return path.StartsWith(@"\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines if the given path is a network path (either UNC or a mapped network drive).
    /// </summary>
    public static bool IsNetworkPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        // Check for UNC path first
        if (IsUncPath(path)) return true;

        // Check if it's a mapped network drive
        try
        {
            var driveLetter = GetDriveLetter(path);
            if (string.IsNullOrEmpty(driveLetter)) return false;

            var driveInfo = new DriveInfo(driveLetter);
            return driveInfo.DriveType == DriveType.Network;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to determine if path is a network path: {Path}", path);
            return false;
        }
    }

    /// <summary>
    /// Extracts the server and share name from a UNC path.
    /// Returns null if the path is not a valid UNC path.
    /// Example: \\server\share\folder -> (server: "server", share: "share")
    /// </summary>
    public static (string Server, string Share)? TryGetUncShareInfo(string? path)
    {
        if (string.IsNullOrEmpty(path) || !IsUncPath(path))
            return null;

        try
        {
            // Remove the leading \\
            var trimmed = path.Substring(2);

            // Split by backslash
            var parts = trimmed.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
        }
        catch (Exception ex)
        {
            // Ignore parsing errors
            Log.Debug(ex, "Failed to parse UNC share info from path: {Path}", path);
        }

        return null;
    }

    /// <summary>
    /// Common network-related error messages that indicate transient network failures.
    /// These errors may be resolved by retrying the operation.
    /// </summary>
    public static readonly string[] NetworkErrorPatterns =
    [
        "network path was not found",
        "network name is no longer available",
        "the specified network name is no longer available",
        "an unexpected network error occurred",
        "the network location cannot be reached",
        "a device attached to the system is not functioning",
        "the semaphore timeout period has expired",
        "the network path was not found",
        "the specified server cannot perform the requested operation",
        "the remote procedure call failed",
        "the remote procedure call was cancelled",
        "the network bios session limit was exceeded",
        "network access is denied",
        "the network connection was aborted",
        "the network connection was reset",
        "the network is not present or not started",
        "the account is not authorized to login from this station",
        "logon failure: unknown user name or bad password",
        "the session was cancelled"
    ];

    /// <summary>
    /// Checks if an exception message contains network-related error patterns
    /// that suggest a transient network failure. Supports messages in multiple
    /// languages (English, German, French, Spanish, Italian).
    /// </summary>
    public static bool IsNetworkError(Exception? exception)
    {
        if (exception == null) return false;

        if (MatchesNetworkPatterns(exception.Message))
            return true;

        if (exception.InnerException != null && MatchesNetworkPatterns(exception.InnerException.Message))
            return true;

        return false;
    }

    private static bool MatchesNetworkPatterns(string message)
    {
        // English patterns
        if (NetworkErrorPatterns.Any(pattern => message.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            return true;

        // "device" can appear in non-network errors like "The device is not ready" (ERROR_NOT_READY)
        // or a hardware "I/O device error" (ERROR_IO_DEVICE); neither is a network failure.
        if (message.Contains("device", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("device is not ready", StringComparison.OrdinalIgnoreCase) &&
            !MatchesDeviceIoPatterns(message))
        {
            return true;
        }

        // German
        if (message.Contains("Netzwerk", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("nicht mehr verfügbar", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // French
        if (message.Contains("réseau", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("n'est plus disponible", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Spanish — use contextual phrases to avoid matching English words like "redirect"
        if (message.Contains("la red", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("de red", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Italian
        if (message.Contains("rete", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if an exception is related to disk space issues.
    /// Checks HResult codes for ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL,
    /// as well as multilingual error messages.
    /// </summary>
    public static bool IsDiskSpaceError(Exception ex)
    {
        if (ex is IOException ioEx)
        {
            var hResult = ioEx.HResult & 0xFFFF;
            if (hResult is 0x70 or 0x27) return true; // ERROR_DISK_FULL, ERROR_HANDLE_DISK_FULL
        }

        if (ex.InnerException is IOException innerIoEx)
        {
            var hResult = innerIoEx.HResult & 0xFFFF;
            if (hResult is 0x70 or 0x27) return true;
        }

        var message = ex.Message;
        if (ex.InnerException != null)
        {
            message += " " + ex.InnerException.Message;
        }

        return message.Contains("Not enough space", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("not enough disk space", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("insufficient disk space", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Disk full", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Espace insuffisant", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("disque plein", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Win32 error code ERROR_IO_DEVICE: the request could not be performed because
    /// of an I/O device error (e.g. a failing or disconnected drive).
    /// </summary>
    private const int ErrorIoDevice = 0x45D;

    /// <summary>
    /// Determines if an exception was caused by a hardware I/O failure on the source or
    /// destination device (e.g. a failing, disconnected, or power-cycling drive).
    /// Windows localizes the message, so the Win32 error code is checked first, with
    /// localized message patterns as a fallback for wrapped exceptions that lost the code.
    /// </summary>
    public static bool IsDeviceIoError(Exception? exception)
    {
        if (exception == null) return false;

        if (HasDeviceIoErrorCode(exception) || MatchesDeviceIoPatterns(exception.Message)) return true;

        return exception.InnerException != null &&
               (HasDeviceIoErrorCode(exception.InnerException) ||
                MatchesDeviceIoPatterns(exception.InnerException.Message));
    }

    private static bool HasDeviceIoErrorCode(Exception exception)
    {
        return exception is IOException ioException && (ioException.HResult & 0xFFFF) == ErrorIoDevice;
    }

    private static bool MatchesDeviceIoPatterns(string message)
    {
        // English, Italian, German, French and Spanish variants of the Windows message
        return message.Contains("I/O device error", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("dispositivo I/O", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("E/A-Gerät", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("périphérique d'E/S", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("dispositivo de E/S", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Adds the standard safety buffer (10% or 200 MB, whichever is larger) to a required
    /// size, saturating at <see cref="long.MaxValue" /> so extreme sizes cannot wrap around
    /// and make an undersized drive look sufficient.
    /// </summary>
    internal static long AddSafetyBuffer(long requiredBytes)
    {
        var buffer = Math.Max(requiredBytes / 10, 200L * 1024 * 1024);
        return requiredBytes > long.MaxValue - buffer ? long.MaxValue : requiredBytes + buffer;
    }

    /// <summary>
    /// Resolves a temporary directory path with sufficient disk space.
    /// First checks the system temp drive, then falls back to other local drives.
    /// </summary>
    public static string ResolveTempDirectory(long requiredSize, string tempSubfolder,
        IDiskMonitorService diskMonitorService)
    {
        var defaultTempPath = Path.GetTempPath();
        var defaultTempDriveRoot = Path.GetPathRoot(defaultTempPath);
        var requiredWithBuffer = AddSafetyBuffer(requiredSize);

        if (defaultTempDriveRoot != null)
        {
            try
            {
                var defaultDrive = new DriveInfo(defaultTempDriveRoot);
                if (defaultDrive.IsReady && defaultDrive.AvailableFreeSpace >= requiredWithBuffer)
                    return Path.Combine(defaultTempPath, tempSubfolder, Guid.NewGuid().ToString());
            }
            catch (Exception ex)
            {
                // Ignore and fall through to alternative search
                Log.Debug(ex, "Could not inspect default temp drive: {TempDriveRoot}", defaultTempDriveRoot);
            }
        }

        var altDrive = diskMonitorService.FindDriveWithFreeSpace(requiredSize, defaultTempDriveRoot);
        if (altDrive != null)
        {
            return Path.Combine(altDrive, tempSubfolder, Guid.NewGuid().ToString());
        }

        var requiredFormatted = Formatter.FormatBytes(requiredWithBuffer);
        var defaultAvailable = Formatter.FormatBytes(diskMonitorService.GetAvailableFreeSpace(defaultTempPath));
        throw new IOException(
            $"Not enough disk space to create temporary files. Required: {requiredFormatted}, Available: {defaultAvailable}. No other local drives have sufficient free space. Please free up disk space and try again.");
    }
}