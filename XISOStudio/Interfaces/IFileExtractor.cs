using XISOStudio.Models;

namespace XISOStudio.Interfaces;

/// <summary>
/// Extracts archive contents to disk and reports archive metadata.
/// </summary>
public interface IFileExtractor
{
    /// <summary>
    /// Extracts an archive into <paramref name="extractionPath" />, skipping unsafe paths and
    /// additional ISO entries.
    /// </summary>
    /// <param name="archivePath">Full path to the archive to extract.</param>
    /// <param name="extractionPath">Directory the archive contents are written to.</param>
    /// <param name="token">Token used to cancel the extraction.</param>
    /// <returns>
    /// The extraction outcome. <see cref="ArchiveExtractionResult.Success" /> is <c>true</c>
    /// when the extraction completed without a fatal error, including archives whose entries
    /// were all skipped or that contain no extractable entries; it is <c>false</c> only when
    /// the extraction itself failed.
    /// </returns>
    Task<ArchiveExtractionResult> ExtractArchiveAsync(string archivePath, string extractionPath,
        CancellationToken token);

    /// <summary>
    /// Reads the total uncompressed size and file count of an archive without extracting it.
    /// </summary>
    /// <param name="archivePath">Full path to the archive to inspect.</param>
    /// <param name="token">Token used to cancel the operation.</param>
    /// <returns>The total uncompressed size in bytes and the number of files in the archive.</returns>
    Task<(long TotalUncompressedSize, int FileCount)> GetArchiveInfoAsync(string archivePath, CancellationToken token);
}