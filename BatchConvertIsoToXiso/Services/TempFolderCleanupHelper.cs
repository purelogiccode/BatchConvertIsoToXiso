using System.IO;
using BatchConvertIsoToXiso.Interfaces;

namespace BatchConvertIsoToXiso.Services;

public static class TempFolderCleanupHelper
{
    /// <summary>
    /// Deletes a directory with retry logic for locked files
    /// </summary>
    public static async Task TryDeleteDirectoryWithRetryAsync(string directoryPath, int maxRetries, int delayMs,
        ILogger? logger, CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (!Directory.Exists(directoryPath)) return;

                Directory.Delete(directoryPath, true);
                logger?.LogMessage($"Successfully deleted temp folder: {Path.GetFileName(directoryPath)}");
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The last attempt must not throw: cleanup runs from finally blocks and
                // a propagation here would mask the original operation error.
                if (attempt < maxRetries)
                {
                    var reason = ex is UnauthorizedAccessException ? "access denied" : "files locked";
                    logger?.LogMessage(
                        $"Deletion attempt {attempt}/{maxRetries} failed for '{Path.GetFileName(directoryPath)}' ({reason}). Retrying...");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger?.LogMessage($"Failed to delete '{Path.GetFileName(directoryPath)}': {ex.Message}");
                return;
            }
        }

        logger?.LogMessage(
            $"WARNING: Could not delete '{Path.GetFileName(directoryPath)}' after {maxRetries} attempts. Manual cleanup may be needed.");
    }

    /// <summary>
    /// Cleans up all BatchConvertIsoToXiso temp folders on all fixed drives
    /// </summary>
    public static async Task CleanupBatchConvertTempFoldersAsync(ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string searchPattern = "BatchConvertIsoToXiso_*";

        // Enumerating drives and directories can block for many seconds on idle or spinning
        // drives (DriveInfo.IsReady waits for the disk to respond), so always do it on a
        // background thread — callers may be on the UI thread.
        var directoriesToClean = await Task.Run(() =>
        {
            var rootsToScan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                rootsToScan.Add(Path.GetTempPath());
            }
            catch
            {
                // ignored
            }

            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (drive is { IsReady: true, DriveType: DriveType.Fixed })
                        rootsToScan.Add(drive.Name);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ignore drive enumeration errors
            }

            var found = new List<string>();
            foreach (var root in rootsToScan)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    found.AddRange(Directory.EnumerateDirectories(root, searchPattern,
                        SearchOption.TopDirectoryOnly));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogMessage($"Error enumerating temp folders on {root}: {ex.Message}");
                }
            }

            return found;
        }, cancellationToken);

        foreach (var dir in directoriesToClean)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogMessage($"Cleaning up orphaned temp folder: {Path.GetFileName(dir)}");
            await TryDeleteDirectoryWithRetryAsync(dir, 3, 1000, logger, cancellationToken);
        }
    }
}