using BatchConvertIsoToXiso.Models;

namespace BatchConvertIsoToXiso.Interfaces;

/// <summary>
/// Converts Xbox ISO images to the optimized XISO format using the XISOSharp library.
/// </summary>
public interface IXisoSharpService
{
    Task<FileProcessingStatus> ConvertIsoToXisoAsync(string inputFile, string outputFolder, bool skipSystemUpdate,
        bool checkIntegrity, IProgress<BatchOperationProgress> progress, CancellationToken token);
}
