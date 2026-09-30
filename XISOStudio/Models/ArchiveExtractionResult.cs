namespace XISOStudio.Models;

/// <summary>
///     Outcome of extracting an archive. <see cref="Success" /> is <c>true</c> when the
///     extraction completed without a fatal error, even if every entry was skipped or the
///     archive held no extractable entries. <see cref="SkippedEntries" /> lists entries the
///     extractor deliberately did not write to disk (for example additional ISO images or
///     unsafe paths); the caller must not delete the source archive when any entry was skipped.
/// </summary>
public sealed record ArchiveExtractionResult(bool Success, IReadOnlyList<string> SkippedEntries)
{
    /// <summary>
    ///     A failed extraction result: the extraction did not complete. No entries were
    ///     deliberately skipped, but partially written files may exist.
    /// </summary>
    public static ArchiveExtractionResult Failed { get; } = new(Success: false, SkippedEntries: []);
}