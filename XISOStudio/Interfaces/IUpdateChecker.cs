namespace XISOStudio.Interfaces;

/// <summary>
/// Checks GitHub for a newer application release.
/// </summary>
public interface IUpdateChecker
{
    /// <summary>
    /// Queries the latest GitHub release and compares its version with the running version.
    /// </summary>
    /// <returns>
    /// A tuple containing whether the check itself succeeded, whether a newer version is
    /// available, the latest version string, and the URL of the release page. The strings are
    /// <c>null</c> when no update is available or the check failed; when
    /// <c>CheckSucceeded</c> is <c>false</c> the running version must not be reported as
    /// up to date.
    /// </returns>
    /// <remarks>
    /// The release tag must contain a numeric version (for example <c>v1.2.3</c> or
    /// <c>release_1.2.3</c>); prerelease/build metadata is ignored and only the numeric part
    /// is compared. A tag without a numeric version is treated as a failed check.
    /// </remarks>
    Task<(bool CheckSucceeded, bool IsNewVersionAvailable, string? LatestVersion, string? DownloadUrl)>
        CheckForUpdateAsync();
}