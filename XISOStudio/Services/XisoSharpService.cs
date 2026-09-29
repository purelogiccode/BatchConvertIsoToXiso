using XISOStudio.Interfaces;
using XISOStudio.Models;
using Serilog;
using XISOSharp;
using XISOSharp.Models;
using ZArchiveSharp.Pipeline;

namespace XISOStudio.Services;

/// <summary>
/// Converts Xbox ISO images to optimized XISO, ZAR (ZArchive/zstd), or CSO (compressed ISO)
/// using the XISOSharp library. All encoding/decoding work is delegated to the library;
/// no external conversion binaries are used.
/// </summary>
public class XisoSharpService : IXisoSharpService
{
    /// <summary>
    /// CISO compression level 9: LZ4 acceleration 1 — byte-identical output to
    /// <c>xdvdfs compress</c> and the smallest CISO the library can produce.
    /// </summary>
    private const int CsoCompressionLevel = 9;

    private readonly ILogger _logger;
    private readonly IDiskMonitorService _diskMonitorService;

    /// <summary>
    /// Initializes a new instance of the <see cref="XisoSharpService"/> class.
    /// </summary>
    /// <param name="logger">Logger used for diagnostics.</param>
    /// <param name="diskMonitorService">Resolves temporary directories based on free disk space.</param>
    public XisoSharpService(ILogger logger, IDiskMonitorService diskMonitorService)
    {
        _logger = logger.ForContext<XisoSharpService>();
        _diskMonitorService = diskMonitorService;
    }

    /// <summary>
    /// Converts <paramref name="inputFile"/> and writes the result into <paramref name="outputFolder"/>
    /// using <paramref name="outputFileName"/> in the requested <paramref name="outputFormat"/>. The
    /// caller supplies the name explicitly so the result always matches the user-visible original
    /// file name, even when the input is a temporary working copy.
    /// </summary>
    /// <param name="inputFile">Path of the source image to convert.</param>
    /// <param name="outputFolder">Folder where the converted file is written.</param>
    /// <param name="outputFileName">File name to use for the converted output.</param>
    /// <param name="outputFormat">The format to convert to.</param>
    /// <param name="skipSystemUpdate">Whether to remove the $SystemUpdate folder from the output image.</param>
    /// <param name="checkIntegrity">Whether to validate the source or output image structure.</param>
    /// <param name="progress">Receives progress updates during the conversion.</param>
    /// <param name="token">Token used to cancel the conversion.</param>
    /// <returns>The outcome of the conversion.</returns>
    public async Task<FileProcessingStatus> ConvertIsoAsync(string inputFile, string outputFolder,
        string outputFileName, OutputFormat outputFormat, bool skipSystemUpdate, bool checkIntegrity,
        IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        _logger.Information("Converting '{FileName}' to {OutputFormat} using XISOSharp...", fileName, outputFormat);

        if (!File.Exists(inputFile))
        {
            _logger.Information("Input file not found: {InputFile}", inputFile);
            return FileProcessingStatus.Failed;
        }

        outputFileName = Path.GetFileName(outputFileName);
        var outputPath = Path.Combine(outputFolder, outputFileName);

        // XISOSharp reads the source and writes the output directly; converting a file
        // onto itself would destroy the source.
        if (XisoPaths.AreSamePath(inputFile, outputPath))
        {
            _logger.Information("The output file would overwrite the source file for '{FileName}'. " +
                                "Please choose a different output folder.", fileName);
            return FileProcessingStatus.Failed;
        }

        // XISO output skips images that are already optimized — unless $SystemUpdate must be
        // stripped, which requires a rewrite. Compressed formats still have to pack those
        // images (the container is what changes).
        if (outputFormat == OutputFormat.Xiso && !skipSystemUpdate && XisoReader.IsOptimizedImage(inputFile))
        {
            _logger.Information("'{FileName}' is already an optimized XISO. Skipping conversion.", fileName);
            return FileProcessingStatus.AlreadyOptimized;
        }

        var inputFileSize = new FileInfo(inputFile).Length;
        var outputCheck = CheckOutputDrive(inputFile, inputFileSize, outputFolder, outputFormat);
        if (outputCheck != null)
        {
            _logger.Information("{Message:l}", outputCheck);
            return FileProcessingStatus.Failed;
        }

        try
        {
            Directory.CreateDirectory(outputFolder);
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Could not create the output folder '{OutputFolder}'", outputFolder);
            return FileProcessingStatus.Failed;
        }

        // Remove any pre-existing output so the conversion starts from a clean file. This runs
        // only after the already-optimized check above, so skipping a file never deletes an
        // existing result in the output folder.
        if (File.Exists(outputPath))
        {
            try
            {
                File.Delete(outputPath);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Could not delete the existing output file '{OutputFileName}'", outputFileName);
                return FileProcessingStatus.Failed;
            }
        }

        return await Task.Run(
            () => outputFormat switch
            {
                OutputFormat.Zar => ConvertToCompressedCore(OutputFormat.Zar, inputFile, outputPath,
                    skipSystemUpdate, checkIntegrity, progress, token),
                OutputFormat.Cso => ConvertToCompressedCore(OutputFormat.Cso, inputFile, outputPath,
                    skipSystemUpdate, checkIntegrity, progress, token),
                _ => ConvertToXisoCore(inputFile, outputFolder, outputPath, skipSystemUpdate, checkIntegrity,
                    progress, token)
            }, token);
    }

    private FileProcessingStatus ConvertToXisoCore(string inputFile, string outputFolder,
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
            Logger.ForwardInfo = message => _logger.Information("  [xiso] {Message:l}", message.TrimEnd());
            Logger.ForwardError = message => _logger.Information("  [xiso] ERROR: {Message:l}", message.TrimEnd());

            var progressAdapter = CreateRewriteProgressAdapter(progress);

            if (skipSystemUpdate)
            {
                // Rewrite applies the filter to the folder's contents but leaves the folder
                // entry behind in XISOSharp 1.4.1, so use extract+repack for the filter.
                return RepackWithoutSystemUpdate(inputFile, outputPath, checkIntegrity, progress, token);
            }

            // Pass the computed output name explicitly so the result always matches
            // the path checked above and shown in progress output.
            var result = XisoReader.Rewrite(inputFile, outputFolder, out outIsoPath, token,
                outputName: Path.GetFileName(outputPath), progress: progressAdapter);

            var resultPath = string.IsNullOrEmpty(outIsoPath) ? outputPath : outIsoPath;

            if (result != 0 || !File.Exists(resultPath))
            {
                _logger.Information("XISOSharp could not convert '{FileName}' (result code {ResultCode}).", fileName,
                    result);
                DeletePartialOutput(resultPath);
                return FileProcessingStatus.Failed;
            }

            if (checkIntegrity)
            {
                _logger.Information("Verifying output XISO integrity...");
                var audit = XisoReader.AuditXiso(resultPath);
                if (!audit.IsValid)
                {
                    _logger.Information("Output XISO failed structural validation: {Issues}",
                        string.Join("; ", audit.Issues));
                    DeletePartialOutput(resultPath);
                    return FileProcessingStatus.Failed;
                }

                _logger.Information("Output XISO passed validation ({FilesChecked} files, {DirsChecked} directories).",
                    audit.FilesChecked, audit.DirsChecked);
            }

            _logger.Information("Successfully converted '{FileName}' to XISO format.", fileName);
            return FileProcessingStatus.Converted;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Conversion of '{FileName}' was canceled. Cleaning up partial output...", fileName);
            DeletePartialOutput(outIsoPath ?? outputPath);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Information(ex, "Not enough disk space to convert '{FileName}'", fileName);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDeviceIoError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Information(ex,
                "The drive reported a hardware I/O error while converting '{FileName}'.\n\n" +
                "This usually means the source or output drive is failing, was disconnected, or has a hardware problem.\n" +
                "Please check the drive connection and health (e.g. run chkdsk), then try again.", fileName);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsNetworkError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Information(ex, "Network error while converting '{FileName}'.\n\n" +
                                    "Please try:\n" +
                                    "1. Check that the network drive is still connected and accessible\n" +
                                    "2. Copy the file to a local drive before processing\n" +
                                    "3. Check your network connection stability", fileName);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Information(ex, "Drive or path not found for '{FileName}'.\n\n" +
                                    "Please check that the drive is connected and the path exists.", fileName);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Warning(ex, "Access denied while converting '{FileName}'.\n\n" +
                                "The output folder may be write-protected or require administrator rights.\n" +
                                "Please choose a different output folder or run the application as administrator.",
                fileName);
            return FileProcessingStatus.Failed;
        }
        catch (Exception ex) when (IsInvalidImageError(ex))
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Information(ex,
                "Failed to convert '{FileName}'. The file may not be a valid Xbox/Xbox 360 ISO image, " +
                "may be corrupt, or contains a file too large for XISO.", fileName);
            return FileProcessingStatus.InvalidInput;
        }
        catch (Exception ex)
        {
            DeletePartialOutput(outIsoPath ?? outputPath);
            _logger.Error(ex, "Failed to convert '{FileName}'", fileName);
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
    ///     Repacks an image with the <c>$SystemUpdate</c> folder removed. XISOSharp 1.4.1's
    ///     <c>XisoReader.Rewrite</c> filters the folder's contents but leaves the folder entry
    ///     itself in the output, so the image is extracted to a temporary directory and packed
    ///     from there (both steps honor <c>Logger.RemoveSystemUpdate</c>).
    /// </summary>
    private FileProcessingStatus RepackWithoutSystemUpdate(string inputFile, string outputPath,
        bool checkIntegrity, IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        string? tempDir = null;

        var previousRemoveSystemUpdate = Logger.RemoveSystemUpdate;
        try
        {
            try
            {
                tempDir = PathHelper.ResolveTempDirectory(new FileInfo(inputFile).Length, "XISOStudio_Filter",
                    _diskMonitorService);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex,
                    "Could not resolve a temp directory for the $SystemUpdate filter on '{FileName}'; using the default temp path",
                    fileName);
                tempDir = Path.Combine(Path.GetTempPath(), "XISOStudio_Filter", Guid.NewGuid().ToString("N"));
            }

            Directory.CreateDirectory(tempDir);

            progress.Report(new BatchOperationProgress { StatusText = "Removing $SystemUpdate folder..." });

            Logger.RemoveSystemUpdate = true;

            var extractResult = XisoReader.UnpackImage(inputFile, tempDir, token,
                progress: CreateRewriteProgressAdapter(progress));
            if (extractResult != 0)
            {
                _logger.Information(
                    "XISOSharp could not unpack '{FileName}' for the $SystemUpdate filter (result code {ResultCode}).",
                    fileName, extractResult);
                return FileProcessingStatus.Failed;
            }

            var packResult = XisoWriter.PackFromDirectory(tempDir, outputPath, excludePatterns: null,
                progressCallback: null, token, CreateRewriteProgressAdapter(progress));
            if (packResult != 0 || !File.Exists(outputPath))
            {
                _logger.Information(
                    "XISOSharp could not repack '{FileName}' without $SystemUpdate (result code {ResultCode}).",
                    fileName, packResult);
                DeletePartialOutput(outputPath);
                return FileProcessingStatus.Failed;
            }
        }
        finally
        {
            Logger.RemoveSystemUpdate = previousRemoveSystemUpdate;

            if (tempDir != null)
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Could not delete temporary filter folder '{TempDir}'", tempDir);
                }
            }
        }

        if (checkIntegrity)
        {
            _logger.Information("Verifying output XISO integrity...");
            var audit = XisoReader.AuditXiso(outputPath);
            if (!audit.IsValid)
            {
                _logger.Information("Output XISO failed structural validation: {Issues}",
                    string.Join("; ", audit.Issues));
                DeletePartialOutput(outputPath);
                return FileProcessingStatus.Failed;
            }

            _logger.Information("Output XISO passed validation ({FilesChecked} files, {DirsChecked} directories).",
                audit.FilesChecked, audit.DirsChecked);
        }

        _logger.Information("Successfully converted '{FileName}' to XISO format.", fileName);
        return FileProcessingStatus.Converted;
    }

    /// <summary>
    /// Produces a ZAR or CSO file. ZAR streams the game-partition file tree straight into
    /// the archive; CSO first rewrites the image to a temporary optimized XISO (CISO has no
    /// partition-aware writer) and compresses that.
    /// </summary>
    private FileProcessingStatus ConvertToCompressedCore(OutputFormat outputFormat, string inputFile,
        string outputPath, bool skipSystemUpdate, bool checkIntegrity,
        IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        var formatName = outputFormat == OutputFormat.Zar ? "ZAR" : "CSO";
        string? tempDir = null;

        var previousRemoveSystemUpdate = Logger.RemoveSystemUpdate;
        var previousForwardInfo = Logger.ForwardInfo;
        var previousForwardError = Logger.ForwardError;

        try
        {
            Logger.RemoveSystemUpdate = skipSystemUpdate;
            Logger.ForwardInfo = message => _logger.Information("  [xiso] {Message:l}", message.TrimEnd());
            Logger.ForwardError = message => _logger.Information("  [xiso] ERROR: {Message:l}", message.TrimEnd());

            if (outputFormat == OutputFormat.Cso)
            {
                // CISO compresses a plain image stream; rewrite Redump/non-optimized inputs to a
                // temporary optimized XISO first so the archive contains the game partition only
                // and the $SystemUpdate filter applies. Already-optimized inputs are rewritten
                // too when $SystemUpdate must be stripped (CISO has no filter of its own).
                var sourceForCompression = inputFile;
                if (!XisoReader.IsOptimizedImage(inputFile) || skipSystemUpdate)
                {
                    try
                    {
                        tempDir = PathHelper.ResolveTempDirectory(new FileInfo(inputFile).Length,
                            "XISOStudio_Cso", _diskMonitorService);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex,
                            "Could not resolve a temp directory for CSO conversion of '{FileName}'; using the default temp path",
                            fileName);
                        tempDir = Path.Combine(Path.GetTempPath(), "XISOStudio_Cso",
                            Guid.NewGuid().ToString("N"));
                    }

                    Directory.CreateDirectory(tempDir);

                    progress.Report(new BatchOperationProgress
                        { StatusText = "Preparing XISO for compression..." });

                    var tempXiso = Path.Combine(tempDir, "source.iso");
                    if (skipSystemUpdate)
                    {
                        // Rewrite leaves the $SystemUpdate folder entry behind; extract+repack
                        // removes it completely.
                        var filterStatus = RepackWithoutSystemUpdate(inputFile, tempXiso, checkIntegrity: false,
                            progress, token);
                        if (filterStatus != FileProcessingStatus.Converted)
                            return FileProcessingStatus.Failed;
                    }
                    else
                    {
                        var rewriteResult = XisoReader.Rewrite(inputFile, tempDir, out var rewrittenXiso, token,
                            outputName: "source.iso", progress: CreateRewriteProgressAdapter(progress));
                        if (rewriteResult != 0 || string.IsNullOrEmpty(rewrittenXiso) || !File.Exists(rewrittenXiso))
                        {
                            _logger.Information(
                                "XISOSharp could not prepare '{FileName}' for CSO compression (result code {ResultCode}).",
                                fileName, rewriteResult);
                            return FileProcessingStatus.Failed;
                        }

                        tempXiso = rewrittenXiso;
                    }

                    sourceForCompression = tempXiso;
                }

                if (checkIntegrity && !AuditSourceImage(sourceForCompression, fileName))
                    return FileProcessingStatus.Failed;

                progress.Report(new BatchOperationProgress { StatusText = "Compressing to CSO..." });
                var csoResult = CisoWriter.CompressToCso(sourceForCompression, outputPath, CsoCompressionLevel,
                    null, CisoWriter.VersionLz4, CreateCisoProgressAdapter(progress), token);
                if (csoResult != 0 || !File.Exists(outputPath))
                {
                    _logger.Information("XISOSharp could not compress '{FileName}' to CSO (result code {ResultCode}).",
                        fileName, csoResult);
                    DeletePartialOutput(outputPath);
                    return FileProcessingStatus.Failed;
                }

                _logger.Information("Successfully compressed '{FileName}' to CSO format.", fileName);
                return FileProcessingStatus.Converted;
            }

            if (checkIntegrity && !AuditSourceImage(inputFile, fileName)) return FileProcessingStatus.Failed;

            // ZAR streams the game-partition tree straight from the source image.
            var partitionOffset = GetGamePartitionOffset(inputFile);
            progress.Report(new BatchOperationProgress { StatusText = "Packing to ZAR..." });

            bool zarResult;
            using (var isoStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            {
                zarResult = XisoZarchive.CreateZar(isoStream, partitionOffset, outputPath, skipSystemUpdate,
                    true, token, null, CreateZarProgressAdapter(progress));
            }

            if (!zarResult || !File.Exists(outputPath))
            {
                _logger.Information("XISOSharp could not pack '{FileName}' to ZAR.", fileName);
                DeletePartialOutput(outputPath);
                return FileProcessingStatus.Failed;
            }

            _logger.Information("Successfully packed '{FileName}' to ZAR format.", fileName);
            return FileProcessingStatus.Converted;
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Conversion of '{FileName}' to {Format} was canceled. Cleaning up partial output...",
                fileName, formatName);
            DeletePartialOutput(outputPath);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
        {
            DeletePartialOutput(outputPath);
            _logger.Information(ex, "Not enough disk space to convert '{FileName}' to {Format}", fileName, formatName);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDeviceIoError(ex))
        {
            DeletePartialOutput(outputPath);
            _logger.Information(ex,
                "The drive reported a hardware I/O error while converting '{FileName}' to {Format}.\n\n" +
                "This usually means the source or output drive is failing, was disconnected, or has a hardware problem.\n" +
                "Please check the drive connection and health (e.g. run chkdsk), then try again.", fileName,
                formatName);
            throw;
        }
        catch (Exception ex) when (PathHelper.IsNetworkError(ex))
        {
            DeletePartialOutput(outputPath);
            _logger.Information(ex, "Network error while converting '{FileName}' to {Format}.\n\n" +
                                    "Please try:\n" +
                                    "1. Check that the network drive is still connected and accessible\n" +
                                    "2. Copy the file to a local drive before processing\n" +
                                    "3. Check your network connection stability", fileName, formatName);
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            DeletePartialOutput(outputPath);
            _logger.Information(ex, "Drive or path not found for '{FileName}'.\n\n" +
                                    "Please check that the drive is connected and the path exists.", fileName);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            DeletePartialOutput(outputPath);
            _logger.Warning(ex, "Access denied while converting '{FileName}' to {Format}.\n\n" +
                                "The output folder may be write-protected or require administrator rights.\n" +
                                "Please choose a different output folder or run the application as administrator.",
                fileName, formatName);
            return FileProcessingStatus.Failed;
        }
        catch (Exception ex) when (IsInvalidImageError(ex))
        {
            DeletePartialOutput(outputPath);
            _logger.Information(ex,
                "Failed to convert '{FileName}' to {Format}. The file may not be a valid Xbox/Xbox 360 ISO image, " +
                "may be corrupt, or contains a file too large for XISO.", fileName, formatName);
            return FileProcessingStatus.InvalidInput;
        }
        catch (Exception ex)
        {
            DeletePartialOutput(outputPath);
            _logger.Error(ex, "Failed to convert '{FileName}' to {Format}", fileName, formatName);
            return FileProcessingStatus.Failed;
        }
        finally
        {
            Logger.RemoveSystemUpdate = previousRemoveSystemUpdate;
            Logger.ForwardInfo = previousForwardInfo;
            Logger.ForwardError = previousForwardError;

            if (tempDir != null)
            {
                try
                {
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Could not delete temporary CSO working folder '{TempDir}'", tempDir);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the byte offset of the game partition for Redump full-disc images, mirroring the
    /// XISOSharp CLI. Returns 0 for XISO/plain images whose partition starts at the file start.
    /// </summary>
    private long GetGamePartitionOffset(string inputFile)
    {
        var redumpType = XgdTables.GetRedumpIsoTypeBySize(new FileInfo(inputFile).Length);
        if (redumpType < 0) return 0;

        var videoType = -1;
        try
        {
            using var stream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            videoType = XgdTables.GetVideoType(stream, redumpType);
        }
        catch (Exception ex)
        {
            // Fall back to the redump-type mapping below.
            _logger.Debug(ex, "Could not read the video type for '{InputFile}'", inputFile);
        }

        // XgdTables.GetXisoTypeFromVideo never returns a negative value (its default arm
        // returns 0), so an unknown or unreadable video PVD (videoType < 0) must fall back
        // to the Redump type mapping directly; passing 0 would select the XGD1 offset.
        var xisoType = videoType >= 0
            ? XgdTables.GetXisoTypeFromVideo(videoType)
            : XgdTables.GetXgdType(redumpType);
        if (xisoType < 0 || xisoType >= XgdTables.XisoOffset.Length) return 0;

        return XgdTables.XisoOffset[xisoType];
    }

    /// <summary>
    /// Validates the image that is about to be packed/compressed. Compressed outputs cannot be
    /// audited with the XISO reader, so the structural check runs on the source image instead.
    /// </summary>
    private bool AuditSourceImage(string sourcePath, string fileName)
    {
        _logger.Information("Verifying source image integrity for '{FileName}'...", fileName);
        // Source images may be raw (non-optimized) dumps; only the filesystem structure
        // is validated, not the optimized tag (which is written during conversion).
        var audit = XisoReader.AuditXiso(sourcePath, requireOptimizedTag: false);
        if (!audit.IsValid)
        {
            _logger.Information("Source image failed structural validation: {Issues}",
                string.Join("; ", audit.Issues));
            return false;
        }

        if (!audit.IsOptimized)
        {
            _logger.Information("Source image is not optimized (raw ISO); the tag is written during conversion.");
        }

        _logger.Information("Source image passed validation ({FilesChecked} files, {DirsChecked} directories).",
            audit.FilesChecked, audit.DirsChecked);
        return true;
    }

    private static IProgress<ProgressInfo> CreateRewriteProgressAdapter(IProgress<BatchOperationProgress> progress)
    {
        return new Progress<ProgressInfo>(info =>
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
    }

    /// <summary>
    /// CISO progress arrives once per 2048-byte sector; report only at 5% steps so the UI is
    /// not flooded for multi-gigabyte images.
    /// </summary>
    private static IProgress<ProgressInfo> CreateCisoProgressAdapter(IProgress<BatchOperationProgress> progress)
    {
        var totalBlocks = 0L;
        var lastPercent = -1;

        return new Progress<ProgressInfo>(info =>
        {
            switch (info.Type)
            {
                case ProgressInfoType.FileCount:
                    totalBlocks = info.Count;
                    progress.Report(new BatchOperationProgress
                        { StatusText = $"Compressing {info.Count:N0} sectors..." });
                    break;
                case ProgressInfoType.FileAdded:
                    if (totalBlocks <= 0) break;
                    var percent = (int)(info.Sector * 100 / totalBlocks);
                    if (percent < lastPercent + 5 && percent < 100) break;
                    lastPercent = percent;
                    progress.Report(new BatchOperationProgress { StatusText = $"Compressing: {percent}%" });
                    break;
                case ProgressInfoType.FinishedPacking:
                    progress.Report(new BatchOperationProgress { StatusText = "Finalizing output..." });
                    break;
            }
        });
    }

    /// <summary>
    /// ZAR progress reports per packed file; report at 5% steps (or the current file name).
    /// </summary>
    private static IProgress<ZarProgress> CreateZarProgressAdapter(IProgress<BatchOperationProgress> progress)
    {
        var lastPercent = -1;

        return new Progress<ZarProgress>(zarProgress =>
        {
            if (zarProgress.BytesTotal <= 0) return;

            var percent = (int)(zarProgress.BytesCompleted * 100 / zarProgress.BytesTotal);
            if (percent < lastPercent + 5 && percent < 100) return;

            lastPercent = percent;
            var text = string.IsNullOrEmpty(zarProgress.CurrentFile)
                ? $"Packing: {percent}%"
                : $"Packing: {zarProgress.CurrentFile}";
            progress.Report(new BatchOperationProgress { StatusText = text });
        });
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

    private void DeletePartialOutput(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            // Cleanup failure is non-fatal; the error is already reported to the user
            _logger.Debug(ex, "Could not delete partial output file '{Path}'", path);
        }
    }

    /// <summary>
    /// Validates that the output drive can hold the converted file before the conversion
    /// starts. Returns an error message when the output drive is full or uses a file system
    /// that cannot store files of this size (FAT32 4 GB limit); otherwise returns null.
    /// </summary>
    private string? CheckOutputDrive(string inputFile, long inputFileSize, string outputFolder,
        OutputFormat outputFormat)
    {
        try
        {
            // Compressed outputs (CSO/ZAR/CHD) are smaller than the source; requiring the
            // full raw size would reject conversions that fit. A conservative 50% estimate
            // is used for them, and for both the free-space and the FAT32 check.
            var estimatedOutputSize = outputFormat == OutputFormat.Xiso ? inputFileSize : inputFileSize / 2;
            var availableSpace = _diskMonitorService.GetAvailableFreeSpace(outputFolder);
            var requiredWithBuffer = estimatedOutputSize + Math.Max(estimatedOutputSize / 10, 200L * 1024 * 1024);

            if (availableSpace > 0 && availableSpace < requiredWithBuffer)
            {
                return $"Not enough disk space on the output drive for '{Path.GetFileName(inputFile)}'. " +
                       $"Estimated required: {Formatter.FormatBytes(estimatedOutputSize)}, Available: {Formatter.FormatBytes(availableSpace)}. " +
                       "Please free up disk space or select a different output folder.";
            }

            var fullPath = Path.GetFullPath(outputFolder);
            if (PathHelper.IsUncPath(fullPath)) return null;

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return null;

            var drive = new DriveInfo(root);
            if (drive.IsReady &&
                drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) &&
                estimatedOutputSize > (4L * 1024 * 1024 * 1024) - 1)
            {
                return $"The output drive '{drive.Name}' uses FAT32, which cannot store files larger than 4 GB. " +
                       $"'{Path.GetFileName(inputFile)}' is {Formatter.FormatBytes(inputFileSize)} (estimated output: {Formatter.FormatBytes(estimatedOutputSize)}). " +
                       "Please use an NTFS or exFAT formatted output drive.";
            }
        }
        catch (Exception ex)
        {
            // Pre-check failures are non-fatal; the conversion will surface real errors if they occur
            _logger.Debug(ex, "Could not check the output drive for '{OutputFolder}'", outputFolder);
        }

        return null;
    }
}