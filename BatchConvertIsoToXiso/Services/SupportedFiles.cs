using System.IO;

namespace BatchConvertIsoToXiso.Services;

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
    ///     Returns true when the path is an ISO image that can be integrity tested.
    /// </summary>
    public static bool IsIso(string path)
    {
        return Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase);
    }
}
