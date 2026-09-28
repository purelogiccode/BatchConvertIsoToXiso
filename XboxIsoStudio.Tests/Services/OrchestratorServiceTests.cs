using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;
using Moq;
using Xunit;

namespace XboxIsoStudio.Tests.Services;

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

    #region Delete Originals Tests

    private static OrchestratorService CreateOrchestrator(
        Mock<IFileExtractor> extractor,
        FileProcessingStatus conversionStatus,
        bool integrityResult = false,
        Mock<IXisoSharpService>? xisoSharp = null)
    {
        var logger = new TestLogger();
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(long.MaxValue);
        var fileMover = new Mock<IFileMover>();
        fileMover.Setup(static m => m.MoveTestedFileAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string source, string destinationFolder, string _, CancellationToken _) =>
            {
                Directory.CreateDirectory(destinationFolder);
                File.Move(source, Path.Combine(destinationFolder, Path.GetFileName(source)), true);
                return Task.CompletedTask;
            });
        var integrity = new Mock<IXisoIntegrityService>();
        integrity.Setup(static i => i.TestIsoIntegrityAsync(It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(integrityResult);

        if (xisoSharp == null)
        {
            xisoSharp = new Mock<IXisoSharpService>();
            xisoSharp.Setup(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversionStatus);
        }

        return new OrchestratorService(extractor.Object, fileMover.Object, logger.Logger,
            integrity.Object, xisoSharp.Object, diskMonitor.Object);
    }

    private static Task<CloudRetryResult> CloudRetrySkip(string fileName)
    {
        return Task.FromResult(CloudRetryResult.Skip);
    }

    private Task RunConvertAsync(OrchestratorService orchestrator, bool deleteOriginals)
    {
        return orchestrator.ConvertAsync(_tempDir, Path.Combine(_tempDir, "out"), deleteOriginals, false, false,
            OutputFormat.Xiso, false, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsRemovesOriginalAfterSuccessfulConversion()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.False(File.Exists(isoPath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsOriginalWhenAlreadyOptimized()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
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
        var orchestrator = CreateOrchestrator(extractor,
            FileProcessingStatus.AlreadyOptimized);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(archivePath));
    }

    #endregion

    #region File List Overload Tests

    [Fact]
    public async Task ConvertFilesAsyncProcessesOnlyProvidedFiles()
    {
        var selected = CreateTempFile("game1.iso", "iso data");
        var notSelected = CreateTempFile("game2.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted);

        await orchestrator.ConvertFilesAsync([selected], Path.Combine(_tempDir, "out"), true, false, false, OutputFormat.Xiso,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(notSelected));
    }

    [Fact]
    public async Task ConvertFilesAsyncEmptyListDoesNothing()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted);

        await orchestrator.ConvertFilesAsync([], Path.Combine(_tempDir, "out"), true, false, false, OutputFormat.Xiso,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task ConvertFilesAsyncFiltersUnsupportedExtensions()
    {
        var binPath = CreateTempFile("game.bin", "bin data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted);

        await orchestrator.ConvertFilesAsync([binPath], Path.Combine(_tempDir, "out"), true, false, false, OutputFormat.Xiso,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(binPath));
    }

    [Fact]
    public async Task TestFilesAsyncOnlyTestsProvidedFiles()
    {
        var selected = CreateTempFile("game1.iso", "iso data");
        var notSelected = CreateTempFile("game2.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: true);

        await orchestrator.TestFilesAsync(_tempDir, [selected], true, false, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.False(File.Exists(selected));
        Assert.True(File.Exists(Path.Combine(_tempDir, "_success", "game1.iso")));
        Assert.True(File.Exists(notSelected));
    }

    [Fact]
    public async Task TestFilesAsyncFailedFileMovesToFailedFolder()
    {
        var selected = CreateTempFile("game1.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: false);

        await orchestrator.TestFilesAsync(_tempDir, [selected], false, true, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_tempDir, "_failed", "game1.iso")));
    }

    [Fact]
    public async Task TestFilesAsyncEmptyListDoesNothing()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: true);

        await orchestrator.TestFilesAsync(_tempDir, [], true, true, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(isoPath));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "_success")));
    }

    [Theory]
    [InlineData("game.cso")]
    [InlineData("game.1.cso")]
    [InlineData("game.zar")]
    public async Task TestFilesAsyncTestsCsoAndZarImages(string fileName)
    {
        var imagePath = CreateTempFile(fileName, "image data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: true);

        await orchestrator.TestFilesAsync(_tempDir, [imagePath], true, false, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_tempDir, "_success", fileName)));
    }

    [Fact]
    public async Task TestFilesAsyncSkipsSplitCisoContinuationParts()
    {
        var part2 = CreateTempFile("game.2.cso", "part 2");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: true);

        await orchestrator.TestFilesAsync(_tempDir, [part2], true, false, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(part2));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "_success")));
    }

    [Fact]
    public async Task TestFilesAsyncMovesSplitCisoPartsWithFirstPart()
    {
        var part1 = CreateTempFile("game.1.cso", "part 1");
        var part2 = CreateTempFile("game.2.cso", "part 2");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(),
            FileProcessingStatus.Converted, integrityResult: true);

        await orchestrator.TestFilesAsync(_tempDir, [part1], true, false, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(_tempDir, "_success", "game.1.cso")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "_success", "game.2.cso")));
        Assert.False(File.Exists(part1));
        Assert.False(File.Exists(part2));
    }

    [Fact]
    public async Task ConvertFilesAsyncZarOutputPassesZarFormatAndExtension()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        string? capturedName = null;
        var capturedFormat = OutputFormat.Xiso;
        var xisoSharp = new Mock<IXisoSharpService>();
        xisoSharp.Setup(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string outputName, OutputFormat format, bool _, bool _,
                IProgress<BatchOperationProgress> _, CancellationToken _) =>
            {
                capturedName = outputName;
                capturedFormat = format;
                return FileProcessingStatus.Converted;
            });

        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            xisoSharp: xisoSharp);

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), false, false, false,
            OutputFormat.Zar, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.Equal("game.zar", capturedName);
        Assert.Equal(OutputFormat.Zar, capturedFormat);
    }

    [Fact]
    public async Task ConvertFilesAsyncCsoOutputPassesCsoFormatAndExtension()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        string? capturedName = null;
        var capturedFormat = OutputFormat.Xiso;
        var xisoSharp = new Mock<IXisoSharpService>();
        xisoSharp.Setup(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string outputName, OutputFormat format, bool _, bool _,
                IProgress<BatchOperationProgress> _, CancellationToken _) =>
            {
                capturedName = outputName;
                capturedFormat = format;
                return FileProcessingStatus.Converted;
            });

        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            xisoSharp: xisoSharp);

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), false, false, false,
            OutputFormat.Cso, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.Equal("game.cso", capturedName);
        Assert.Equal(OutputFormat.Cso, capturedFormat);
    }

    #endregion
}