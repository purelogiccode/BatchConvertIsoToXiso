using XISOSharp;

namespace XISOStudio.Services;

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
    /// <param name="internalPath">Container-internal path to normalize; <c>null</c> or empty denotes the root.</param>
    /// <returns>The normalized root-absolute path.</returns>
    public static string Normalize(string? internalPath)
    {
        return XisoExplorer.Normalize(internalPath);
    }

    /// <summary>
    /// Joins a directory path and an entry name into a container-internal path.
    /// </summary>
    /// <param name="directory">Directory portion of the path.</param>
    /// <param name="name">Entry name to append to the directory.</param>
    /// <returns>The combined container-internal path.</returns>
    public static string Combine(string directory, string name)
    {
        return XisoExplorer.Combine(directory, name);
    }

    /// <summary>
    /// Returns the parent of a normalized internal path (<c>"/"</c> for top-level paths).
    /// </summary>
    /// <param name="internalPath">Internal path whose parent is resolved.</param>
    /// <returns>The parent path, or <c>"/"</c> for top-level paths.</returns>
    public static string GetParent(string internalPath)
    {
        var normalized = Normalize(internalPath);
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash <= 0) return "/";

        // Interior repeated slashes are preserved by Normalize, so trim any trailing
        // separators to return a normalized parent ("/a//b" -> "/a", not "/a/").
        var parent = normalized[..lastSlash].TrimEnd('/');
        return parent.Length == 0 ? "/" : parent;
    }
}