using BatchConvertIsoToXiso.Models;

namespace BatchConvertIsoToXiso.Interfaces;

/// <summary>
/// Converts Xbox ISO images to the optimized XISO format using the XISOSharp library.
/// </summary>
public interface IXisoSharpService
{
    /// <summary>
    /// Converts <paramref name="inputFile"/> and writes the result into <paramref name="outputFolder"/>
    /// using <paramref name="outputFileName"/>. The caller supplies the name explicitly so the result
    /// always matches the user-visible original file name, even when the input is a temporary working copy.
    /// </summary>
    Task<FileProcessingStatus> ConvertIsoToXisoAsync(string inputFile, string outputFolder, string outputFileName,
        bool skipSystemUpdate, bool checkIntegrity, IProgress<BatchOperationProgress> progress, CancellationToken token);
}
