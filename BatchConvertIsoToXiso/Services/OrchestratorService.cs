using System.IO;
using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using Serilog;
using XISOSharp;

namespace BatchConvertIsoToXiso.Services;

public class OrchestratorService : IOrchestratorService
{
    private readonly IFileExtractor _fileExtractor;
    private readonly IFileMover _fileMover;
    private readonly ILogger _logger;
    private readonly IXisoIntegrityService _integrityService;
    private readonly IXisoSharpService _xisoSharpService;
    private readonly IDiskMonitorService _diskMonitorService;

    private class ProcessingContext
    {
        public int GlobalFileIndex { get; set; } = 1;
    }

    public OrchestratorService(
        IFileExtractor fileExtractor,
        IFileMover fileMover,
        ILogger logger,
        IXisoIntegrityService integrityService,
        IXisoSharpService xisoSharpService,
        IDiskMonitorService diskMonitorService)
    {
        _fileExtractor = fileExtractor;
        _fileMover = fileMover;
        _logger = logger.ForContext<OrchestratorService>();
        _integrityService = integrityService;
        _xisoSharpService = xisoSharpService;
        _diskMonitorService = diskMonitorService;
    }

    #region Conversion Logic

    public Task ConvertAsync(
        string inputFolder,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        OutputFormat outputFormat,
        bool searchSubfolders,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token)
    {
        return RunWithErrorHandlingAsync("ConvertAsync", async () =>
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new IOException(
                    $"The input folder does not exist or is not accessible: '{inputFolder}'\n\n" +
                    "Possible causes:\n" +
                    "• The folder was deleted, moved, or renamed\n" +
                    "• The folder is on a network drive that is disconnected\n" +
                    "• The folder is a cloud placeholder (OneDrive, Dropbox) that hasn't been synced\n" +
                    "• The path contains characters that are not supported by the file system\n\n" +
                    "Please verify the folder exists and try again.");
            }

            _logger.Information("Starting conversion. Input: {InputFolder}, Output: {OutputFolder}", inputFolder,
                outputFolder);

            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = searchSubfolders
            };

            List<string> topLevelEntries = [];
            const int maxRetries = 3;
            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    topLevelEntries = await Task.Run(
                        () => Directory.GetFiles(inputFolder, "*.*", enumOptions)
                            .Where(SupportedFiles.IsConvertible).ToList(), token);
                    break;
                }
                catch (DirectoryNotFoundException ex)
                {
                    _logger.Information(ex, "Input folder not found: {InputFolder}", inputFolder);
                    throw new IOException(
                        $"The input folder was not found: '{inputFolder}'\n\n" +
                        "The folder may have been deleted, moved, or is a cloud placeholder that hasn't been synced.\n" +
                        $"Original error: {ex.Message}",
                        ex);
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    _logger.Information(ex, "Folder scan attempt {Attempt}/{MaxRetries} failed; retrying", attempt,
                        maxRetries);
                    await Task.Delay(attempt * 2000, token);
                }
            }

            if (topLevelEntries.Count == 0)
            {
                _logger.Information("No convertible files found in {InputFolder}", inputFolder);
                return;
            }

            _logger.Information("Found {FileCount} file(s) to process in {InputFolder}", topLevelEntries.Count,
                inputFolder);

            await ConvertEntriesCoreAsync(topLevelEntries, outputFolder, deleteOriginals, skipSystemUpdate,
                checkIntegrity, outputFormat, progress, onCloudRetryRequired, token);
        });
    }

    public Task ConvertFilesAsync(
        IReadOnlyList<string> files,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        OutputFormat outputFormat,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token)
    {
        return RunWithErrorHandlingAsync("ConvertFilesAsync", async () =>
        {
            var convertibleFiles = files.Where(SupportedFiles.IsConvertible).ToList();
            if (convertibleFiles.Count == 0)
            {
                _logger.Information("No convertible files selected for conversion.");
                return;
            }

            _logger.Information("Starting conversion of {FileCount} selected file(s). Output: {OutputFolder}",
                convertibleFiles.Count, outputFolder);

            await ConvertEntriesCoreAsync(convertibleFiles, outputFolder, deleteOriginals, skipSystemUpdate,
                checkIntegrity, outputFormat, progress, onCloudRetryRequired, token);
        });
    }

    private async Task ConvertEntriesCoreAsync(
        IReadOnlyList<string> entries,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        OutputFormat outputFormat,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token)
    {
        progress.Report(new BatchOperationProgress { TotalFiles = entries.Count });

        var tempFoldersToCleanUp = new List<string>();
        var context = new ProcessingContext();
        var topLevelProcessed = 0;

        try
        {
            foreach (var entryPath in entries)
            {
                token.ThrowIfCancellationRequested();

                // Check file existence on a background thread to avoid blocking UI for cloud/slow files
                var fileExists = await Task.Run(() => File.Exists(entryPath), token);
                if (!fileExists)
                {
                    progress.Report(new BatchOperationProgress
                    {
                        LogMessage = $"Error: Source file not found: {entryPath}. Skipping.", FailedCount = 1,
                        FailedPathToAdd = entryPath
                    });
                    _logger.Information("Source file not found, skipping: {FilePath}", entryPath);
                    topLevelProcessed++;
                    progress.Report(new BatchOperationProgress { ProcessedCount = topLevelProcessed });
                    continue;
                }

                var fileName = Path.GetFileName(entryPath);
                var extension = Path.GetExtension(entryPath).ToLowerInvariant();
                progress.Report(new BatchOperationProgress
                {
                    StatusText = $"Processing: {fileName}", CurrentDrive = PathHelper.GetDriveLetter(entryPath)
                });

                try
                {
                    switch (extension)
                    {
                        case ".iso":
                            var isoStatus = await ConvertFileInternalAsync(entryPath, outputFolder, deleteOriginals,
                                context.GlobalFileIndex++, skipSystemUpdate, checkIntegrity, outputFormat, progress,
                                onCloudRetryRequired, token);
                            ReportStatus(isoStatus, entryPath, progress);
                            break;

                        case ".zip" or ".7z" or ".rar":
                            await ProcessArchiveAsync(entryPath, outputFolder, deleteOriginals, skipSystemUpdate,
                                checkIntegrity, outputFormat, context, tempFoldersToCleanUp, progress,
                                onCloudRetryRequired, token);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("Conversion canceled while processing {FileName}", fileName);
                    throw;
                }
                catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
                {
                    // Stop the batch — no point continuing without disk space
                    progress.Report(new BatchOperationProgress
                    {
                        LogMessage =
                            "ERROR: Not enough disk space on the output drive. Batch operation stopped. Please free up disk space and try again.",
                        FailedCount = 1,
                        FailedPathToAdd = entryPath
                    });
                    _logger.Information(ex, "Not enough disk space; batch conversion stopped on {FileName}",
                        fileName);
                    throw;
                }
                catch (Exception ex) when (IsFatalEnvironmentalError(ex))
                {
                    // Stop the batch — no point continuing if the output drive or network is disconnected
                    progress.Report(new BatchOperationProgress
                    {
                        LogMessage =
                            $"FATAL ERROR: The output device or path is not available: {ex.Message}. Batch operation stopped.",
                        FailedCount = 1,
                        FailedPathToAdd = entryPath
                    });
                    _logger.Information(ex,
                        "Output device or path unavailable; batch conversion stopped on {FileName}",
                        fileName);
                    throw;
                }
                catch (Exception ex)
                {
                    // Provide user-friendly message for corrupt archives
                    string logMessage;
                    if (ex.Message.Contains("End of stream reached", StringComparison.OrdinalIgnoreCase))
                    {
                        logMessage =
                            $"ERROR: {fileName} appears to be corrupt or incomplete. The file may have been damaged during download or transfer. Please re-download the archive and try again.";
                    }
                    else
                    {
                        logMessage = $"Critical error processing {fileName}: {ex.Message}";
                    }

                    progress.Report(new BatchOperationProgress
                        { LogMessage = logMessage, FailedCount = 1, FailedPathToAdd = entryPath });

                    // Filter environmental errors (disconnected drives, network issues, etc.)
                    var isEnvironmentalError = IsFatalEnvironmentalError(ex) || PathHelper.IsNetworkError(ex);

                    // Filter common archive errors (corruption, incomplete downloads, etc.)
                    var isArchiveError = ex.Message.Contains("Data error", StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("Invalid archive",
                                             StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("Unsupported archive",
                                             StringComparison.OrdinalIgnoreCase) ||
                                         ex.Message.Contains("End of stream reached",
                                             StringComparison.OrdinalIgnoreCase);

                    if (!isEnvironmentalError && !isArchiveError)
                    {
                        _logger.Error(ex, "Orchestrator error on {FileName}", fileName);
                    }
                    else
                    {
                        _logger.Information(ex, "Handled processing error on {FileName}", fileName);
                    }
                }

                topLevelProcessed++;
                progress.Report(new BatchOperationProgress { ProcessedCount = topLevelProcessed });
            }
        }
        finally
        {
            await CleanupTempFoldersAsync(tempFoldersToCleanUp, progress, token);
        }

        _logger.Information("Batch conversion completed. Processed {ProcessedCount} of {TotalFiles} file(s)",
            topLevelProcessed, entries.Count);
    }

    private async Task RunWithErrorHandlingAsync(string operationName, Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("OrchestratorService.{OperationName} canceled", operationName);
            throw;
        }
        catch (Exception ex)
        {
            // The caller (MainWindow) is responsible for reporting unexpected failures;
            // log here with context but avoid duplicate bug reports.
            if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) || IsFatalEnvironmentalError(ex) ||
                ex is IOException)
            {
                _logger.Information(ex, "OrchestratorService.{OperationName} stopped due to an environmental error",
                    operationName);
            }
            else
            {
                _logger.Information(ex, "OrchestratorService.{OperationName} failed", operationName);
            }

            throw;
        }
    }

    private string ResolveTempDirectory(long requiredSize, string tempSubfolder)
    {
        return PathHelper.ResolveTempDirectory(requiredSize, tempSubfolder, _diskMonitorService);
    }

    private async Task ProcessArchiveAsync(string archivePath, string outputFolder, bool deleteOriginal,
        bool skipUpdate, bool checkIntegrity, OutputFormat outputFormat, ProcessingContext context,
        List<string> tempFolders, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> cloudRetry, CancellationToken token)
    {
        string tempDir;
        try
        {
            progress.Report(new BatchOperationProgress { LogMessage = "Analyzing archive for required space..." });
            var (totalSize, fileCount) = await _fileExtractor.GetArchiveInfoAsync(archivePath, token);
            tempDir = ResolveTempDirectory(totalSize, "BatchConvertIsoToXiso_Extract");
            progress.Report(new BatchOperationProgress
            {
                LogMessage =
                    $"Archive contains {fileCount} files ({Formatter.FormatBytes(totalSize)} uncompressed). Extracting to: {Path.GetDirectoryName(tempDir)}"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Archive processing canceled: {ArchivePath}", archivePath);
            throw;
        }
        catch (IOException ex) when (ex.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("Not enough disk space", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Information(ex, "Not enough disk space to extract archive: {ArchivePath}", archivePath);
            throw;
        }
        catch (Exception ex)
        {
            // If we can't analyze the archive, fall back to default temp and let ExtractArchiveAsync handle it
            progress.Report(new BatchOperationProgress
                { LogMessage = $"Could not analyze archive: {ex.Message}. Using default temp path." });
            _logger.Warning(ex, "Could not analyze archive {ArchivePath}; using default temp path", archivePath);
            tempDir = Path.Combine(Path.GetTempPath(), "BatchConvertIsoToXiso_Extract", Guid.NewGuid().ToString());
        }

        tempFolders.Add(tempDir);

        bool extracted;
        var internalFail = false;
        var internalSuccess = false;

        try
        {
            Directory.CreateDirectory(tempDir);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            extracted = await _fileExtractor.ExtractArchiveAsync(archivePath, tempDir, linkedCts.Token);

            if (extracted)
            {
                var files = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                    .Where(SupportedFiles.IsIso).ToList();

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    var status = await ConvertFileInternalAsync(file, outputFolder, false, context.GlobalFileIndex++,
                        skipUpdate, checkIntegrity, outputFormat, progress, cloudRetry, token);

                    switch (status)
                    {
                        case FileProcessingStatus.Converted:
                            internalSuccess = true;
                            break;
                        case FileProcessingStatus.Failed:
                            internalFail = true;
                            break;
                    }
                }
            }
            else
            {
                internalFail = true;
            }
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions to stop the batch
            _logger.Debug("Archive extraction canceled: {ArchivePath}", archivePath);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
        {
            // Stop the batch — no point continuing without disk space
            _logger.Information(ex, "Not enough disk space while extracting archive: {ArchivePath}", archivePath);
            throw;
        }
        catch (Exception ex) when (IsFatalEnvironmentalError(ex))
        {
            // Stop the batch — no point continuing if the output drive or network is disconnected
            _logger.Information(ex, "Output device or path unavailable while extracting archive: {ArchivePath}",
                archivePath);
            throw;
        }
        catch (Exception ex)
        {
            // Mark as failed, but don't stop the batch processing
            // Error has already been logged by the FileExtractor
            _logger.Debug(ex, "Archive processing failed for {ArchivePath}; continuing batch", archivePath);
            internalFail = true;
            extracted = false;
        }
        finally
        {
            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 1000, _logger, token);
            tempFolders.Remove(tempDir);
        }

        if (internalFail || !extracted)
            progress.Report(new BatchOperationProgress { FailedCount = 1, FailedPathToAdd = archivePath });
        else if (internalSuccess) progress.Report(new BatchOperationProgress { SuccessCount = 1 });
        else progress.Report(new BatchOperationProgress { SkippedCount = 1 });

        // Only remove the archive when at least one file was actually converted and nothing
        // failed. If every entry was skipped (for example already-optimized images), deleting
        // the archive would destroy the only copy of the content.
        if (deleteOriginal && extracted && internalSuccess && !internalFail)
        {
            try
            {
                File.Delete(archivePath);
            }
            catch (Exception ex)
            {
                /* ignore */
                _logger.Debug(ex, "Could not delete original archive {ArchivePath}", archivePath);
            }
        }
    }

    private async Task<FileProcessingStatus> ConvertFileInternalAsync(string inputFile, string outputFolder,
        bool deleteOriginal, int fileIndex, bool skipSystemUpdate, bool checkIntegrity, OutputFormat outputFormat,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired, CancellationToken token)
    {
        var originalFileName = Path.GetFileName(inputFile);
        string? localTempWorkingDir = null;

        try
        {
            // Handle cloud files first: copy to local temp when the source is not directly readable
            var sourcePath = inputFile;

            try
            {
                // Simple check if file is accessible (triggers cloud download if hydration is automatic, or fails)
                // Run on background thread to avoid blocking UI for cloud/slow files
                await Task.Run(() =>
                {
                    using var stream = File.OpenRead(inputFile);
                }, token);
            }
            catch (IOException)
            {
                _logger.Debug("File {FileName} is not directly readable; copying to a local temp file",
                    originalFileName);
                try
                {
                    var fileSize = new FileInfo(inputFile).Length;
                    localTempWorkingDir = ResolveTempDirectory(fileSize, "BatchConvertIsoToXiso_Convert");
                }
                catch (IOException ex) when (PathHelper.IsDiskSpaceError(ex))
                {
                    _logger.Information(ex, "Not enough disk space while preparing conversion of {FileName}",
                        originalFileName);
                    throw;
                }
                catch (Exception ex)
                {
                    progress.Report(new BatchOperationProgress
                    {
                        LogMessage =
                            $"Could not resolve temp directory for copy: {ex.Message}. Falling back to default temp path."
                    });
                    _logger.Warning(ex,
                        "Could not resolve temp directory for copying {FileName}; using default temp path",
                        originalFileName);
                    localTempWorkingDir = Path.Combine(Path.GetTempPath(), "BatchConvertIsoToXiso_Convert",
                        Guid.NewGuid().ToString());
                }

                Directory.CreateDirectory(localTempWorkingDir);
                var simpleFilename = GenerateFilename.GenerateSimpleFilename(fileIndex);
                var localTempIsoPath = Path.Combine(localTempWorkingDir, simpleFilename);

                progress.Report(new BatchOperationProgress
                {
                    LogMessage = $"File '{originalFileName}': Copying to local temp...",
                    CurrentDrive = PathHelper.GetDriveLetter(Path.GetTempPath())
                });

                if (!await CopyFileWithCloudRetryAsync(inputFile, localTempIsoPath, onCloudRetryRequired, progress,
                        token))
                {
                    return FileProcessingStatus.Failed;
                }

                sourcePath = localTempIsoPath;
            }

            Directory.CreateDirectory(outputFolder);

            // Generate the output filename for the requested format
            var outputExtension = outputFormat switch
            {
                OutputFormat.Zar => ".zar",
                OutputFormat.Cso => ".cso",
                _ => ".iso"
            };
            var outputFileName = Path.GetFileNameWithoutExtension(originalFileName) + outputExtension;
            var destinationPath = Path.Combine(outputFolder, outputFileName);

            // Never delete the source file: converting a file onto itself would destroy it
            if (XisoPaths.AreSamePath(sourcePath, destinationPath))
            {
                progress.Report(new BatchOperationProgress
                {
                    LogMessage =
                        $"Error: The output file would overwrite the source file '{originalFileName}'. Please choose a different output folder."
                });
                return FileProcessingStatus.Failed;
            }

            var formatLabel = outputFormat switch
            {
                OutputFormat.Zar => "ZAR",
                OutputFormat.Cso => "CSO",
                _ => "optimized XISO"
            };

            progress.Report(new BatchOperationProgress
            {
                LogMessage = $"File '{originalFileName}': Converting to {formatLabel} with XISOSharp...",
                CurrentDrive = PathHelper.GetDriveLetter(outputFolder)
            });

            // Pass the user-visible output name explicitly: the working copy may be a
            // temporary file, but the converted result must keep the original name.
            var status = await _xisoSharpService.ConvertIsoAsync(sourcePath, outputFolder, outputFileName,
                outputFormat, skipSystemUpdate, checkIntegrity, progress, token);

            if (status == FileProcessingStatus.AlreadyOptimized) return FileProcessingStatus.Skipped;
            if (status != FileProcessingStatus.Converted) return FileProcessingStatus.Failed;

            if (deleteOriginal)
            {
                try
                {
                    // Even when the conversion ran from a temporary working copy (cloud files),
                    // the user-visible original is the file that "Replace Originals" must remove.
                    File.Delete(inputFile);
                    progress.Report(new BatchOperationProgress
                        { LogMessage = $"Deleted original: {originalFileName}" });
                }
                catch (Exception ex)
                {
                    progress.Report(new BatchOperationProgress
                        { LogMessage = $"Warning: Could not delete original {originalFileName}: {ex.Message}" });
                    _logger.Warning(ex, "Could not delete original file {FileName}", originalFileName);
                }
            }

            return FileProcessingStatus.Converted;
        }
        finally
        {
            if (localTempWorkingDir != null)
                await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(localTempWorkingDir, 5, 1000, _logger,
                    token);
        }
    }

    #endregion

    #region Testing Logic

    public Task TestAsync(string inputFolder, bool moveSuccessful, bool moveFailed, bool searchSubfolders,
        bool performDeepScan, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired, CancellationToken token)
    {
        return RunWithErrorHandlingAsync("TestAsync", async () =>
        {
            if (!Directory.Exists(inputFolder))
            {
                throw new IOException(
                    $"The input folder does not exist or is not accessible: '{inputFolder}'\n\n" +
                    "Possible causes:\n" +
                    "• The folder was deleted, moved, or renamed\n" +
                    "• The folder is on a network drive that is disconnected\n" +
                    "• The folder is a cloud placeholder (OneDrive, Dropbox) that hasn't been synced\n" +
                    "• The path contains characters that are not supported by the file system\n\n" +
                    "Please verify the folder exists and try again.");
            }

            _logger.Information("Starting image test. Input: {InputFolder}", inputFolder);

            var enumOptions = new EnumerationOptions
                { IgnoreInaccessible = true, RecurseSubdirectories = searchSubfolders };

            List<string> imageFiles;
            try
            {
                imageFiles = await Task.Run(() => Directory.GetFiles(inputFolder, "*.*", enumOptions)
                    .Where(SupportedFiles.IsTestable).ToList(), token);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Information(ex, "Input folder not found: {InputFolder}", inputFolder);
                throw new IOException(
                    $"The input folder was not found: '{inputFolder}'\n\n" +
                    "The folder may have been deleted, moved, or is a cloud placeholder that hasn't been synced.\n" +
                    $"Original error: {ex.Message}",
                    ex);
            }

            if (imageFiles.Count == 0)
            {
                _logger.Information("No supported image files found in {InputFolder}", inputFolder);
                return;
            }

            _logger.Information("Found {FileCount} image file(s) to test in {InputFolder}", imageFiles.Count,
                inputFolder);

            await TestEntriesCoreAsync(inputFolder, imageFiles, moveSuccessful, moveFailed, performDeepScan, progress,
                onCloudRetryRequired, token);
        });
    }

    public Task TestFilesAsync(string inputFolder, IReadOnlyList<string> files, bool moveSuccessful, bool moveFailed,
        bool performDeepScan, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired, CancellationToken token)
    {
        return RunWithErrorHandlingAsync("TestFilesAsync", async () =>
        {
            var imageFiles = files.Where(SupportedFiles.IsTestable).ToList();
            if (imageFiles.Count == 0)
            {
                _logger.Information("No supported image files selected for testing.");
                return;
            }

            _logger.Information("Starting image test of {FileCount} selected file(s). Input: {InputFolder}",
                imageFiles.Count, inputFolder);

            await TestEntriesCoreAsync(inputFolder, imageFiles, moveSuccessful, moveFailed, performDeepScan, progress,
                onCloudRetryRequired, token);
        });
    }

    private async Task TestEntriesCoreAsync(string inputFolder, IReadOnlyList<string> imageFiles, bool moveSuccessful,
        bool moveFailed, bool performDeepScan, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired, CancellationToken token)
    {
        progress.Report(new BatchOperationProgress { TotalFiles = imageFiles.Count });
        var successFolder = Path.Combine(inputFolder, "_success");
        var failedFolder = Path.Combine(inputFolder, "_failed");

        var processed = 0;
        var fileIndex = 1;

        foreach (var imagePath in imageFiles)
        {
            token.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(imagePath);
            progress.Report(new BatchOperationProgress
            {
                StatusText = $"Testing: {fileName}", CurrentDrive = PathHelper.GetDriveLetter(Path.GetTempPath())
            });

            var result = await TestSingleIsoInternalAsync(imagePath, fileIndex++, performDeepScan,
                onCloudRetryRequired,
                progress, token);

            if (result == IsoTestResultStatus.Passed)
            {
                progress.Report(new BatchOperationProgress
                    { SuccessCount = 1, LogMessage = $"  SUCCESS: '{fileName}' passed test." });
                if (moveSuccessful)
                    await MoveTestedImageAsync(imagePath, successFolder, "successfully tested", token);
            }
            else
            {
                progress.Report(new BatchOperationProgress
                {
                    FailedCount = 1, FailedPathToAdd = imagePath, LogMessage = $"  FAILURE: '{fileName}' failed test."
                });
                _logger.Information("Test failed for {FileName}", fileName);
                if (moveFailed) await MoveTestedImageAsync(imagePath, failedFolder, "failed test", token);
            }

            processed++;
            progress.Report(new BatchOperationProgress { ProcessedCount = processed });
        }

        _logger.Information("Image test completed. Processed {ProcessedCount} file(s)", processed);
    }

    /// <summary>
    /// Moves a tested image to the success/failed folder. A split CISO set is opened
    /// through its first part (<c>game.1.cso</c>), so the continuation parts
    /// (<c>game.2.cso</c>, …) travel with it — moving part 1 alone would break the set.
    /// </summary>
    private async Task MoveTestedImageAsync(string imagePath, string destinationFolder, string moveReason,
        CancellationToken token)
    {
        await _fileMover.MoveTestedFileAsync(imagePath, destinationFolder, moveReason, token);

        if (!imagePath.EndsWith(".1.cso", StringComparison.OrdinalIgnoreCase)) return;

        // Part 1 was not moved (destination already exists or the move failed): leave the
        // set together instead of stranding the continuation parts.
        if (File.Exists(imagePath)) return;

        var basePath = imagePath[..^".1.cso".Length];
        for (var part = 2; ; part++)
        {
            var partPath = $"{basePath}.{part}.cso";
            if (!File.Exists(partPath)) break;

            await _fileMover.MoveTestedFileAsync(partPath, destinationFolder, moveReason, token);
        }
    }

    private async Task<IsoTestResultStatus> TestSingleIsoInternalAsync(string isoPath, int index, bool performDeepScan,
        Func<string, Task<CloudRetryResult>> cloudRetry, IProgress<BatchOperationProgress> progress,
        CancellationToken token)
    {
        // 1. Handle Cloud/OneDrive files (download to temp if necessary)
        var pathToCheck = isoPath;
        string? tempCloudCopy = null;

        try
        {
            // Simple check if file is accessible (triggers cloud download if hydration is automatic, or fails)
            // Run on background thread to avoid blocking UI for cloud/slow files
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(isoPath);
            }, token);
        }
        catch (IOException)
        {
            // Likely cloud file issue, use existing copy logic
            _logger.Debug("Image {ImagePath} is not directly readable; copying to local temp", isoPath);

            // The temp copy must keep the original extension: the integrity service
            // routes by extension (plain ISO/CISO vs ZAR).
            var simpleName = Path.ChangeExtension(GenerateFilename.GenerateSimpleFilename(index),
                Path.GetExtension(isoPath));
            long estimatedSize = 0;
            try
            {
                estimatedSize = new FileInfo(isoPath).Length;
            }
            catch (Exception ex)
            {
                // ignored
                _logger.Debug(ex, "Could not determine size of image {ImagePath}", isoPath);
            }

            var tempDir = ResolveTempDirectory(estimatedSize, "BatchConvertIsoToXiso_Test");
            Directory.CreateDirectory(tempDir);
            tempCloudCopy = Path.Combine(tempDir, simpleName);

            if (await CopyFileWithCloudRetryAsync(isoPath, tempCloudCopy, cloudRetry, progress, token))
            {
                pathToCheck = tempCloudCopy;
            }
            else
            {
                return IsoTestResultStatus.Failed;
            }
        }

        try
        {
            progress.Report(new BatchOperationProgress { LogMessage = "  Verifying image structure and readability..." });

            var passed = await _integrityService.TestIsoIntegrityAsync(pathToCheck, performDeepScan, progress, token);

            return passed ? IsoTestResultStatus.Passed : IsoTestResultStatus.Failed;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Image test canceled: {ImagePath}", isoPath);
            throw;
        }
        catch (Exception ex)
        {
            progress.Report(new BatchOperationProgress { LogMessage = $"  Test Error: {ex.Message}" });
            _logger.Information(ex, "Test failed for {ImagePath}", isoPath);
            return IsoTestResultStatus.Failed;
        }
        finally
        {
            if (tempCloudCopy != null && File.Exists(tempCloudCopy))
            {
                try
                {
                    // ReSharper disable once NullableWarningSuppressionIsUsed
                    Directory.Delete(Path.GetDirectoryName(tempCloudCopy)!, true);
                }
                catch (Exception ex)
                {
                    /* ignore cleanup errors */
                    _logger.Debug(ex, "Could not delete test temp copy {TempPath}", tempCloudCopy);
                }
            }
        }
    }

    #endregion

    #region Helpers

    internal static bool IsFatalEnvironmentalError(Exception ex)
    {
        if (PathHelper.IsDeviceIoError(ex)) return true;

        if (ex is IOException ioEx)
        {
            var hResult = ioEx.HResult & 0xFFFF;
            // 0x15: ERROR_NOT_READY, 0x03: ERROR_PATH_NOT_FOUND, 0x0F: ERROR_INVALID_DRIVE,
            // 0x37: ERROR_DEV_NOT_EXIST, 0x40: ERROR_NETNAME_DELETED
            if (hResult is 0x15 or 0x03 or 0x0F or 0x37 or 0x40) return true;
        }

        if (ex is DirectoryNotFoundException) return true;

        return ex is IOException ioEx2 &&
               (ioEx2.Message.Contains("device", StringComparison.OrdinalIgnoreCase) ||
                ioEx2.Message.Contains("network name is no longer available", StringComparison.OrdinalIgnoreCase) ||
                ioEx2.Message.Contains("Zařízení není připraveno",
                    StringComparison.OrdinalIgnoreCase)); // Czech translation from bug reports
    }

    private static void ReportStatus(FileProcessingStatus status, string path,
        IProgress<BatchOperationProgress> progress)
    {
        switch (status)
        {
            case FileProcessingStatus.Converted:
                progress.Report(new BatchOperationProgress { SuccessCount = 1 }); break;
            case FileProcessingStatus.AlreadyOptimized:
            case FileProcessingStatus.Skipped: progress.Report(new BatchOperationProgress { SkippedCount = 1 }); break;
            case FileProcessingStatus.Failed:
                progress.Report(new BatchOperationProgress { FailedCount = 1, FailedPathToAdd = path }); break;
        }
    }

    private async Task<bool> CopyFileWithCloudRetryAsync(string source, string dest,
        Func<string, Task<CloudRetryResult>> cloudRetry, IProgress<BatchOperationProgress> progress,
        CancellationToken token)
    {
        const int maxNetworkRetries = 5;
        const int initialRetryDelayMs = 500;
        var networkRetryCount = 0;

        while (true)
        {
            try
            {
                await Task.Run(() => File.Copy(source, dest, true), token);
                return true;
            }
            catch (IOException ex) when (PathHelper.IsNetworkError(ex) && networkRetryCount < maxNetworkRetries)
            {
                networkRetryCount++;
                var delayMs = initialRetryDelayMs * (int)Math.Pow(2, networkRetryCount - 1);
                progress.Report(new BatchOperationProgress
                {
                    LogMessage =
                        $"Network error detected, retrying in {delayMs}ms... (attempt {networkRetryCount}/{maxNetworkRetries})"
                });
                _logger.Information(ex,
                    "Network error copying {Source}; retrying in {DelayMs}ms (attempt {Attempt}/{MaxRetries})", source,
                    delayMs, networkRetryCount, maxNetworkRetries);
                await Task.Delay(delayMs, token);
            }
            catch (IOException ex) when (ex.Message.Contains("cloud operation", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug(ex, "Cloud operation required while copying {Source}", source);
                var result = await cloudRetry(Path.GetFileName(source));
                switch (result)
                {
                    case CloudRetryResult.Retry:
                        continue;
                    case CloudRetryResult.Cancel:
                        _logger.Debug("Cloud retry canceled by the user for {Source}", source);
                        throw new OperationCanceledException();
                    default:
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("File copy canceled: {Source}", source);
                throw;
            }
            catch (Exception ex)
            {
                progress.Report(new BatchOperationProgress { LogMessage = $"Copy failed: {ex.Message}" });
                if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) || IsFatalEnvironmentalError(ex))
                {
                    _logger.Information(ex, "Copy failed for {Source} due to an environmental error", source);
                }
                else
                {
                    _logger.Warning(ex, "Copy failed for {Source}", source);
                }

                return false;
            }
        }
    }

    private async Task CleanupTempFoldersAsync(List<string> folders, IProgress<BatchOperationProgress> progress,
        CancellationToken token)
    {
        if (folders.Count == 0) return;

        progress.Report(new BatchOperationProgress { LogMessage = "Cleaning up temporary folders..." });
        foreach (var folder in folders.ToList())
        {
            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(folder, 5, 1000, _logger, token);
        }
    }

    #endregion
}