using XISOStudio.Models;

namespace XISOStudio.Interfaces;

/// <summary>
/// Converts Xbox ISO images to CHD (Compressed Hunks of Data) using the CHDSharp library.
/// The source is first rewritten to an optimized XISO (game partition only, honoring the
/// $SystemUpdate filter) and then encoded as a DVD CHD with the chdman <c>createdvd</c> preset.
/// </summary>
public interface IChdService
{
    /// <summary>
    /// Converts <paramref name="inputFile"/> and writes the result into <paramref name="outputFolder"/>
    /// using <paramref name="outputFileName"/>. The caller supplies the name explicitly so the
    /// result always matches the user-visible original file name, even when the input is a
    /// temporary working copy.
    /// </summary>
    Task<FileProcessingStatus> ConvertIsoToChdAsync(string inputFile, string outputFolder,
        string outputFileName, bool skipSystemUpdate, bool checkIntegrity,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}