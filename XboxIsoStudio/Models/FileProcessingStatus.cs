namespace XboxIsoStudio.Models;

public enum FileProcessingStatus
{
    Converted,
    Skipped,
    Failed,
    AlreadyOptimized,

    /// <summary>The input is not a valid/supported Xbox image (unsupported, empty or corrupt).</summary>
    InvalidInput
}
