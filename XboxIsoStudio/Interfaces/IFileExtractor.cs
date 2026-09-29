using XboxIsoStudio.Models;

namespace XboxIsoStudio.Interfaces;

public interface IFileExtractor
{
    Task<ArchiveExtractionResult> ExtractArchiveAsync(string archivePath, string extractionPath,
        CancellationToken token);

    Task<(long TotalUncompressedSize, int FileCount)> GetArchiveInfoAsync(string archivePath, CancellationToken token);
}
