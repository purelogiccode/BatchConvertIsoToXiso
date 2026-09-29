namespace XboxIsoStudio.Models;

/// <summary>
/// How the caller wants to proceed after a cloud file operation failed during a copy
/// (for example a OneDrive or Dropbox file that could not be downloaded).
/// </summary>
public enum CloudRetryResult
{
    /// <summary>Retry the failed cloud file operation.</summary>
    Retry,

    /// <summary>Skip the current file and continue with the rest of the batch.</summary>
    Skip,

    /// <summary>Cancel the whole batch operation.</summary>
    Cancel
}