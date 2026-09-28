using XISOSharp;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// Path helpers shared by the image explorers (XISO/CISO and ZAR). Internal
/// paths are <c>/</c>-separated, root-absolute and case-insensitive, mirroring
/// the XISOSharp path contract.
/// </summary>
public static class ImagePaths
{
    /// <summary>
    /// Normalizes a container-internal path: backslashes become forward slashes,
    /// a leading slash is enforced, and a trailing slash is stripped (except root).
    /// Empty input denotes the root.
    /// </summary>
    public static string Normalize(string? internalPath)
    {
        return XisoExplorer.Normalize(internalPath);
    }

    /// <summary>
    /// Joins a directory path and an entry name into a container-internal path.
    /// </summary>
    public static string Combine(string directory, string name)
    {
        return XisoExplorer.Combine(directory, name);
    }

    /// <summary>
    /// Returns the parent of a normalized internal path (<c>"/"</c> for top-level paths).
    /// </summary>
    public static string GetParent(string internalPath)
    {
        var normalized = Normalize(internalPath);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }
}
