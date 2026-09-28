using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using BatchConvertIsoToXiso.Services;
using Moq;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class OrchestratorServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"OrchestratorTests_{Guid.NewGuid():N}");
    private readonly List<string> _tempFiles = [];

    public OrchestratorServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
                // ignored
            }
        }

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

    private string CreateTempFile(string name, string content = "")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    #region IsFatalEnvironmentalError Tests

    [Fact]
    public void IsFatalEnvironmentalErrorDirectoryNotFoundExceptionReturnsTrue()
    {
        var ex = new DirectoryNotFoundException("Path not found");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Theory]
    [InlineData(0x15, "The device is not ready")] // ERROR_NOT_READY
    [InlineData(0x03, "The system cannot find the path")] // ERROR_PATH_NOT_FOUND
    [InlineData(0x0F, "The system cannot find the drive")] // ERROR_INVALID_DRIVE
    [InlineData(0x37, "The device does not exist")] // ERROR_DEV_NOT_EXIST
    [InlineData(0x40, "The network name is no longer available")] // ERROR_NETNAME_DELETED
    [InlineData(0x45D, "The request could not be performed because of an I/O device error")] // ERROR_IO_DEVICE
    public void IsFatalEnvironmentalErrorIoExceptionWithFatalHResultReturnsTrue(int hresult, string message)
    {
        var ex = new IOException(message, hresult);
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Theory]
    [InlineData(0x02, "File not found")] // ERROR_FILE_NOT_FOUND - not fatal
    [InlineData(0x05, "Access denied")] // ERROR_ACCESS_DENIED - not fatal
    [InlineData(0x20, "Sharing violation")] // ERROR_SHARING_VIOLATION - not fatal
    public void IsFatalEnvironmentalErrorIoExceptionWithNonFatalHResultReturnsFalse(int hresult, string message)
    {
        var ex = new IOException(message, hresult);
        Assert.False(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithDeviceMessageReturnsTrue()
    {
        var ex = new IOException("The device is not ready.");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithNetworkNameMessageReturnsTrue()
    {
        var ex = new IOException("The network name is no longer available.");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithCzechDeviceMessageReturnsTrue()
    {
        var ex = new IOException("Zařízení není připraveno");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithLocalizedDeviceIoMessageReturnsTrue()
    {
        var ex = new IOException("Impossibile eseguire la richiesta a causa di un errore di dispositivo I/O.");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithGenericMessageReturnsFalse()
    {
        var ex = new IOException("Something went wrong");
        Assert.False(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorArgumentExceptionReturnsFalse()
    {
        var ex = new ArgumentException("Bad argument");
        Assert.False(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorInvalidOperationExceptionReturnsFalse()
    {
        var ex = new InvalidOperationException("Invalid operation");
        Assert.False(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorFileNotFoundExceptionReturnsFalse()
    {
        var ex = new FileNotFoundException("File not found");
        Assert.False(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    [Fact]
    public void IsFatalEnvironmentalErrorIoExceptionWithCaseInsensitiveDeviceMessageReturnsTrue()
    {
        var ex = new IOException("The DEVICE is not available");
        Assert.True(OrchestratorService.IsFatalEnvironmentalError(ex));
    }

    #endregion

    #region GetReferencedBinFilesFromCue Tests

    [Fact]
    public void GetReferencedBinFilesFromCueValidCueFileReturnsBinPaths()
    {
        var cuePath = CreateTempFile("game.cue",
            "FILE \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n    INDEX 01 00:00:00\n");
        var expectedBin = Path.Combine(_tempDir, "game.bin");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Single(result);
        Assert.Equal(expectedBin, result[0]);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueMultipleFilesReturnsAll()
    {
        var cuePath = CreateTempFile("multi.cue",
            "FILE \"track01.bin\" BINARY\n  TRACK 01 MODE1/2352\n" +
            "FILE \"track02.bin\" BINARY\n  TRACK 02 AUDIO\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.EndsWith("track01.bin", StringComparison.Ordinal));
        Assert.Contains(result, p => p.EndsWith("track02.bin", StringComparison.Ordinal));
    }

    [Fact]
    public void GetReferencedBinFilesFromCueDuplicateFilesReturnsUnique()
    {
        var cuePath = CreateTempFile("dupes.cue",
            "FILE \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n" +
            "FILE \"game.bin\" BINARY\n  TRACK 02 AUDIO\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Single(result);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueEmptyFileReturnsEmpty()
    {
        var cuePath = CreateTempFile("empty.cue", "");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Empty(result);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueNoFileLinesReturnsEmpty()
    {
        var cuePath = CreateTempFile("nofile.cue",
            "TRACK 01 MODE1/2352\n  INDEX 01 00:00:00\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Empty(result);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueNonExistentFileReturnsEmpty()
    {
        var result = OrchestratorService.GetReferencedBinFilesFromCue(Path.Combine(_tempDir, "nonexistent.cue"));

        Assert.Empty(result);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueFileNameWithSpacesReturnsCorrectly()
    {
        var cuePath = CreateTempFile("spaces.cue",
            "FILE \"my game file.bin\" BINARY\n  TRACK 01 MODE1/2352\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Single(result);
        Assert.Contains("my game file.bin", result[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueFileLineWithoutQuotesReturnsEmpty()
    {
        var cuePath = CreateTempFile("noquotes.cue",
            "FILE game.bin BINARY\n  TRACK 01 MODE1/2352\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Empty(result);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueCaseInsensitiveFileKeywordReturnsBinPaths()
    {
        var cuePath = CreateTempFile("case.cue",
            "file \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Single(result);
        Assert.Contains("game.bin", result[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetReferencedBinFilesFromCueLeadingTrailingWhitespaceTrimsCorrectly()
    {
        var cuePath = CreateTempFile("whitespace.cue",
            "   FILE \"game.bin\" BINARY   \n  TRACK 01 MODE1/2352\n");

        var result = OrchestratorService.GetReferencedBinFilesFromCue(cuePath);

        Assert.Single(result);
        Assert.Contains("game.bin", result[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Delete Originals Tests

    private static OrchestratorService CreateOrchestrator(
        Mock<IFileExtractor> extractor,
        Mock<IExternalToolService> externalTool,
        FileProcessingStatus conversionStatus)
    {
        var logger = new TestLogger();
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(long.MaxValue);
        var fileMover = new Mock<IFileMover>();
        var integrity = new Mock<IXisoIntegrityService>();

        var xisoSharp = new Mock<IXisoSharpService>();
        xisoSharp.Setup(static s => s.ConvertIsoToXisoAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversionStatus);

        return new OrchestratorService(externalTool.Object, extractor.Object, fileMover.Object, logger.Logger,
            integrity.Object, xisoSharp.Object, diskMonitor.Object);
    }

    private static Task<CloudRetryResult> CloudRetrySkip(string fileName)
    {
        return Task.FromResult(CloudRetryResult.Skip);
    }

    private Task RunConvertAsync(OrchestratorService orchestrator, bool deleteOriginals)
    {
        return orchestrator.ConvertAsync(_tempDir, Path.Combine(_tempDir, "out"), deleteOriginals, false, false,
            false, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsRemovesOriginalAfterSuccessfulConversion()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), new Mock<IExternalToolService>(),
            FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.False(File.Exists(isoPath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsOriginalWhenAlreadyOptimized()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), new Mock<IExternalToolService>(),
            FileProcessingStatus.AlreadyOptimized);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsArchiveWhenEveryEntryIsSkipped()
    {
        var archivePath = CreateTempFile("games.zip", "archive data");
        var extractor = new Mock<IFileExtractor>();
        extractor.Setup(static e => e.GetArchiveInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((100L, 1));
        extractor.Setup(static e => e.ExtractArchiveAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string extractionPath, CancellationToken _) =>
            {
                Directory.CreateDirectory(extractionPath);
                File.WriteAllText(Path.Combine(extractionPath, "game.iso"), "iso data");
                return true;
            });
        var orchestrator = CreateOrchestrator(extractor, new Mock<IExternalToolService>(),
            FileProcessingStatus.AlreadyOptimized);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(archivePath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsCueAndBinWhenConversionIsSkipped()
    {
        var binPath = CreateTempFile("game.bin", "bin data");
        var cuePath = CreateTempFile("game.cue", "FILE \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n");
        var externalTool = new Mock<IExternalToolService>();
        externalTool.Setup(static t => t.ConvertCueBinToIsoAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string tempDir, CancellationToken _) =>
            {
                Directory.CreateDirectory(tempDir);
                var tempIso = Path.Combine(tempDir, "game.iso");
                File.WriteAllText(tempIso, "iso data");
                return tempIso;
            });
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), externalTool,
            FileProcessingStatus.AlreadyOptimized);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(cuePath));
        Assert.True(File.Exists(binPath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsRemovesCueAndBinAfterSuccessfulConversion()
    {
        var binPath = CreateTempFile("game.bin", "bin data");
        var cuePath = CreateTempFile("game.cue", "FILE \"game.bin\" BINARY\n  TRACK 01 MODE1/2352\n");
        var externalTool = new Mock<IExternalToolService>();
        externalTool.Setup(static t => t.ConvertCueBinToIsoAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string tempDir, CancellationToken _) =>
            {
                Directory.CreateDirectory(tempDir);
                var tempIso = Path.Combine(tempDir, "game.iso");
                File.WriteAllText(tempIso, "iso data");
                return tempIso;
            });
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), externalTool,
            FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.False(File.Exists(cuePath));
        Assert.False(File.Exists(binPath));
    }

    #endregion
}