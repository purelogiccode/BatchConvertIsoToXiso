using System.IO.Compression;
using XISOStudio.Services;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests archive inspection, extraction, transient I/O detection, and zip-slip protection in <c>FileExtractorService</c>.</summary>
public class FileExtractorServiceTests : IDisposable
{
    private readonly TestLogger _logger = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"FileExtractorTests_{Guid.NewGuid():N}");

    public FileExtractorServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch
        {
            // ignored
        }

        GC.SuppressFinalize(this);
    }

    private FileExtractorService CreateService()
    {
        return new FileExtractorService(_logger.Logger);
    }

    private string CreateTestZip(string zipName, Dictionary<string, string> entries)
    {
        var zipPath = Path.Combine(_tempDir, zipName);
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (name, content) in entries)
        {
            var entry = archive.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }

        return zipPath;
    }

    private string CreateCorruptZip(string zipName)
    {
        var zipPath = Path.Combine(_tempDir, zipName);
        File.WriteAllBytes(zipPath, [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0xFF, 0xFF]);
        return zipPath;
    }

    #region GetArchiveInfoAsync Tests

    [Fact]
    public async Task GetArchiveInfoAsyncValidZipReturnsCorrectCount()
    {
        var zipPath = CreateTestZip("test.zip", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "file1.txt", "content1" },
            { "file2.txt", "content2" },
            { "file3.txt", "content3" }
        });
        var service = CreateService();

        var (totalSize, fileCount) = await service.GetArchiveInfoAsync(zipPath, CancellationToken.None);

        Assert.Equal(3, fileCount);
        Assert.True(totalSize > 0);
    }

    [Fact]
    public async Task GetArchiveInfoAsyncValidZipReturnsCorrectSize()
    {
        const string content1 = "Hello, World!"; // 13 bytes
        const string content2 = "Test content for size calculation"; // 33 bytes
        var zipPath = CreateTestZip("sized.zip", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "a.txt", content1 },
            { "b.txt", content2 }
        });
        var service = CreateService();

        var (totalSize, _) = await service.GetArchiveInfoAsync(zipPath, CancellationToken.None);

        Assert.Equal(content1.Length + content2.Length, totalSize);
    }

    [Fact]
    public async Task GetArchiveInfoAsyncSingleFileZipReturnsOne()
    {
        var zipPath = CreateTestZip("single.zip", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "only.txt", "data" }
        });
        var service = CreateService();

        var (_, fileCount) = await service.GetArchiveInfoAsync(zipPath, CancellationToken.None);

        Assert.Equal(1, fileCount);
    }

    [Fact]
    public async Task GetArchiveInfoAsyncZipWithSubdirectoriesOnlyCountsFiles()
    {
        var zipPath = CreateTestZip("nested.zip", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "root.txt", "root content" },
            { "sub/deep/file.txt", "deep content" }
        });
        var service = CreateService();

        var (_, fileCount) = await service.GetArchiveInfoAsync(zipPath, CancellationToken.None);

        Assert.Equal(2, fileCount);
    }

    [Fact]
    public async Task GetArchiveInfoAsyncEmptyZipReturnsZero()
    {
        var zipPath = Path.Combine(_tempDir, "empty.zip");
        await using (ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            // empty archive
        }

        var service = CreateService();
        var (totalSize, fileCount) = await service.GetArchiveInfoAsync(zipPath, CancellationToken.None);

        Assert.Equal(0, fileCount);
        Assert.Equal(0, totalSize);
    }

    [Fact]
    public async Task GetArchiveInfoAsyncCancellationThrowsOperationCanceled()
    {
        var zipPath = CreateTestZip("cancel.zip",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "f.txt", "d" } });
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetArchiveInfoAsync(zipPath, cts.Token));
    }

    #endregion

    #region Transient Error Detection Tests

    [Theory]
    [InlineData(0x20, "The process cannot access the file because it is being used by another process.")]
    [InlineData(0x21, "The process cannot access the file because another process has locked a portion of the file.")]
    public void IsTransientIoErrorSharingAndLockViolationsReturnTrue(int hresult, string message)
    {
        var ex = new IOException(message, unchecked((int)(0x80070000u | (uint)hresult)));
        Assert.True(FileExtractorService.IsTransientIoError(ex));
    }

    [Fact]
    public void IsTransientIoErrorDiskFullReturnsFalse()
    {
        var ex = new IOException("There is not enough space on the disk.", unchecked((int)0x80070070));
        Assert.False(FileExtractorService.IsTransientIoError(ex));
    }

    [Fact]
    public void IsTransientIoErrorEndOfStreamReturnsFalse()
    {
        Assert.False(FileExtractorService.IsTransientIoError(new EndOfStreamException()));
    }

    [Fact]
    public void IsTransientIoErrorNetworkMessageReturnsTrue()
    {
        var ex = new IOException("The network path was not found.");
        Assert.True(FileExtractorService.IsTransientIoError(ex));
    }

    #endregion

    #region ExtractArchiveAsync - Error Handling Tests

    [Fact]
    public async Task ExtractArchiveAsyncNonExistentFileThrows()
    {
        var service = CreateService();
        var nonExistent = Path.Combine(_tempDir, "does_not_exist.zip");

        await Assert.ThrowsAnyAsync<IOException>(() =>
            service.ExtractArchiveAsync(nonExistent, Path.Combine(_tempDir, "out"), CancellationToken.None));
    }

    [Fact]
    public async Task ExtractArchiveAsyncCorruptZipThrowsAndDoesNotLogWarningOrError()
    {
        var corruptPath = CreateCorruptZip("corrupt.zip");
        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_out");
        Directory.CreateDirectory(outDir);

        try
        {
            await service.ExtractArchiveAsync(corruptPath, outDir, CancellationToken.None);
        }
        catch
        {
            // Expected to throw
        }

        // Corrupt archives are user/input errors and must not be logged as warnings or errors
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public async Task ExtractArchiveAsyncValidZipExtractsSuccessfully()
    {
        var zipPath = CreateTestZip("valid.zip", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "test.txt", "hello world" }
        });
        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_valid");

        var result = await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(Path.Combine(outDir, "test.txt")));
        Assert.Equal("hello world", File.ReadAllText(Path.Combine(outDir, "test.txt")));
    }

    [Fact]
    public async Task ExtractArchiveAsyncPasswordProtectedZipReturnsFalseAndLogsError()
    {
        // Create a password-protected ZIP using System.IO.Compression
        var zipPath = Path.Combine(_tempDir, "protected.zip");
        const string entryName = "secret.txt";

        // Create a minimal encrypted zip (PKWARE encryption)
        // SharpCompress will throw CryptographicException for encrypted zips
        // We'll create a zip with the encryption flag set
        CreateEncryptedZip(zipPath, entryName, "secret content", "password123");

        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_protected");
        Directory.CreateDirectory(outDir);

        var result = await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);

        Assert.False(result.Success);

        // Verify the error message was logged (contains "encrypted" or "password")
        Assert.True(
            _logger.HasMessage("encrypted") ||
            _logger.HasMessage("password-protected"));
    }

    [Fact]
    public async Task ExtractArchiveAsyncPasswordProtectedZipDoesNotLogWarningOrError()
    {
        var zipPath = Path.Combine(_tempDir, "protected2.zip");
        CreateEncryptedZip(zipPath, "secret.txt", "data", "pw");

        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_protected2");
        Directory.CreateDirectory(outDir);

        await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);

        // Password-protected archives are user/input errors and must not be logged as warnings or errors
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    #endregion

    #region ExtractArchiveAsync - Logging Tests

    [Fact]
    public async Task ExtractArchiveAsyncLogsExtractionStart()
    {
        var zipPath = CreateTestZip("logtest.zip",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "f.txt", "d" } });
        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_log");

        await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);

        Assert.True(_logger.HasMessage("Starting extraction"));
    }

    [Fact]
    public async Task ExtractArchiveAsyncLogsSuccess()
    {
        var zipPath = CreateTestZip("success.zip",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "f.txt", "d" } });
        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_success");

        await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);

        Assert.True(_logger.HasMessage("Successfully extracted"));
    }

    [Fact]
    public async Task ExtractArchiveAsyncCancellationLogsCancellation()
    {
        var zipPath = CreateTestZip("cancellog.zip",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "f.txt", "d" } });
        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_cancel");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await service.ExtractArchiveAsync(zipPath, outDir, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.True(_logger.HasMessage("canceled"));
    }

    #endregion

    #region ExtractArchiveAsync - Zip Slip Prevention Tests

    [Fact]
    public async Task ExtractArchiveAsyncZipSlipAbsolutePathEntrySkipsEntry()
    {
        // Create a zip with a path traversal attempt
        var zipPath = Path.Combine(_tempDir, "zipslip.zip");
        await using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../../../etc/passwd");
            await using var writer = new StreamWriter(entry.Open());
            writer.Write("malicious content");
        }

        var service = CreateService();
        var outDir = Path.Combine(_tempDir, "extract_zipslip");

        // Should not throw, but skip the malicious entry
        var result = await service.ExtractArchiveAsync(zipPath, outDir, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Contains(result.SkippedEntries, entry => entry.Contains("etc/passwd", StringComparison.Ordinal));
    }

    #endregion

    #region Helper Methods

    private static void CreateEncryptedZip(string zipPath, string entryName, string content, string _)
    {
        // Create a ZIP file with PKWARE traditional encryption
        // This is the simplest way to create an encrypted zip that SharpCompress will detect
        using var fs = File.Create(zipPath);
        using var bw = new BinaryWriter(fs);

        // Local file header
        bw.Write(0x04034B50); // signature
        bw.Write((ushort)20); // version needed
        bw.Write((ushort)1); // general purpose bit flag (bit 0 = encrypted)
        bw.Write((ushort)0); // compression method (stored)
        bw.Write((ushort)0); // last mod time
        bw.Write((ushort)0); // last mod date
        bw.Write((uint)0); // crc32
        bw.Write((uint)(content.Length + 12)); // compressed size (content + encryption header)
        bw.Write((uint)(content.Length + 12)); // uncompressed size
        bw.Write((ushort)entryName.Length);
        bw.Write((ushort)0); // extra field length

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(entryName);
        bw.Write(nameBytes);

        // Encryption header (12 bytes)
        var encHeader = new byte[12];
        new Random(42).NextBytes(encHeader);
        bw.Write(encHeader);

        // Content (encrypted - just raw bytes for testing)
        var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
        bw.Write(contentBytes);

        // Central directory
        var cdOffset = (uint)fs.Position;
        bw.Write(0x02014B50); // central directory signature
        bw.Write((ushort)20); // version made by
        bw.Write((ushort)20); // version needed
        bw.Write((ushort)1); // general purpose bit flag (encrypted)
        bw.Write((ushort)0); // compression method
        bw.Write((ushort)0); // last mod time
        bw.Write((ushort)0); // last mod date
        bw.Write((uint)0); // crc32
        bw.Write((uint)(content.Length + 12)); // compressed size
        bw.Write((uint)(content.Length + 12)); // uncompressed size
        bw.Write((ushort)nameBytes.Length);
        bw.Write((ushort)0); // extra field length
        bw.Write((ushort)0); // file comment length
        bw.Write((ushort)0); // disk number start
        bw.Write((ushort)0); // internal file attributes
        bw.Write((uint)0); // external file attributes
        bw.Write((uint)0); // relative offset of local header
        bw.Write(nameBytes);

        var cdSize = (uint)(fs.Position - cdOffset);

        // End of central directory
        bw.Write(0x06054B50); // end of central directory signature
        bw.Write((ushort)0); // disk number
        bw.Write((ushort)0); // disk number with central directory
        bw.Write((ushort)1); // total entries on disk
        bw.Write((ushort)1); // total entries
        bw.Write(cdSize); // central directory size
        bw.Write(cdOffset); // central directory offset
        bw.Write((ushort)0); // comment length
    }

    #endregion
}