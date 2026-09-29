using Serilog;

namespace XboxIsoStudio.Services;

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
                logger?.Information("Successfully deleted temp folder: {TempFolder}", Path.GetFileName(directoryPath));
                return;
            }
            catch (OperationCanceledException)
            {
                // Cleanup runs from finally blocks; cancellation must not mask the
                // original operation error.
                logger?.Debug("Deletion of '{TempFolder}' was canceled", Path.GetFileName(directoryPath));
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The last attempt must not throw: cleanup runs from finally blocks and
                // a propagation here would mask the original operation error.
                if (attempt < maxRetries)
                {
                    var reason = ex is UnauthorizedAccessException ? "access denied" : "files locked";
                    logger?.Warning(ex,
                        "Deletion attempt {Attempt}/{MaxRetries} failed for '{TempFolder}' ({Reason}). Retrying...",
                        attempt, maxRetries, Path.GetFileName(directoryPath), reason);
                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        logger?.Debug("Deletion retry for '{TempFolder}' was canceled",
                            Path.GetFileName(directoryPath));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "Failed to delete '{TempFolder}'", Path.GetFileName(directoryPath));
                return;
            }
        }

        logger?.Warning("Could not delete '{TempFolder}' after {MaxRetries} attempts. Manual cleanup may be needed.",
            Path.GetFileName(directoryPath), maxRetries);
    }

    /// <summary>
    ///     Work folders are always created as <c>&lt;XboxIsoStudio_*&gt;/&lt;guid&gt;</c>.
    ///     Cleanup only removes those GUID-named children, and only when they are old
    ///     enough that no running instance can still be using them. Unrelated user
    ///     folders with the same prefix and another instance's fresh work folders are
    ///     therefore left alone.
    /// </summary>
    private static readonly TimeSpan MinimumOrphanAge = TimeSpan.FromHours(6);

    /// <summary>
    ///     Finds stale <c>&lt;XboxIsoStudio_*&gt;/&lt;guid&gt;</c> work folders under the given
    ///     roots. Only GUID-named children older than <see cref="MinimumOrphanAge" /> are
    ///     returned; everything else is considered user data or still in use.
    /// </summary>
    internal static List<string> FindOrphanedWorkDirectories(IEnumerable<string> rootsToScan, DateTime utcNow,
        ILogger? logger)
    {
        const string searchPattern = "XboxIsoStudio_*";
        var found = new List<string>();

        foreach (var root in rootsToScan)
        {
            try
            {
                foreach (var parent in Directory.EnumerateDirectories(root, searchPattern,
                             SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        foreach (var child in Directory.EnumerateDirectories(parent))
                        {
                            if (!Guid.TryParse(Path.GetFileName(child), out _)) continue;

                            var lastWriteUtc = Directory.GetLastWriteTimeUtc(child);
                            if (utcNow - lastWriteUtc < MinimumOrphanAge) continue;

                            found.Add(child);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Debug(ex, "Could not inspect temp folder '{ParentFolder}'", parent);
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.Warning(ex, "Error enumerating temp folders on {Root}", root);
            }
        }

        return found;
    }

    /// <summary>
    /// Cleans up orphaned XboxIsoStudio work folders on all fixed drives
    /// </summary>
    public static async Task CleanupBatchConvertTempFoldersAsync(ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
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
                catch (Exception ex)
                {
                    logger.Debug(ex, "Could not resolve the system temp path for cleanup");
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
                catch (Exception ex)
                {
                    // Ignore drive enumeration errors
                    logger.Debug(ex, "Could not enumerate drives for temp folder cleanup");
                }

                return FindOrphanedWorkDirectories(rootsToScan, DateTime.UtcNow, logger);
            }, cancellationToken);

            foreach (var dir in directoriesToClean)
            {
                cancellationToken.ThrowIfCancellationRequested();
                logger.Information("Cleaning up orphaned temp folder: {TempFolder}", Path.GetFileName(dir));
                await TryDeleteDirectoryWithRetryAsync(dir, 3, 1000, logger, CancellationToken.None);
            }
        }
        catch (OperationCanceledException)
        {
            logger.Debug("Temp folder cleanup was canceled");
            throw;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error cleaning up temporary folders");
        }
    }
}