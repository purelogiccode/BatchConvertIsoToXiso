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
        Mock<IXisoSharpService>? xisoSharp = null,
        Mock<IChdService>? chdService = null,
        Mock<IXisoIntegrityService>? integrity = null)
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
        if (integrity == null)
        {
            integrity = new Mock<IXisoIntegrityService>();
            integrity.Setup(static i => i.TestIsoIntegrityAsync(It.IsAny<string>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(integrityResult);
        }

        if (xisoSharp == null)
        {
            xisoSharp = new Mock<IXisoSharpService>();
            xisoSharp.Setup(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversionStatus);
        }

        if (chdService == null)
        {
            chdService = new Mock<IChdService>();
            chdService.Setup(static s => s.ConvertIsoToChdAsync(It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                    It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversionStatus);
        }

        return new OrchestratorService(extractor.Object, fileMover.Object, logger.Logger,
            integrity.Object, xisoSharp.Object, chdService.Object, diskMonitor.Object);
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
                return new ArchiveExtractionResult(true, []);
            });
        var orchestrator = CreateOrchestrator(extractor,
            FileProcessingStatus.AlreadyOptimized);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(archivePath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsArchiveWhenExtractorSkippedAnEntry()
    {
        var archivePath = CreateTempFile("games.zip", "archive data");
        var extractor = new Mock<IFileExtractor>();
        extractor.Setup(static e => e.GetArchiveInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((100L, 2));
        extractor.Setup(static e => e.ExtractArchiveAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string extractionPath, CancellationToken _) =>
            {
                Directory.CreateDirectory(extractionPath);
                File.WriteAllText(Path.Combine(extractionPath, "disc1.iso"), "iso data");
                // The built-in extractor leaves additional ISOs in the archive on purpose.
                return new ArchiveExtractionResult(true, ["disc2.iso"]);
            });
        var orchestrator = CreateOrchestrator(extractor, FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(archivePath));
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsKeepsArchiveWhenAnExtractedImageWasNotConverted()
    {
        var archivePath = CreateTempFile("games.zip", "archive data");
        var extractor = new Mock<IFileExtractor>();
        extractor.Setup(static e => e.GetArchiveInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((100L, 2));
        extractor.Setup(static e => e.ExtractArchiveAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string extractionPath, CancellationToken _) =>
            {
                Directory.CreateDirectory(extractionPath);
                File.WriteAllText(Path.Combine(extractionPath, "game.iso"), "iso data");
                File.WriteAllText(Path.Combine(extractionPath, "extra.cso"), "cso data");
                return new ArchiveExtractionResult(true, []);
            });
        var orchestrator = CreateOrchestrator(extractor, FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.True(File.Exists(archivePath));
    }

    [Fact]
    public async Task ConvertFilesAsyncDeleteOriginalsKeepsArchiveWhenAnExtractedImageIsInvalid()
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
                return new ArchiveExtractionResult(true, []);
            });
        var orchestrator = CreateOrchestrator(extractor, FileProcessingStatus.InvalidInput);
        var progress = new CollectingProgress();

        await orchestrator.ConvertFilesAsync([archivePath], Path.Combine(_tempDir, "out"), true, false, false,
            OutputFormat.Xiso, progress, CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(archivePath));
        Assert.Contains(progress.Reports, p => p.InvalidIsoCount == 1);
    }

    [Fact]
    public async Task ConvertAsyncDeleteOriginalsRemovesArchiveWhenEveryExtractedImageIsConverted()
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
                return new ArchiveExtractionResult(true, []);
            });
        var orchestrator = CreateOrchestrator(extractor, FileProcessingStatus.Converted);

        await RunConvertAsync(orchestrator, true);

        Assert.False(File.Exists(archivePath));
    }

    #endregion

    #region Output Name Collision Tests

    [Fact]
    public async Task ConvertFilesAsyncSameNamedInputsGetDistinctOutputNames()
    {
        var disc1 = Path.Combine(_tempDir, "Disc1");
        var disc2 = Path.Combine(_tempDir, "Disc2");
        Directory.CreateDirectory(disc1);
        Directory.CreateDirectory(disc2);
        var file1 = Path.Combine(disc1, "game.iso");
        var file2 = Path.Combine(disc2, "game.iso");
        File.WriteAllText(file1, "iso data");
        File.WriteAllText(file2, "iso data");

        var outputNames = new List<string>();
        var xisoSharp = new Mock<IXisoSharpService>();
        xisoSharp.Setup(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string outputName, OutputFormat _, bool _, bool _,
                IProgress<BatchOperationProgress> _, CancellationToken _) =>
            {
                outputNames.Add(outputName);
                return FileProcessingStatus.Converted;
            });

        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            xisoSharp: xisoSharp);

        await orchestrator.ConvertFilesAsync([file1, file2], Path.Combine(_tempDir, "out"), true, false, false,
            OutputFormat.Xiso, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.Equal(2, outputNames.Count);
        Assert.Equal(2, outputNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(outputNames, name => name.Equals("game.iso", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(outputNames, name => name.Equals("game (2).iso", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(file1));
        Assert.False(File.Exists(file2));
    }

    #endregion

    #region Test Error Handling Tests

    [Fact]
    public async Task TestFilesAsyncContinuesAfterUnreadableFile()
    {
        // A directory with an image extension cannot be opened as a file; before the fix
        // the resulting exception aborted the whole batch.
        var unreadablePath = Path.Combine(_tempDir, "unreadable.iso");
        Directory.CreateDirectory(unreadablePath);
        var goodPath = CreateTempFile("good.iso", "iso data");

        var testedPaths = new List<string>();
        var integrity = new Mock<IXisoIntegrityService>();
        integrity.Setup(static i => i.TestIsoIntegrityAsync(It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, bool _, IProgress<BatchOperationProgress> _, CancellationToken _) =>
            {
                testedPaths.Add(path);
                return true;
            });

        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            integrity: integrity);

        await orchestrator.TestFilesAsync(_tempDir, [unreadablePath, goodPath], false, false, false,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.Contains(testedPaths, path => path.Equals(goodPath, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Test Temp Cleanup Tests

    [Fact]
    public async Task TestFilesAsyncCleansUpTempCopyWhenCloudCopyFails()
    {
        var isoPath = CreateTempFile("locked.iso", "iso data");
        var tempRoot = Path.Combine(Path.GetTempPath(), "XboxIsoStudio_Test");
        var before = Directory.Exists(tempRoot)
            ? Directory.GetDirectories(tempRoot).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            integrityResult: true);

        // Holding the file without sharing makes the readability probe fail, so the
        // orchestrator takes the cloud-copy path — which then also fails.
        await using (new FileStream(isoPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await orchestrator.TestFilesAsync(_tempDir, [isoPath], false, false, false,
                new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);
        }

        var after = Directory.Exists(tempRoot)
            ? Directory.GetDirectories(tempRoot).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Empty(after.Except(before, StringComparer.OrdinalIgnoreCase));
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

        await orchestrator.ConvertFilesAsync([selected], Path.Combine(_tempDir, "out"), true, false, false,
            OutputFormat.Xiso,
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

        await orchestrator.ConvertFilesAsync([binPath], Path.Combine(_tempDir, "out"), true, false, false,
            OutputFormat.Xiso,
            new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.True(File.Exists(binPath));
    }

    [Fact]
    public async Task ConvertFilesAsyncEngineSkippedStatusCountsAsSkipped()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Skipped);
        var progress = new CollectingProgress();

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), false, false, false,
            OutputFormat.Xiso, progress, CloudRetrySkip, CancellationToken.None);

        Assert.Contains(progress.Reports, p => p.SkippedCount == 1);
        Assert.DoesNotContain(progress.Reports, p => p.FailedCount > 0);
    }

    [Fact]
    public async Task ConvertFilesAsyncEngineInvalidInputCountsAsFailedAndInvalidIso()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.InvalidInput);
        var progress = new CollectingProgress();

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), true, false, false,
            OutputFormat.Xiso, progress, CloudRetrySkip, CancellationToken.None);

        Assert.Contains(progress.Reports, p => p.FailedCount == 1 && p.InvalidIsoCount == 1);
        // An invalid input must never lead to deletion of the original file.
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task ConvertFilesAsyncEngineFailureDoesNotCountAsInvalidIso()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Failed);
        var progress = new CollectingProgress();

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), false, false, false,
            OutputFormat.Xiso, progress, CloudRetrySkip, CancellationToken.None);

        Assert.Contains(progress.Reports, p => p.FailedCount == 1);
        Assert.DoesNotContain(progress.Reports, p => p.InvalidIsoCount > 0);
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
    [InlineData("game.chd")]
    public async Task TestFilesAsyncTestsCsoZarAndChdImages(string fileName)
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

    [Fact]
    public async Task ConvertFilesAsyncChdOutputRoutesToChdServiceWithChdExtension()
    {
        var isoPath = CreateTempFile("game.iso", "iso data");
        string? capturedName = null;
        var chdService = new Mock<IChdService>();
        chdService.Setup(static s => s.ConvertIsoToChdAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string outputName, bool _, bool _,
                IProgress<BatchOperationProgress> _, CancellationToken _) =>
            {
                capturedName = outputName;
                return FileProcessingStatus.Converted;
            });

        var xisoSharp = new Mock<IXisoSharpService>();
        var orchestrator = CreateOrchestrator(new Mock<IFileExtractor>(), FileProcessingStatus.Converted,
            xisoSharp: xisoSharp, chdService: chdService);

        await orchestrator.ConvertFilesAsync([isoPath], Path.Combine(_tempDir, "out"), false, false, false,
            OutputFormat.Chd, new Progress<BatchOperationProgress>(), CloudRetrySkip, CancellationToken.None);

        Assert.Equal("game.chd", capturedName);
        xisoSharp.Verify(static s => s.ConvertIsoAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<OutputFormat>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<IProgress<BatchOperationProgress>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
