namespace XboxIsoStudio.Models;

/// <summary>
///     Outcome of extracting an archive. <see cref="SkippedEntries" /> lists entries the
///     extractor deliberately did not write to disk (for example additional ISO images or
///     unsafe paths); the caller must not delete the source archive when any entry was skipped.
/// </summary>
public sealed record ArchiveExtractionResult(bool Success, IReadOnlyList<string> SkippedEntries)
{
    public static ArchiveExtractionResult Failed { get; } = new(Success: false, SkippedEntries: []);
}