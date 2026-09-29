namespace XboxIsoStudio.Models;

/// <summary>
/// Outcome of processing a single input image during conversion.
/// </summary>
public enum FileProcessingStatus
{
    /// <summary>The image was converted and written to the output folder.</summary>
    Converted,

    /// <summary>The image was not rewritten by the converter.</summary>
    Skipped,

    /// <summary>Processing the image failed.</summary>
    Failed,

    /// <summary>The image was already optimized and did not need conversion.</summary>
    AlreadyOptimized,

    /// <summary>The input is not a valid/supported Xbox image (unsupported, empty or corrupt).</summary>
    InvalidInput
}