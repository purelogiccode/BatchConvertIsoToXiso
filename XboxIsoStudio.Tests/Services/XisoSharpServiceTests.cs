using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;
using Moq;
using Serilog.Events;
using XISOSharp;
using Xunit;
using ZArchiveSharp;

namespace XboxIsoStudio.Tests.Services;

public sealed class XisoSharpServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"XisoSharpServiceTests_{Guid.NewGuid():N}");
    private readonly TestLogger _logger = new();

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

    private XisoSharpService CreateService()
    {
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(long.MaxValue);
        return new XisoSharpService(_logger.Logger, diskMonitor.Object);
    }

    [Fact]
    public async Task AlreadyOptimizedImageReturnsAlreadyOptimized()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.iso", OutputFormat.Xiso, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.AlreadyOptimized, status);
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task AlreadyOptimizedImageKeepsExistingOutput()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");
        Directory.CreateDirectory(outputFolder);
        var existingOutput = Path.Combine(outputFolder, "game.iso");
        await File.WriteAllTextAsync(existingOutput, "existing output should survive");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.iso", OutputFormat.Xiso, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.AlreadyOptimized, status);
        Assert.True(File.Exists(existingOutput));
        Assert.Equal("existing output should survive", await File.ReadAllTextAsync(existingOutput));
    }

    [Fact]
    public async Task OutputOverwritingSourceReturnsFailedAndKeepsSource()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();

        var status = await service.ConvertIsoAsync(isoPath, _tempRoot, "game.iso", OutputFormat.Xiso, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        Assert.True(File.Exists(isoPath));
    }

    [Fact]
    public async Task InvalidImageReturnsFailedWithoutBugReport()
    {
        var badIso = Path.Combine(_tempRoot, "bad.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var service = CreateService();

        var status = await service.ConvertIsoAsync(badIso, Path.Combine(_tempRoot, "out"), "bad.iso", OutputFormat.Xiso,
            false,
            false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.InvalidInput, status);
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
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

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.iso", OutputFormat.Xiso, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var outputPath = Path.Combine(outputFolder, "game.iso");
        Assert.True(File.Exists(outputPath));
        Assert.True(XisoReader.AuditXiso(outputPath).IsValid);
    }

    [Fact]
    public async Task ExplicitOutputNameIsUsedForTemporaryInput()
    {
        var isoPath = CreateOptimizedXiso("iso_000001.iso");

        // Clear the optimized tag so the image is treated as a standard (non-optimized) XISO
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "My Game.iso", OutputFormat.Xiso, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        Assert.True(File.Exists(Path.Combine(outputFolder, "My Game.iso")));
        Assert.False(File.Exists(Path.Combine(outputFolder, "iso_000001.iso")));
    }

    [Fact]
    public async Task ExistingOutputIsReplacedWhenConversionProceeds()
    {
        var isoPath = CreateOptimizedXiso();

        // Clear the optimized tag so the image is treated as a standard (non-optimized) XISO
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");
        Directory.CreateDirectory(outputFolder);
        var outputPath = Path.Combine(outputFolder, "game.iso");
        await File.WriteAllTextAsync(outputPath, "stale output");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.iso", OutputFormat.Xiso, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        Assert.True(XisoReader.AuditXiso(outputPath).IsValid);
    }

    [Fact]
    public async Task MissingInputReturnsFailed()
    {
        var service = CreateService();

        var status = await service.ConvertIsoAsync(Path.Combine(_tempRoot, "missing.iso"),
            Path.Combine(_tempRoot, "out"), "missing.iso", OutputFormat.Xiso, false, false,
            new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
    }

    private string CreateXisoWithSystemUpdate(string name)
    {
        var sourceDir = Path.Combine(_tempRoot, "source-su");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "default.xbe"), "fake xbe content");
        var updateDir = Path.Combine(sourceDir, "$SystemUpdate");
        Directory.CreateDirectory(updateDir);
        File.WriteAllText(Path.Combine(updateDir, "su.bin"), "system update payload");

        var isoPath = Path.Combine(_tempRoot, name);
        Assert.Equal(0, XisoWriter.PackFromDirectory(sourceDir, isoPath));
        return isoPath;
    }

    [Fact]
    public async Task ZarOutputPacksAlreadyOptimizedImage()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.zar", OutputFormat.Zar, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var outputPath = Path.Combine(outputFolder, "game.zar");
        Assert.True(File.Exists(outputPath));

        // The archive must open and contain the game files.
        var extractDir = Path.Combine(_tempRoot, "zar-out");
        ZArchiveTool.Extract(outputPath, extractDir);
        Assert.True(File.Exists(Path.Combine(extractDir, "default.xbe")));
    }

    [Fact]
    public async Task ZarOutputWithIntegrityCheckAcceptsRawNonOptimizedImage()
    {
        var isoPath = CreateOptimizedXiso();

        // Clear the optimized tag so the image is treated as a standard (non-optimized) XISO
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        // The source audit must not require the optimized tag: raw dumps are packed as-is.
        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.zar", OutputFormat.Zar, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        Assert.True(File.Exists(Path.Combine(outputFolder, "game.zar")));
    }

    [Fact]
    public async Task ZarOutputWithSkipSystemUpdateExcludesUpdateFolder()
    {
        var isoPath = CreateXisoWithSystemUpdate("game-su.iso");
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game-su.zar", OutputFormat.Zar, true,
            false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var extractDir = Path.Combine(_tempRoot, "zar-su-out");
        ZArchiveTool.Extract(Path.Combine(outputFolder, "game-su.zar"), extractDir);
        Assert.True(File.Exists(Path.Combine(extractDir, "default.xbe")));
        Assert.False(Directory.Exists(Path.Combine(extractDir, "$SystemUpdate")));
    }

    [Fact]
    public async Task CsoOutputCompressesAlreadyOptimizedImage()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.cso", OutputFormat.Cso, false, false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var outputPath = Path.Combine(outputFolder, "game.cso");
        Assert.True(File.Exists(outputPath));
        Assert.True(CisoReader.IsCso(outputPath));

        // Round-trip: the CISO must decompress back to a valid XISO.
        var decompressed = Path.Combine(_tempRoot, "decompressed.iso");
        Assert.Equal(0, CisoReader.DecompressToIso(outputPath, decompressed));
        Assert.True(XisoReader.AuditXiso(decompressed).IsValid);
    }

    [Fact]
    public async Task CsoOutputFromNonOptimizedImageRewritesBeforeCompressing()
    {
        var isoPath = CreateOptimizedXiso();

        // Clear the optimized tag so the CSO path exercises the temporary XISO rewrite.
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game.cso", OutputFormat.Cso, false, true,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        var outputPath = Path.Combine(outputFolder, "game.cso");
        Assert.True(File.Exists(outputPath));
        Assert.True(CisoReader.IsCso(outputPath));

        var decompressed = Path.Combine(_tempRoot, "decompressed2.iso");
        Assert.Equal(0, CisoReader.DecompressToIso(outputPath, decompressed));
        Assert.True(XisoReader.AuditXiso(decompressed).IsValid);
    }

    [Fact]
    public async Task CsoOutputWithSkipSystemUpdateStripsUpdateFolderFromOptimizedImage()
    {
        var isoPath = CreateXisoWithSystemUpdate("game-su-cso.iso");
        Assert.True(XisoReader.IsOptimizedImage(isoPath));
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game-su.cso", OutputFormat.Cso, true,
            false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);

        using var explorer = ImageExplorerFactory.Open(Path.Combine(outputFolder, "game-su.cso"));
        var names = explorer.ListChildren("/").Select(static e => e.Name).ToList();
        Assert.Contains(names, name => name.Equals("default.xbe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("$SystemUpdate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task XisoOutputWithSkipSystemUpdateRewritesOptimizedImage()
    {
        var isoPath = CreateXisoWithSystemUpdate("game-su-xiso.iso");
        Assert.True(XisoReader.IsOptimizedImage(isoPath));
        var service = CreateService();
        var outputFolder = Path.Combine(_tempRoot, "out");

        var status = await service.ConvertIsoAsync(isoPath, outputFolder, "game-su.iso", OutputFormat.Xiso, true,
            false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);

        using var explorer = ImageExplorerFactory.Open(Path.Combine(outputFolder, "game-su.iso"));
        var names = explorer.ListChildren("/").Select(static e => e.Name).ToList();
        Assert.Contains(names, name => name.Equals("default.xbe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Equals("$SystemUpdate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CsoOutputSpaceCheckUsesCompressedEstimate()
    {
        var isoPath = CreateOptimizedXiso();
        var inputSize = new FileInfo(isoPath).Length;
        var halfSize = inputSize / 2;
        var csoRequired = halfSize + Math.Max(halfSize / 10, 200L * 1024 * 1024);
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(csoRequired + 1);
        var service = new XisoSharpService(_logger.Logger, diskMonitor.Object);

        var status = await service.ConvertIsoAsync(isoPath, Path.Combine(_tempRoot, "out"), "space.cso",
            OutputFormat.Cso, false, false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Converted, status);
        Assert.False(_logger.HasMessage("Not enough disk space"));
    }

    [Fact]
    public async Task XisoOutputSpaceCheckUsesRawSize()
    {
        var isoPath = CreateOptimizedXiso();

        // Clear the optimized tag so the raw-size pre-check runs instead of the skip path.
        await using (var stream = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            stream.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
            stream.Write(new byte[Constants.OptimizedTagLength]);
        }

        var inputSize = new FileInfo(isoPath).Length;
        var halfSize = inputSize / 2;
        var csoRequired = halfSize + Math.Max(halfSize / 10, 200L * 1024 * 1024);
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(csoRequired + 1);
        var service = new XisoSharpService(_logger.Logger, diskMonitor.Object);

        var status = await service.ConvertIsoAsync(isoPath, Path.Combine(_tempRoot, "out"), "space.iso",
            OutputFormat.Xiso, false, false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.Failed, status);
        Assert.True(_logger.HasMessage("Not enough disk space"));
    }

    [Fact]
    public async Task InvalidImageZarOutputReturnsFailed()
    {
        var badIso = Path.Combine(_tempRoot, "bad-zar.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var service = CreateService();

        var status = await service.ConvertIsoAsync(badIso, Path.Combine(_tempRoot, "out"), "bad-zar.zar",
            OutputFormat.Zar, false, false, new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.Equal(FileProcessingStatus.InvalidInput, status);
    }
}
