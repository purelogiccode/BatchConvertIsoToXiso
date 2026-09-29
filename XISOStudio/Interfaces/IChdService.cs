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
    /// <param name="inputFile">Path of the ISO image to convert.</param>
    /// <param name="outputFolder">Folder that receives the CHD file.</param>
    /// <param name="outputFileName">File name to use for the result.</param>
    /// <param name="skipSystemUpdate">Whether to exclude the $SystemUpdate folder from the optimized image.</param>
    /// <param name="checkIntegrity">Whether to verify the result after conversion.</param>
    /// <param name="progress">Receives batch progress and status updates.</param>
    /// <param name="token">Token that cancels the conversion.</param>
    /// <returns>The status of the completed conversion.</returns>
    Task<FileProcessingStatus> ConvertIsoToChdAsync(string inputFile, string outputFolder,
        string outputFileName, bool skipSystemUpdate, bool checkIntegrity,
        IProgress<BatchOperationProgress> progress, CancellationToken token);
}