using XISOStudio.Interfaces;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Moves processed files to their destination folder, retrying transient failures and checking
/// free space before cross-volume moves.
/// </summary>
public class FileMoverService : IFileMover
{
    private readonly ILogger _logger;
    private readonly IDiskMonitorService _diskMonitorService;

    // Maximum retry attempts for file move operations
    private const int MaxRetryAttempts = 6;

    // Initial delay in milliseconds (will be used for exponential backoff)
    private const int InitialRetryDelayMs = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileMoverService"/> class.
    /// </summary>
    /// <param name="logger">Logger used to report move progress and failures.</param>
    /// <param name="diskMonitorService">Service used to check free space before cross-volume moves.</param>
    public FileMoverService(ILogger logger, IDiskMonitorService diskMonitorService)
    {
        _logger = logger.ForContext<FileMoverService>();
        _diskMonitorService = diskMonitorService;
    }

    /// <summary>
    /// Moves a tested file into the destination folder, creating the folder when needed and
    /// skipping the move when the source is missing or the destination file already exists.
    /// </summary>
    /// <param name="sourceFile">Path of the file to move.</param>
    /// <param name="destinationFolder">Folder that receives the file.</param>
    /// <param name="moveReason">Reason for the move, recorded in the log.</param>
    /// <param name="token">Cancellation token for the operation.</param>
    public async Task MoveTestedFileAsync(string sourceFile, string destinationFolder, string moveReason,
        CancellationToken token)
    {
        var fileName = Path.GetFileName(sourceFile);
        var destinationFile = Path.Combine(destinationFolder, fileName);

        try
        {
            token.ThrowIfCancellationRequested();

            if (!await Task.Run(() => Directory.Exists(destinationFolder), token))
            {
                await Task.Run(() => Directory.CreateDirectory(destinationFolder), token);
            }

            token.ThrowIfCancellationRequested();

            if (await Task.Run(() => File.Exists(destinationFile), token))
            {
                _logger.Information(
                    "Cannot move {FileName}: Destination file already exists at {DestinationFile}. Skipping move.",
                    fileName, destinationFile);
                return;
            }

            if (!await Task.Run(() => File.Exists(sourceFile), token))
            {
                _logger.Information(
                    "Cannot move {FileName}: Source file no longer exists. It may have already been moved.", fileName);
                return;
            }

            // A move within the same volume is a rename and needs no additional free space;
            // only a cross-volume move copies the data and can fill the destination drive.
            if (IsCrossVolumeMove(sourceFile, destinationFolder))
            {
                var sourceFileInfo = new FileInfo(sourceFile);
                var availableSpace = _diskMonitorService.GetAvailableFreeSpace(destinationFolder);
                if (availableSpace > 0 && sourceFileInfo.Length > availableSpace)
                {
                    var requiredSpace = Formatter.FormatBytes(sourceFileInfo.Length);
                    var availableSpaceFormatted = Formatter.FormatBytes(availableSpace);
                    _logger.Information(
                        "Cannot move {FileName}: Insufficient disk space. Required: {RequiredSpace}, Available: {AvailableSpace}",
                        fileName, requiredSpace, availableSpaceFormatted);
                    return;
                }
            }

            token.ThrowIfCancellationRequested();

            // Check if either source or destination is a network path
            var isNetworkOperation =
                PathHelper.IsNetworkPath(sourceFile) || PathHelper.IsNetworkPath(destinationFolder);

            // Both local and network moves can fail transiently (antivirus scanning a
            // newly created file, network glitches, etc.) — always use retry logic.
            await MoveFileWithRetryAsync(sourceFile, destinationFile, fileName, isNetworkOperation, token);

            _logger.Information("Moved {FileName} ({MoveReason}) to {DestinationFolder}", fileName, moveReason,
                destinationFolder);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Move operation for {FileName} cancelled.", fileName);
            throw;
        }
        catch (Exception ex)
        {
            // A move failure (locked file, permissions, full disk) is environmental; the
            // user already sees the failure in the summary, so do not auto-report it.
            _logger.Information(ex, "Error moving {FileName} to {DestinationFolder}: {Message}", fileName,
                destinationFolder, ex.Message);
        }
    }

    /// <summary>
    /// Returns true when the source and destination resolve to different volumes (or when
    /// either root cannot be determined, in which case the space check is kept as a guard).
    /// </summary>
    private bool IsCrossVolumeMove(string sourceFile, string destinationFolder)
    {
        try
        {
            var sourceRoot = GetVolumeRoot(sourceFile);
            var destinationRoot = GetVolumeRoot(destinationFolder);
            if (string.IsNullOrEmpty(sourceRoot) || string.IsNullOrEmpty(destinationRoot)) return true;

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return !sourceRoot.Equals(destinationRoot, comparison);
        }
        catch (Exception ex)
        {
            // Unresolvable paths fail later with their own error; keep the space check.
            _logger.Debug(ex, "Could not resolve volume roots for {SourceFile} and {DestinationFolder}",
                sourceFile, destinationFolder);
            return true;
        }
    }

    /// <summary>
    /// Resolves the volume or mount point that contains <paramref name="path"/>. On Unix,
    /// <see cref="Path.GetPathRoot(string)"/> always returns "/", so the longest matching
    /// drive mount point is used instead; this keeps the free-space guard active for moves
    /// between mount points (for example /tmp to a mounted USB drive).
    /// </summary>
    private string? GetVolumeRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
        {
            return Path.GetPathRoot(fullPath);
        }

        var bestRoot = Path.GetPathRoot(fullPath);
        var fullPathWithSeparator = fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;

        foreach (var drive in DriveInfo.GetDrives())
        {
            var root = drive.RootDirectory.FullName;
            if (string.IsNullOrEmpty(root)) continue;

            // Ensure the root ends with a separator so "/media/usb" cannot match
            // "/media/usb2".
            if (!root.EndsWith(Path.DirectorySeparatorChar))
            {
                root += Path.DirectorySeparatorChar;
            }

            if (fullPathWithSeparator.StartsWith(root, StringComparison.Ordinal) &&
                root.Length > (bestRoot?.Length ?? 0))
            {
                bestRoot = root;
            }
        }

        return bestRoot;
    }

    /// <summary>
    /// Moves a file with retry logic and exponential backoff for transient errors
    /// (file locked by another process, network issues, etc.).
    /// </summary>
    private async Task MoveFileWithRetryAsync(string source, string dest, string fileName, bool isNetworkOperation,
        CancellationToken token)
    {
        Exception? lastException = null;

        for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                await Task.Run(() => File.Move(source, dest), token);
                return; // Success
            }
            catch (IOException ex) when (attempt < MaxRetryAttempts - 1)
            {
                lastException = ex;

                // Exponential backoff: 1000ms, 2000ms, 4000ms, 8000ms, 16000ms, etc.
                var delayMs = InitialRetryDelayMs * (int)Math.Pow(2, attempt);
                var reason = isNetworkOperation ? "Network error" : "File is locked or in use";
                _logger.Information(ex,
                    "{Reason} moving {FileName}, retrying in {DelayMs}ms... (attempt {Attempt}/{MaxRetryAttempts})",
                    reason, fileName, delayMs, attempt + 1, MaxRetryAttempts);
                await Task.Delay(delayMs, token);
            }
        }

        // All retries exhausted
        if (lastException != null)
        {
            throw new IOException(
                $"Failed to move file after {MaxRetryAttempts} attempts. Last error: {lastException.Message}",
                lastException);
        }
    }
}