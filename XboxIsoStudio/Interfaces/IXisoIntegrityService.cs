using XboxIsoStudio.Models;

namespace XboxIsoStudio.Interfaces;

/// <summary>
/// Validates Xbox image structure and readability: XDVDFS structure for plain ISO and
/// CISO (<c>.cso</c>) images via the XISOSharp library, ZAR archive structure and
/// blocks for <c>.zar</c> via ZArchiveSharp.
/// </summary>
public interface IXisoIntegrityService
{
    /// <summary>
    /// Tests an image for structural validity and readability.
    /// </summary>
    /// <param name="imagePath">Full path to the image or archive to test.</param>
    /// <param name="performDeepScan">
    /// When <c>true</c>, reads all image data to detect media or decompression errors.
    /// </param>
    /// <param name="progress">Receives progress updates during the test.</param>
    /// <param name="token">Token used to cancel the test.</param>
    /// <returns><c>true</c> when the image passed the test; otherwise <c>false</c>.</returns>
    Task<bool> TestIsoIntegrityAsync(string imagePath, bool performDeepScan,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}