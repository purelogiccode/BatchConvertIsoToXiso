using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;

namespace XboxIsoStudio;

public partial class MainWindow
{
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            // Log the current running version
            var currentVersion = GetApplicationVersion.GetProgramVersion();
            _logger.Information("Application started. Current version: {CurrentVersion}", currentVersion);
            _logger.Information("Checking for updates...");

            var (isNewVersionAvailable, latestVersion, downloadUrl) = await _updateChecker.CheckForUpdateAsync();

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
                    _urlOpener.OpenUrl(downloadUrl);
                }
            }
            else
            {
                _logger.Information("You are using the most updated version ({CurrentVersion}).", currentVersion);
            }
        }
        catch (Exception ex)
        {
            // Log and report the error, but don't bother the user.
            _logger.Warning(ex, "Error during update check");
        }
    }
}
