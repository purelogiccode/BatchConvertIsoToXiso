using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using BatchConvertIsoToXiso.Services;
using Moq;
using XISOSharp;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public sealed class XisoSharpServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"XisoSharpServiceTests_{Guid.NewGuid():N}");

    public XisoSharpServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignored
        }
    }

    private string CreateOptimizedXiso(string name = "game.iso")
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "default.xbe"), "fake xbe content");
        var mediaDir = Path.Combine(sourceDir, "media");
        Directory.CreateDirectory(mediaDir);
        File.WriteAllBytes(Path.Combine(mediaDir, "data.bin"), new byte[4096]);

        var isoPath = Path.Combine(_tempRoot, name);
        var result = XisoWriter.PackFromDirectory(sourceDir, isoPath);
        Assert.Equal(0, result);
        return isoPath;
    }

    private static XisoSharpService CreateService(Mock<IBugReportService>? bugReport = null)
    {
        var logger = new Mock<ILogger>();
        bugReport ??= new Mock<IBugReportService>();
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(long.MaxValue);
        return new XisoSharpService(logger.Object, bugReport.Object, diskMonitor.Object);
    }

    [Fact]
    public async Task AlreadyOptimizedImageReturnsAlreadyOptimized()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoToXisoAsync(isoPath, outputFolder, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.AlreadyOptimized, status);
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task OutputOverwritingSourceReturnsFailedAndKeepsSource()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();

        var status = await service.ConvertIsoToXisoAsync(isoPath, _tempRoot, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task InvalidImageReturnsFailedWithoutBugReport()
    {
        var badIso = Path.Combine(_tempRoot, "bad.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var bugReport = new Mock<IBugReportService>();
        var service = CreateService(bugReport);

        var status = await service.ConvertIsoToXisoAsync(badIso, Path.Combine(_tempRoot, "out"), false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task NonOptimizedImageIsConvertedToOutputFolder()
    {
        var isoPath = CreateOptimizedXiso();

        // Clear the optimized tag so the image is treated as a standard (non-optimized) XISO
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        Assert.False(XisoReader.IsOptimizedImage(isoPath));

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoToXisoAsync(isoPath, outputFolder, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var outputPath = Path.Combine(outputFolder, "game.iso");
        Assert.True(File.Exists(outputPath));
        Assert.True(XisoReader.AuditXiso(outputPath).IsValid);
    }

    [Fact]
    public async Task MissingInputReturnsFailed()
    {
        var service = CreateService();

        var status = await service.ConvertIsoToXisoAsync(Path.Combine(_tempRoot, "missing.iso"),
            Path.Combine(_tempRoot, "out"), false, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
    }
}
