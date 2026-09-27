using BatchConvertIsoToXiso.Models;

namespace BatchConvertIsoToXiso.Interfaces;

/// <summary>
/// Validates XISO image structure and readability using the XISOSharp library.
/// </summary>
public interface IXisoIntegrityService
{
    Task<bool> TestIsoIntegrityAsync(string isoPath, bool performDeepScan,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}
