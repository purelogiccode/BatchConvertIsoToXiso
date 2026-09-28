using System.Globalization;
using System.IO;

namespace XboxIsoStudio.Services;

/// <summary>
///     Extension filters shared by the UI file lists and the orchestrator folder scans so
///     both always offer exactly the same files.
/// </summary>
public static class SupportedFiles
{
    /// <summary>
    ///     Returns true when the path has an extension the conversion engine can process.
    /// </summary>
    public static bool IsConvertible(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".iso" or ".zip" or ".7z" or ".rar";
    }

    /// <summary>
    ///     Returns true when the path is a plain ISO image (<c>.iso</c>). Used for
    ///     ISO files extracted from archives, which are the only convertible entries.
    /// </summary>
    public static bool IsIso(string path)
    {
        return Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Returns true when the path is an image the integrity test can verify:
    ///     plain ISO, CISO (<c>.cso</c>, including the first part of a split set) or
    ///     ZAR archive.
    /// </summary>
    public static bool IsTestable(string path)
    {
        return IsImage(path);
    }

    private static bool IsImage(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".iso" or ".zar" => true,
            ".cso" => !IsSplitContinuationPart(path),
            _ => false
        };
    }

    /// <summary>
    ///     Split CISO parts are named <c>game.1.cso</c>, <c>game.2.cso</c>, …; only part 1
    ///     carries the header and opens the whole image, so part 2+ must not be listed as
    ///     standalone images.
    /// </summary>
    private static bool IsSplitContinuationPart(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        var lastDot = stem.LastIndexOf('.');
        if (lastDot < 0) return false;

        return int.TryParse(stem.AsSpan(lastDot + 1), NumberStyles.None, CultureInfo.InvariantCulture,
                   out var part) && part >= 2;
    }
}
