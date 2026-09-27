using System.Buffers;
using System.IO;
using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using XISOSharp;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// Validates XISO images using the XISOSharp library: a deep structural audit of the
/// XDVDFS directory tree, plus an optional sequential surface scan that reads every
/// sector to detect physical media errors.
/// </summary>
public class XisoIntegrityService : IXisoIntegrityService
{
    private readonly ILogger _logger;
    private readonly IBugReportService _bugReportService;

    public XisoIntegrityService(ILogger logger, IBugReportService bugReportService)
    {
        _logger = logger;
        _bugReportService = bugReportService;
    }

    public Task<bool> TestIsoIntegrityAsync(string isoPath, bool performDeepScan,
        IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            var fileName = Path.GetFileName(isoPath);

            try
            {
                _logger.LogMessage($"[INFO] Starting structural integrity test for: {fileName}");
                _logger.LogMessage(
                    "[INFO] Note: This verifies filesystem structure and readability, not data checksums.");

                // 1. Optional deep surface scan: read the entire image sequentially to test physical media
                if (performDeepScan && !PerformSurfaceScan(isoPath, progress, token))
                {
                    return false;
                }

                // 2. Logical structure test: deep audit of the XDVDFS directory tree
                _logger.LogMessage("[INFO] Validating XDVDFS directory structure...");
                var audit = XisoReader.AuditXiso(isoPath);

                if (!audit.IsValid)
                {
                    _logger.LogMessage($"[ERROR] Structural validation failed for {fileName}:");
                    foreach (var issue in audit.Issues)
                    {
                        _logger.LogMessage($"  - {issue}");
                    }

                    return false;
                }

                _logger.LogMessage(
                    $"[INFO] Structure is valid ({audit.FilesChecked} files, {audit.DirsChecked} directories checked).");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsInputError(ex))
            {
                _logger.LogMessage($"Integrity check failed for {fileName}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"Integrity check failed for {fileName}: {ex.Message}");
                _ = _bugReportService.SendBugReportAsync($"Integrity check failed for {fileName}", ex);
                return false;
            }
        }, token);
    }

    /// <summary>
    /// Errors that indicate the input image is missing, unsupported, or corrupt rather
    /// than an application defect. XISOSharp's low-level reader reports truncated
    /// images as a plain IOException with a "Read error" message.
    /// </summary>
    private static bool IsInputError(Exception ex)
    {
        return ex is XisoFormatException or XisoEmptyException or InvalidDataException or ExtractErrorException
                   or FileNotFoundException or EndOfStreamException ||
               (ex is IOException ioException &&
                ioException.Message.StartsWith("Read error", StringComparison.OrdinalIgnoreCase));
    }

    private bool PerformSurfaceScan(string isoPath, IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        _logger.LogMessage("[INFO] Performing deep surface scan (sequential read of all sectors)...");

        try
        {
            using var stream = XisoReader.OpenImageStream(isoPath);
            var buffer = ArrayPool<byte>.Shared.Rent(4 * 1024 * 1024); // 4MB chunks
            try
            {
                var totalBytes = stream.Length;
                long bytesRead = 0;
                long lastReportedPercent = -1;

                while (bytesRead < totalBytes)
                {
                    token.ThrowIfCancellationRequested();

                    var toRead = (int)Math.Min(buffer.Length, totalBytes - bytesRead);
                    var read = stream.Read(buffer, 0, toRead);

                    if (read == 0)
                    {
                        _logger.LogMessage($"[ERROR] Surface scan failed: unexpected end of file at {bytesRead}.");
                        return false;
                    }

                    bytesRead += read;

                    // Report progress every 1%
                    var percent = bytesRead * 100 / totalBytes;
                    if (percent > lastReportedPercent)
                    {
                        lastReportedPercent = percent;
                        progress.Report(new BatchOperationProgress { StatusText = $"Surface scan: {percent}%" });
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogMessage($"[ERROR] Surface scan failed: {ex.Message}");
            return false;
        }

        _logger.LogMessage("[INFO] Surface scan completed successfully.");
        return true;
    }
}
