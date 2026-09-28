using BatchConvertIsoToXiso.Models;

namespace BatchConvertIsoToXiso.Interfaces;

/// <summary>
/// Validates Xbox image structure and readability: XDVDFS structure for plain ISO and
/// CISO (<c>.cso</c>) images via the XISOSharp library, ZAR archive structure and
/// blocks for <c>.zar</c> via ZArchiveSharp.
/// </summary>
public interface IXisoIntegrityService
{
    Task<bool> TestIsoIntegrityAsync(string imagePath, bool performDeepScan,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}
