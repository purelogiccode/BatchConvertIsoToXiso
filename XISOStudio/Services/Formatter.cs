namespace XISOStudio.Services;

/// <summary>
/// Formats byte counts and transfer speeds as human-readable strings for the UI.
/// </summary>
public static class Formatter
{
    /// <summary>
    /// Formats a byte count as a human-readable size using binary units (B, KB, MB, GB, TB).
    /// </summary>
    /// <param name="bytes">Number of bytes to format.</param>
    /// <returns>The formatted size string.</returns>
    public static string FormatBytes(long bytes)
    {
        const long kilobyte = 1024;
        const long megabyte = kilobyte * 1024;
        const long gigabyte = megabyte * 1024;
        const long terabyte = gigabyte * 1024;

        return bytes switch
        {
            < kilobyte => $"{bytes} B",
            < megabyte => $"{bytes / (double)kilobyte:F1} KB",
            < gigabyte => $"{bytes / (double)megabyte:F1} MB",
            < terabyte => $"{bytes / (double)gigabyte:F1} GB",
            _ => $"{bytes / (double)terabyte:F1} TB"
        };
    }

    /// <summary>
    /// Formats a transfer rate as a human-readable speed using B/s, KB/s, or MB/s.
    /// </summary>
    /// <param name="bytesPerSecond">Transfer rate in bytes per second.</param>
    /// <returns>The formatted speed string.</returns>
    public static string FormatBytesPerSecond(double bytesPerSecond)
    {
        const int kilobyte = 1024;
        const int megabyte = kilobyte * 1024;

        switch (bytesPerSecond)
        {
            case < kilobyte:
                return $"{bytesPerSecond:F1} B/s";
            case < megabyte:
                return $"{bytesPerSecond / kilobyte:F1} KB/s";
            default:
                return $"{bytesPerSecond / megabyte:F1} MB/s";
        }
    }
}