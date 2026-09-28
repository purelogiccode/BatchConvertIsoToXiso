using System.Buffers;
using System.IO;
using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using Serilog;
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

    public XisoIntegrityService(ILogger logger)
    {
        _logger = logger.ForContext<XisoIntegrityService>();
    }

    public Task<bool> TestIsoIntegrityAsync(string isoPath, bool performDeepScan,
        IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        return Task.Run(() =>
        {
            var fileName = Path.GetFileName(isoPath);

            try
            {
                _logger.Information("Starting structural integrity test for: {FileName}", fileName);
                _logger.Information(
                    "Note: This verifies filesystem structure and readability, not data checksums.");

                // 1. Optional deep surface scan: read the entire image sequentially to test physical media
                if (performDeepScan && !PerformSurfaceScan(isoPath, progress, token))
                {
                    return false;
                }

                // 2. Logical structure test: deep audit of the XDVDFS directory tree.
                // The audit also reports a missing optimized tag; raw (unconverted) dumps
                // legitimately lack it, so that entry is informational, not a failure.
                _logger.Information("Validating XDVDFS directory structure...");
                var audit = XisoReader.AuditXiso(isoPath);
                var issues = audit.Issues.Where(static issue => !IsOptimizationNotice(issue)).ToList();

                if (issues.Count > 0)
                {
                    _logger.Information("Structural validation failed for {FileName}:", fileName);
                    foreach (var issue in issues)
                    {
                        _logger.Information("  - {Issue:l}", issue);
                    }

                    return false;
                }

                if (issues.Count != audit.Issues.Count)
                {
                    _logger.Information(
                        "Image is not optimized (raw ISO); the optimized tag is written during conversion.");
                }

                _logger.Information("Structure is valid ({FilesChecked} files, {DirsChecked} directories checked).",
                    audit.FilesChecked, audit.DirsChecked);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Integrity check of {FileName} was canceled.", fileName);
                throw;
            }
            catch (Exception ex) when (IsInputError(ex))
            {
                _logger.Information(ex, "Integrity check failed for {FileName}", fileName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Integrity check failed for {FileName}", fileName);
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

    /// <summary>
    /// The library's audit flags a missing optimized tag as an issue. Raw Redump-style
    /// ISOs are never optimized, so the tag is not part of filesystem integrity: the
    /// test accepts them while still failing on any structural defect.
    /// </summary>
    private static bool IsOptimizationNotice(string issue)
    {
        return issue.StartsWith("Optimized tag not found", StringComparison.Ordinal);
    }

    private bool PerformSurfaceScan(string isoPath, IProgress<BatchOperationProgress> progress, CancellationToken token)
    {
        _logger.Information("Performing deep surface scan (sequential read of all sectors)...");

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
                        _logger.Information("Surface scan failed: unexpected end of file at {BytesRead}.", bytesRead);
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
            _logger.Debug("Surface scan was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Surface scan failed");
            return false;
        }

        _logger.Information("Surface scan completed successfully.");
        return true;
    }
}