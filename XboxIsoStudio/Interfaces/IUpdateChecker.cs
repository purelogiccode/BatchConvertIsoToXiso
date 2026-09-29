namespace XboxIsoStudio.Interfaces;

/// <summary>
/// Checks GitHub for a newer application release.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Queries the latest GitHub release and compares its version with the running version.
    /// </summary>
    /// <returns>
    /// A tuple containing whether a newer version is available, the latest version string,
    /// and the URL of the release page; the strings are <c>null</c> when no update is available.
    /// </returns>
    Task<(bool IsNewVersionAvailable, string? LatestVersion, string? DownloadUrl)> CheckForUpdateAsync();
}