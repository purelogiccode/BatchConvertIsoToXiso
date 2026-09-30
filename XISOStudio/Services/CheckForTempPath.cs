namespace XISOStudio.Services;

/// <summary>
/// Provides helpers for detecting whether a user-selected path lies inside the system
/// temporary directory.
/// </summary>
public static class CheckForTempPath
{
    /// <summary>
    /// Checks if the selected path is the system's temporary directory or a subfolder within it.
    /// </summary>
    /// <param name="selectedPath">The path selected by the user.</param>
    /// <returns>True if the path is the system temp folder or a subfolder, false otherwise.</returns>
    public static bool IsSystemTempPath(string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);

        var comparison = PathHelper.PathComparison;
        var systemTempFullPath = Path.GetFullPath(Path.GetTempPath());
        var normalizedSystemTempPath = NormalizeForComparison(systemTempFullPath);
        var normalizedSelectedPath = NormalizeForComparison(Path.GetFullPath(selectedPath));

        // Check if the selected path is exactly the system temp path or starts with it
        // (indicating a subfolder). The comparison is case-sensitive on file systems that
        // are case-sensitive (Linux).
        if (normalizedSelectedPath.Equals(normalizedSystemTempPath, comparison)) return true;

        // A temp folder that is itself a filesystem root ("C:\", "/") must not mark the whole
        // drive as temp: with a root temp path, only the root itself is rejected.
        if (IsFileSystemRoot(systemTempFullPath)) return false;

        return normalizedSelectedPath.StartsWith(normalizedSystemTempPath + Path.DirectorySeparatorChar,
            comparison);
    }

    /// <summary>
    /// Normalizes a full path for comparison: trailing separators are removed, except for a
    /// filesystem root, which keeps its separator so it cannot become a prefix that matches
    /// every path on the volume.
    /// </summary>
    /// <param name="fullPath">Full path to normalize.</param>
    /// <returns>The normalized path used for comparisons.</returns>
    private static string NormalizeForComparison(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root) && fullPath.Length <= root.Length)
        {
            return root;
        }

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    /// True when the path is a filesystem root (for example "C:\" or "/").
    /// </summary>
    /// <param name="fullPath">Full path to inspect.</param>
    /// <returns><c>true</c> when the path is a filesystem root; otherwise <c>false</c>.</returns>
    private static bool IsFileSystemRoot(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root)) return false;

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Equals(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                PathHelper.PathComparison);
    }
}