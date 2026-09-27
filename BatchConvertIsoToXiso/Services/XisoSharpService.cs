using System.IO;
using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using XISOSharp;
using XISOSharp.Models;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// Converts Xbox ISO images to optimized XISO format using the XISOSharp library.
/// All encoding/decoding work is delegated to the library; no external conversion
/// binaries are used.
/// </summary>
public class XisoSharpService : IXisoSharpService
{
    private readonly ILogger _logger;
    private readonly IBugReportService _bugReportService;
    private readonly IDiskMonitorService _diskMonitorService;

    public XisoSharpService(ILogger logger, IBugReportService bugReportService, IDiskMonitorService diskMonitorService)
    {
        _logger = logger;
        _bugReportService = bugReportService;
        _diskMonitorService = diskMonitorService;
    }

    public async Task<FileProcessingStatus> ConvertIsoToXisoAsync(string inputFile, string outputFolder,
        bool skipSystemUpdate, bool checkIntegrity, IProgress<BatchOperationProgress> progress,
        CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        _logger.LogMessage($"Converting '{fileName}' using XISOSharp...");

        if (!File.Exists(inputFile))
        {
            _logger.LogMessage($"[ERROR] Input file not found: {inputFile}");
            return FileProcessingStatus.Failed;
        }

        var outputFileName = Path.GetFileNameWithoutExtension(inputFile) + ".iso";
        var outputPath = Path.Combine(outputFolder, outputFileName);

        // XISOSharp reads the source and writes the output directly; converting a file
        // onto itself would destroy the source.
        if (XisoPaths.AreSamePath(inputFile, outputPath))
        {
            _logger.LogMessage($"[ERROR] The output file would overwrite the source file for '{fileName}'. " +
                               "Please choose a different output folder.");
            return FileProcessingStatus.Failed;
        }

        if (XisoReader.IsOptimizedImage(inputFile))
        {
            _logger.LogMessage($"'{fileName}' is already an optimized XISO. Skipping conversion.");
            return FileProcessingStatus.AlreadyOptimized;
        }

        var inputFileSize = new FileInfo(inputFile).Length;
        var outputCheck = CheckOutputDrive(inputFile, inputFileSize, outputFolder);
        if (outputCheck != null)
        {
            _logger.LogMessage($"[ERROR] {outputCheck}");
            return FileProcessingStatus.Failed;
        }

        try
        {
            Directory.CreateDirectory(outputFolder);
        }
        catch (Exception ex)
        {
            _logger.LogMessage($"[ERROR] Could not create the output folder '{outputFolder}': {ex.Message}");
            return FileProcessingStatus.Failed;
        }

        return await Task.Run(
            () => ConvertCore(inputFile, outputFolder, outputPath, skipSystemUpdate, checkIntegrity,
                progress, token), token);
    }

    private FileProcessingStatus ConvertCore(string inputFile, string outputFolder,
        string outputPath, bool skipSystemUpdate, bool checkIntegrity, IProgress<BatchOperationProgress> progress,
        CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        string? outIsoPath = null;

        var previousRemoveSystemUpdate = Logger.RemoveSystemUpdate;
        var previousForwardInfo = Logger.ForwardInfo;
        var previousForwardError = Logger.ForwardError;

        try
        {
            // The library exposes the $SystemUpdate filter and diagnostics as process-wide
            // static state. Conversions are serialized by the orchestrator, so save and
            // restore the previous values around this conversion.
            Logger.RemoveSystemUpdate = skipSystemUpdate;
            Logger.ForwardInfo = message => _logger.LogMessage($"  [xiso] {message.TrimEnd()}");
            Logger.ForwardError = message => _logger.LogMessage($"  [xiso] ERROR: {message.TrimEnd()}");

            var progressAdapter = new Progress<ProgressInfo>(info =>
            {
                switch (info.Type)
                {
                    case ProgressInfoType.FileCount:
                        progress.Report(new BatchOperationProgress { StatusText = $"Packing {info.Count} files..." });
                        break;
                    case ProgressInfoType.FileAdded:
                        progress.Report(new BatchOperationProgress { StatusText = $"Packing: {info.Path}" });
                        break;
                    case ProgressInfoType.FileProgress:
                        progress.Report(new BatchOperationProgress { StatusText = $"Writing: {info.Path}" });
                        break;
                    case ProgressInfoType.FinishedPacking:
                        progress.Report(new BatchOperationProgress { StatusText = "Finalizing output..." });
                        break;
                }
            });

            // outputName is intentionally null: XISOSharp names the rewritten image after the
            // source file with a .iso extension, which is exactly outputFileName here.
            var result = XisoReader.Rewrite(inputFile, outputFolder, out outIsoPath, token,
                progress: progressAdapter);

            var resultPath = string.IsNullOrEmpty(outIsoPath) ? outputPath : outIsoPath;

            if (result != 0 || !File.Exists(resultPath))
            {
                _logger.LogMessage($"[ERROR] XISOSharp could not convert '{fileName}' (result code {result}).");
                DeletePartialOutput(resultPath);
                return FileProcessingStatus.Failed;
            }

            if (checkIntegrity)
            {
                _logger.LogMessage("Verifying output XISO integrity...");
                var audit = XisoReader.AuditXiso(resultPath);
                if (!audit.IsValid)
                {
                    _logger.LogMessage(
                        $"[ERROR] Output XISO failed structural validation: {string.Join("; ", audit.Issues)}");
                    DeletePartialOutput(resultPath);
                    return FileProcessingStatus.Failed;
                }

                _logger.LogMessage(
                    $"Output XISO passed validation ({audit.FilesChecked} files, {audit.DirsChecked} directories).");
            }

            _logger.LogMessage($"Successfully converted '{fileName}' to XISO format.");
            return FileProcessingStatus.Converted;
        }
        catch (OperationCanceledException)
        {
            _logger.LogMessage($"Conversion of '{fileName}' was canceled. Cleaning up partial output...");
            DeletePartialOutput(outIsoPath ?? outputPath);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Not enough disk space to convert '{fileName}': {ex.Message}");
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDeviceIoError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] The drive reported a hardware I/O error while converting '{fileName}': {ex.Message}\n\n" +
                               "This usually means the source or output drive is failing, was disconnected, or has a hardware problem.\n" +
                               "Please check the drive connection and health (e.g. run chkdsk), then try again.");
            throw;
        }
        catch (Exception ex) when (PathHelper.IsNetworkError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Network error while converting '{fileName}': {ex.Message}\n\n" +
                               "Please try:\n" +
                               "1. Check that the network drive is still connected and accessible\n" +
                               "2. Copy the file to a local drive before processing\n" +
                               "3. Check your network connection stability");
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Drive or path not found for '{fileName}': {ex.Message}\n\n" +
                               "Please check that the drive is connected and the path exists.");
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Access denied while converting '{fileName}'.\n\n" +
                               "The output folder may be write-protected or require administrator rights.\n" +
                               "Please choose a different output folder or run the application as administrator.");
            return FileProcessingStatus.Failed;
        }
        catch (Exception ex) when (IsInvalidImageError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Failed to convert '{fileName}': {ex.Message}\n\n" +
                               "The file may not be a valid Xbox/Xbox 360 ISO image, may be corrupt, or contains a file too large for XISO.");
            return FileProcessingStatus.Failed;
        }
        catch (Exception ex)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.LogMessage($"[ERROR] Failed to convert '{fileName}': {ex.Message}");
            _ = _bugReportService.SendBugReportAsync($"Failed to convert '{fileName}'", ex);
            return FileProcessingStatus.Failed;
        }
        finally
        {
            Logger.RemoveSystemUpdate = previousRemoveSystemUpdate;
            Logger.ForwardInfo = previousForwardInfo;
            Logger.ForwardError = previousForwardError;
        }
    }

    /// <summary>
    /// Errors that indicate the input image itself is unsupported or corrupt rather
    /// than an application defect. XISOSharp's low-level reader reports truncated
    /// images as a plain IOException with a "Read error" message.
    /// </summary>
    private static bool IsInvalidImageError(Exception ex)
    {
        return ex is XisoFormatException or XisoEmptyException or XisoFileTooLargeException or InvalidDataException
            or ExtractErrorException or EndOfStreamException ||
               (ex is IOException ioException &&
                ioException.Message.StartsWith("Read error", StringComparison.OrdinalIgnoreCase));
    }

    private static void DeletePartialOutput(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Cleanup failure is non-fatal; the error is already reported to the user
        }
    }

    /// <summary>
    /// Validates that the output drive can hold the converted file before the conversion
    /// starts. Returns an error message when the output drive is full or uses a file system
    /// that cannot store files of this size (FAT32 4 GB limit); otherwise returns null.
    /// </summary>
    private string? CheckOutputDrive(string inputFile, long inputFileSize, string outputFolder)
    {
        try
        {
            var availableSpace = _diskMonitorService.GetAvailableFreeSpace(outputFolder);
            var requiredWithBuffer = inputFileSize + Math.Max(inputFileSize / 10, 200L * 1024 * 1024);

            if (availableSpace > 0 && availableSpace < requiredWithBuffer)
            {
                return $"Not enough disk space on the output drive for '{Path.GetFileName(inputFile)}'. " +
                       $"Required: {Formatter.FormatBytes(inputFileSize)}, Available: {Formatter.FormatBytes(availableSpace)}. " +
                       "Please free up disk space or select a different output folder.";
            }

            var fullPath = Path.GetFullPath(outputFolder);
            if (PathHelper.IsUncPath(fullPath)) return null;

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return null;

            var drive = new DriveInfo(root);
            if (drive.IsReady &&
                drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) &&
                inputFileSize > 4L * 1024 * 1024 * 1024 - 1)
            {
                return $"The output drive '{drive.Name}' uses FAT32, which cannot store files larger than 4 GB. " +
                       $"'{Path.GetFileName(inputFile)}' is {Formatter.FormatBytes(inputFileSize)}. " +
                       "Please use an NTFS or exFAT formatted output drive.";
            }
        }
        catch
        {
            // Pre-check failures are non-fatal; the conversion will surface real errors if they occur
        }

        return null;
    }
}
