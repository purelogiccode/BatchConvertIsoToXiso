using XboxIsoStudio.Models;

namespace XboxIsoStudio.Interfaces;

/// <summary>
/// Converts Xbox ISO images to the optimized XISO, ZAR, or CSO format using the XISOSharp library.
/// </summary>
public interface IXisoSharpService
{
    /// <summary>
    /// Converts <paramref name="inputFile"/> and writes the result into <paramref name="outputFolder"/>
    /// using <paramref name="outputFileName"/> in the requested <paramref name="outputFormat"/>.
    /// The caller supplies the name explicitly so the result always matches the user-visible original
    /// file name, even when the input is a temporary working copy.
    /// </summary>
    Task<FileProcessingStatus> ConvertIsoAsync(string inputFile, string outputFolder, string outputFileName,
        OutputFormat outputFormat, bool skipSystemUpdate, bool checkIntegrity,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}
