using CHDSharp;
using CHDSharp.Models;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;
using Moq;
using Serilog.Events;
using XISOSharp;
using Xunit;

namespace XboxIsoStudio.Tests.Services;

public sealed class ChdServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ChdServiceTests_{Guid.NewGuid():N}");
    private readonly TestLogger _logger = new();

    public ChdServiceTests()
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
        Assert.Equal(0, XisoWriter.PackFromDirectory(sourceDir, isoPath));
        return isoPath;
    }

    private static void RemoveOptimizedTag(string isoPath)
    {
        using var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
        stream.Write(new byte[Constants.OptimizedTagLength]);
    }

    private ChdService CreateService()
    {
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(long.MaxValue);
        var xisoSharpService = new XisoSharpService(_logger.Logger, diskMonitor.Object);
        return new ChdService(_logger.Logger, xisoSharpService);
    }

    private static void AssertValidDvdChd(string chdPath)
    {
        Assert.True(File.Exists(chdPath));

        Assert.Equal(ChdError.Chderrnone, Chd.Classify(chdPath, out var classification));
        Assert.Equal("dvd", classification);

        using var stream = File.OpenRead(chdPath);
        var result = Chd.CheckFile(stream, Path.GetFileName(chdPath), deepCheck: true);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ConvertOptimizedIsoProducesValidDvdChd()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoToChdAsync(isoPath, outputFolder, "game.chd", false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        AssertValidDvdChd(Path.Combine(outputFolder, "game.chd"));
    }

    [Fact]
    public async Task ConvertNonOptimizedIsoRewritesToGamePartitionBeforeEncoding()
    {
        var isoPath = CreateOptimizedXiso();
        RemoveOptimizedTag(isoPath);
        Assert.False(XisoReader.IsOptimizedImage(isoPath));

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoToChdAsync(isoPath, outputFolder, "game.chd", false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        AssertValidDvdChd(Path.Combine(outputFolder, "game.chd"));
    }

    [Fact]
    public async Task ConvertUsesSuppliedOutputNameVerbatim()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoToChdAsync(isoPath, outputFolder, "custom-name.chd", false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        AssertValidDvdChd(Path.Combine(outputFolder, "custom-name.chd"));
    }

    [Fact]
    public async Task OutputOverwritingSourceReturnsFailedAndKeepsSource()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();

        var status = await service.ConvertIsoToChdAsync(isoPath, _tempRoot, "game.iso", false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task MissingInputReturnsFailed()
    {
        var service = CreateService();

        var status = await service.ConvertIsoToChdAsync(Path.Combine(_tempRoot, "missing.iso"),
            Path.Combine(_tempRoot, "out"), "missing.chd", false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
    }

    [Fact]
    public async Task InvalidImageReturnsFailedWithoutBugReport()
    {
        var badIso = Path.Combine(_tempRoot, "bad.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var service = CreateService();

        var status = await service.ConvertIsoToChdAsync(badIso, Path.Combine(_tempRoot, "out"), "bad.chd", false,
            false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }
}
