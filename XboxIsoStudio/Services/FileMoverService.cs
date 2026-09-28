using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class FileMoverService : IFileMover
{
    private readonly ILogger _logger;
    private readonly IDiskMonitorService _diskMonitorService;

    // Maximum retry attempts for file move operations
    private const int MaxRetryAttempts = 6;

    // Initial delay in milliseconds (will be used for exponential backoff)
    private const int InitialRetryDelayMs = 1000;

    public FileMoverService(ILogger logger, IDiskMonitorService diskMonitorService)
    {
        _logger = logger.ForContext<FileMoverService>();
        _diskMonitorService = diskMonitorService;
    }

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

            // Check available disk space before moving
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
            _logger.Error(ex, "Error moving {FileName} to {DestinationFolder}: {Message}", fileName, destinationFolder,
                ex.Message);
        }
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
                _logger.Warning(ex,
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