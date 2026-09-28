using System.IO;
using System.Runtime.InteropServices;
using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using Serilog;
using XISOSharp;

namespace BatchConvertIsoToXiso.Services;

public class OrchestratorService : IOrchestratorService
{
    private readonly IExternalToolService _externalToolService;
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
        IExternalToolService externalToolService,
        IFileExtractor fileExtractor,
        IFileMover fileMover,
        ILogger logger,
        IXisoIntegrityService integrityService,
        IXisoSharpService xisoSharpService,
        IDiskMonitorService diskMonitorService)
    {
        _externalToolService = externalToolService;
        _fileExtractor = fileExtractor;
        _fileMover = fileMover;
        _logger = logger.ForContext<OrchestratorService>();
        _integrityService = integrityService;
        _xisoSharpService = xisoSharpService;
        _diskMonitorService = diskMonitorService;
    }

    #region Conversion Logic

    public async Task ConvertAsync(
        string inputFolder,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        bool searchSubfolders,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token)
    {
        try
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
                    topLevelEntries = await Task.Run(() => Directory.GetFiles(inputFolder, "*.*", enumOptions)
                        .Where(static f =>
                        {
                            var ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext is ".iso" or ".zip" or ".7z" or ".rar" or ".cue";
                        }).ToList(), token);
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

            progress.Report(new BatchOperationProgress { TotalFiles = topLevelEntries.Count });

            var tempFoldersToCleanUp = new List<string>();
            var context = new ProcessingContext();
            var topLevelProcessed = 0;

            try
            {
                foreach (var entryPath in topLevelEntries)
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
                                    context.GlobalFileIndex++, skipSystemUpdate, checkIntegrity, progress,
                                    onCloudRetryRequired, token);
                                ReportStatus(isoStatus, entryPath, progress);
                                break;

                            case ".zip" or ".7z" or ".rar":
                                await ProcessArchiveAsync(entryPath, outputFolder, deleteOriginals, skipSystemUpdate,
                                    checkIntegrity, context, tempFoldersToCleanUp, progress, onCloudRetryRequired,
                                    token);
                                break;

                            case ".cue":
                                await ProcessCueAsync(entryPath, outputFolder, deleteOriginals, skipSystemUpdate,
                                    checkIntegrity, context, tempFoldersToCleanUp, progress, onCloudRetryRequired,
                                    token);
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
                topLevelProcessed, topLevelEntries.Count);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("OrchestratorService.ConvertAsync canceled");
            throw;
        }
        catch (Exception ex)
        {
            // The caller (MainWindow) is responsible for reporting unexpected failures;
            // log here with context but avoid duplicate bug reports.
            if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) || IsFatalEnvironmentalError(ex) ||
                ex is IOException)
            {
                _logger.Information(ex, "OrchestratorService.ConvertAsync stopped due to an environmental error");
            }
            else
            {
                _logger.Information(ex, "OrchestratorService.ConvertAsync failed");
            }

            throw;
        }
    }

    private string ResolveTempDirectory(long requiredSize, string tempSubfolder)
    {
        return PathHelper.ResolveTempDirectory(requiredSize, tempSubfolder, _diskMonitorService);
    }

    private async Task ProcessArchiveAsync(string archivePath, string outputFolder, bool deleteOriginal,
        bool skipUpdate, bool checkIntegrity, ProcessingContext context,
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
                    .Where(static f => Path.GetExtension(f).ToLowerInvariant() is ".iso" or ".cue").ToList();

                foreach (var file in files)
                {
                    token.ThrowIfCancellationRequested();
                    FileProcessingStatus status;
                    if (Path.GetExtension(file).Equals(".iso", StringComparison.OrdinalIgnoreCase))
                    {
                        status = await ConvertFileInternalAsync(file, outputFolder, false, context.GlobalFileIndex++,
                            skipUpdate, checkIntegrity, progress, cloudRetry, token);
                    }
                    else
                    {
                        status = await ProcessCueInternalAsync(file, outputFolder, false, skipUpdate, checkIntegrity,
                            context, tempFolders, progress, cloudRetry, token);
                    }

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

    private async Task ProcessCueAsync(string cuePath, string outputFolder, bool deleteOriginal, bool skipUpdate,
        bool checkIntegrity, ProcessingContext context, List<string> tempFolders,
        IProgress<BatchOperationProgress> progress, Func<string, Task<CloudRetryResult>> cloudRetry,
        CancellationToken token)
    {
        var status = await ProcessCueInternalAsync(cuePath, outputFolder, deleteOriginal, skipUpdate, checkIntegrity,
            context, tempFolders, progress, cloudRetry, token);
        ReportStatus(status, cuePath, progress);
    }

    private async Task<FileProcessingStatus> ProcessCueInternalAsync(string cuePath, string outputFolder,
        bool deleteOriginal, bool skipUpdate, bool checkIntegrity,
        ProcessingContext context, List<string> tempFolders, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> cloudRetry, CancellationToken token)
    {
        // bchunk.exe is x64-only; the ARM64 bundle intentionally does not ship it.
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            progress.Report(new BatchOperationProgress
            {
                LogMessage =
                    $"Skipping '{Path.GetFileName(cuePath)}': CUE/BIN conversion requires the x64-only bchunk.exe, which is not supported on ARM64."
            });
            return FileProcessingStatus.Skipped;
        }

        long estimatedCueSize = 0;
        try
        {
            estimatedCueSize = new FileInfo(cuePath).Length;
            var binFiles = GetReferencedBinFilesFromCue(cuePath, _logger);
            foreach (var binFile in binFiles)
            {
                if (File.Exists(binFile))
                {
                    estimatedCueSize += new FileInfo(binFile).Length;
                }
            }
        }
        catch (Exception ex)
        {
            // If we can't estimate, fall back to default temp path
            _logger.Debug(ex, "Could not estimate size of CUE/BIN files for {CuePath}", cuePath);
        }

        string tempCueDir;
        try
        {
            tempCueDir = ResolveTempDirectory(estimatedCueSize, "BatchConvertIsoToXiso_CueBin");
        }
        catch (IOException ex) when (ex.Message.Contains("not enough space", StringComparison.OrdinalIgnoreCase) ||
                                     ex.Message.Contains("Not enough disk space", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Information(ex, "Not enough disk space to convert CUE/BIN: {CuePath}", cuePath);
            throw;
        }
        catch (Exception ex)
        {
            progress.Report(new BatchOperationProgress
                { LogMessage = $"Could not resolve temp directory for CUE: {ex.Message}. Using default temp path." });
            _logger.Warning(ex, "Could not resolve temp directory for CUE {CuePath}; using default temp path", cuePath);
            tempCueDir = Path.Combine(Path.GetTempPath(), "BatchConvertIsoToXiso_CueBin", Guid.NewGuid().ToString());
        }

        tempFolders.Add(tempCueDir);
        try
        {
            Directory.CreateDirectory(tempCueDir);
            var tempIso = await _externalToolService.ConvertCueBinToIsoAsync(cuePath, tempCueDir, token);
            if (tempIso != null && File.Exists(tempIso))
            {
                var status = await ConvertFileInternalAsync(tempIso, outputFolder, false, context.GlobalFileIndex++,
                    skipUpdate, checkIntegrity, progress, cloudRetry, token);

                // Remove the CUE/BIN sources only when a converted file was actually produced;
                // a skipped conversion must never delete the originals.
                if (deleteOriginal && status == FileProcessingStatus.Converted)
                {
                    try
                    {
                        // Parse the CUE file to find and delete only the referenced BIN files
                        // This MUST be done before deleting the CUE file
                        var referencedBinFiles = GetReferencedBinFilesFromCue(cuePath, _logger);

                        // Delete only the specific CUE file being processed
                        File.Delete(cuePath);

                        foreach (var binFile in referencedBinFiles)
                        {
                            if (File.Exists(binFile))
                            {
                                File.Delete(binFile);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // ignored
                        _logger.Debug(ex, "Could not delete original CUE/BIN files for {CuePath}", cuePath);
                    }
                }

                return status;
            }

            return FileProcessingStatus.Failed;
        }
        finally
        {
            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempCueDir, 3, 1000, _logger, token);
            tempFolders.Remove(tempCueDir);
        }
    }

    private async Task<FileProcessingStatus> ConvertFileInternalAsync(string inputFile, string outputFolder,
        bool deleteOriginal, int fileIndex, bool skipSystemUpdate, bool checkIntegrity,
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

            // Generate output filename with .iso extension
            var outputFileName = Path.GetFileNameWithoutExtension(originalFileName) + ".iso";
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

            progress.Report(new BatchOperationProgress
            {
                LogMessage = $"File '{originalFileName}': Converting to optimized XISO with XISOSharp...",
                CurrentDrive = PathHelper.GetDriveLetter(outputFolder)
            });

            // Pass the user-visible output name explicitly: the working copy may be a
            // temporary file, but the converted result must keep the original name.
            var status = await _xisoSharpService.ConvertIsoToXisoAsync(sourcePath, outputFolder, outputFileName,
                skipSystemUpdate, checkIntegrity, progress, token);

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

    public async Task TestAsync(string inputFolder, bool moveSuccessful, bool moveFailed, bool searchSubfolders,
        bool performDeepScan, IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired, CancellationToken token)
    {
        try
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

            _logger.Information("Starting ISO test. Input: {InputFolder}", inputFolder);

            var enumOptions = new EnumerationOptions
                { IgnoreInaccessible = true, RecurseSubdirectories = searchSubfolders };

            List<string> isoFiles;
            try
            {
                isoFiles = await Task.Run(() => Directory.GetFiles(inputFolder, "*.iso", enumOptions).ToList(), token);
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

            if (isoFiles.Count == 0)
            {
                _logger.Information("No ISO files found in {InputFolder}", inputFolder);
                return;
            }

            _logger.Information("Found {FileCount} ISO file(s) to test in {InputFolder}", isoFiles.Count, inputFolder);

            progress.Report(new BatchOperationProgress { TotalFiles = isoFiles.Count });
            var successFolder = Path.Combine(inputFolder, "_success");
            var failedFolder = Path.Combine(inputFolder, "_failed");

            var processed = 0;
            var fileIndex = 1;

            foreach (var isoPath in isoFiles)
            {
                token.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(isoPath);
                progress.Report(new BatchOperationProgress
                {
                    StatusText = $"Testing: {fileName}", CurrentDrive = PathHelper.GetDriveLetter(Path.GetTempPath())
                });

                var result = await TestSingleIsoInternalAsync(isoPath, fileIndex++, performDeepScan,
                    onCloudRetryRequired,
                    progress, token);

                if (result == IsoTestResultStatus.Passed)
                {
                    progress.Report(new BatchOperationProgress
                        { SuccessCount = 1, LogMessage = $"  SUCCESS: '{fileName}' passed test." });
                    if (moveSuccessful)
                        await _fileMover.MoveTestedFileAsync(isoPath, successFolder, "successfully tested", token);
                }
                else
                {
                    progress.Report(new BatchOperationProgress
                    {
                        FailedCount = 1, FailedPathToAdd = isoPath, LogMessage = $"  FAILURE: '{fileName}' failed test."
                    });
                    _logger.Information("Test failed for {FileName}", fileName);
                    if (moveFailed) await _fileMover.MoveTestedFileAsync(isoPath, failedFolder, "failed test", token);
                }

                processed++;
                progress.Report(new BatchOperationProgress { ProcessedCount = processed });
            }

            _logger.Information("ISO test completed. Processed {ProcessedCount} file(s)", processed);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("OrchestratorService.TestAsync canceled");
            throw;
        }
        catch (Exception ex)
        {
            // The caller (MainWindow) is responsible for reporting unexpected failures;
            // log here with context but avoid duplicate bug reports.
            if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) || IsFatalEnvironmentalError(ex) ||
                ex is IOException)
            {
                _logger.Information(ex, "OrchestratorService.TestAsync stopped due to an environmental error");
            }
            else
            {
                _logger.Information(ex, "OrchestratorService.TestAsync failed");
            }

            throw;
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
            _logger.Debug("ISO {IsoPath} is not directly readable; copying to local temp", isoPath);
            var simpleName = GenerateFilename.GenerateSimpleFilename(index);
            long estimatedSize = 0;
            try
            {
                estimatedSize = new FileInfo(isoPath).Length;
            }
            catch (Exception ex)
            {
                // ignored
                _logger.Debug(ex, "Could not determine size of ISO {IsoPath}", isoPath);
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
            progress.Report(new BatchOperationProgress { LogMessage = "  Verifying ISO structure and readability..." });

            var passed = await _integrityService.TestIsoIntegrityAsync(pathToCheck, performDeepScan, progress, token);

            return passed ? IsoTestResultStatus.Passed : IsoTestResultStatus.Failed;
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("ISO test canceled: {IsoPath}", isoPath);
            throw;
        }
        catch (Exception ex)
        {
            progress.Report(new BatchOperationProgress { LogMessage = $"  Test Error: {ex.Message}" });
            _logger.Information(ex, "Test failed for {IsoPath}", isoPath);
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

    #region CUE File Parsing

    /// <summary>
    /// Parses a CUE file to extract the referenced BIN file paths.
    /// CUE files contain lines like: FILE "filename.bin" BINARY
    /// </summary>
    /// <param name="cuePath">Path to the CUE file</param>
    /// <param name="logger">Optional logger used to report parsing failures.</param>
    /// <returns>List of full paths to referenced BIN files</returns>
    internal static List<string> GetReferencedBinFilesFromCue(string cuePath, ILogger? logger = null)
    {
        var binFiles = new List<string>();
        var cueFolder = Path.GetDirectoryName(cuePath);

        if (string.IsNullOrEmpty(cueFolder) || !File.Exists(cuePath))
            return binFiles;

        try
        {
            var lines = File.ReadAllLines(cuePath);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                // Look for FILE "filename" BINARY patterns
                if (trimmedLine.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract filename between quotes
                    var firstQuote = trimmedLine.IndexOf('"');
                    var secondQuote = trimmedLine.IndexOf('"', firstQuote + 1);

                    if (firstQuote >= 0 && secondQuote > firstQuote)
                    {
                        var fileName = trimmedLine.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
                        var binPath = Path.Combine(cueFolder, fileName);
                        if (!binFiles.Contains(binPath))
                        {
                            binFiles.Add(binPath);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If parsing fails, return empty list
            logger?.Debug(ex, "Could not parse CUE file {CuePath}", cuePath);
        }

        return binFiles;
    }

    #endregion
}