using XISOStudio.Models;
using XISOStudio.Services;

namespace XISOStudio;

/// <summary>Checks for application updates at startup and offers to open the download page.</summary>
public partial class MainWindow
{
    /// <summary>Checks GitHub for a newer release and offers to open its download page.</summary>
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            // Log the current running version
            var currentVersion = GetApplicationVersion.GetProgramVersion();
            _logger.Information("Application started. Current version: {CurrentVersion}", currentVersion);
            _logger.Information("Checking for updates...");

            var (checkSucceeded, isNewVersionAvailable, latestVersion, downloadUrl) =
                await _updateChecker.CheckForUpdateAsync();

            if (!checkSucceeded)
            {
                // Offline, rate-limited, or the release could not be parsed: do not claim
                // the running version is up to date.
                _logger.Information(
                    "Could not check for updates. Please check your internet connection and try again later.");
                return;
            }

            if (isNewVersionAvailable && !string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(latestVersion))
            {
                _logger.Information("Update available! Version {LatestVersion} is available on the release page.",
                    latestVersion);
                _logger.Information("Current version: {CurrentVersion} | Available version: {LatestVersion}",
                    currentVersion, latestVersion);

                var result = await _messageBoxService.ShowAsync(
                    $"A new version ({latestVersion}) is available. Would you like to go to the download page?",
                    "Update Available", UiMessageBoxButton.YesNo, UiMessageBoxImage.Information);

                if (result == UiMessageBoxResult.Yes)
                {
                    try
                    {
                        _urlOpener.OpenUrl(downloadUrl);
                    }
                    catch (Exception ex)
                    {
                        // No browser/handler configured is an environment condition, not a defect.
                        _logger.Information(ex, "Could not open the download page for version {LatestVersion}",
                            latestVersion);
                    }
                }
            }
            else
            {
                _logger.Information("You are using the most updated version ({CurrentVersion}).", currentVersion);
            }
        }
        catch (Exception ex)
        {
            // Update checks fail whenever the machine is offline; environmental, not a defect.
            _logger.Information(ex, "Error during update check");
        }
    }
}