namespace XboxIsoStudio.Models;

/// <summary>
/// Outcome of testing a single Xbox image for structural integrity and readability.
/// </summary>
public enum IsoTestResultStatus
{
    /// <summary>The image passed the integrity test.</summary>
    Passed,

    /// <summary>The image failed the integrity test.</summary>
    Failed
}