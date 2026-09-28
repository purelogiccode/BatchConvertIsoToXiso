using BatchConvertIsoToXiso.Models;
using BatchConvertIsoToXiso.Services;
using Serilog.Events;
using XISOSharp;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public sealed class XisoIntegrityServiceTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"XisoIntegrityServiceTests_{Guid.NewGuid():N}");

    private readonly TestLogger _logger = new();

    public XisoIntegrityServiceTests()
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
        File.WriteAllBytes(Path.Combine(mediaDir, "data.bin"), new byte[8192]);

        var isoPath = Path.Combine(_tempRoot, name);
        var result = XisoWriter.PackFromDirectory(sourceDir, isoPath);
        Assert.Equal(0, result);
        return isoPath;
    }

    private static void RemoveOptimizedTag(string isoPath)
    {
        using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.Seek(Constants.OptimizedTagOffset, SeekOrigin.Begin);
        fs.Write(new byte[Constants.OptimizedTagLength]);
    }

    private static void CorruptRootDirectoryPointer(string isoPath)
    {
        using var fs = new FileStream(isoPath, FileMode.Open, FileAccess.Write, FileShare.None);
        fs.Seek(Constants.HeaderOffset + 20, SeekOrigin.Begin);
        fs.Write(BitConverter.GetBytes(0xFFFFFFF0u));
    }

    private XisoIntegrityService CreateService()
    {
        return new XisoIntegrityService(_logger.Logger);
    }

    [Fact]
    public async Task ValidXisoPassesStructuralTest()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(isoPath, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.True(passed);
    }

    [Fact]
    public async Task ValidXisoPassesDeepScan()
    {
        var isoPath = CreateOptimizedXiso();
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(isoPath, true, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.True(passed);
    }

    [Fact]
    public async Task RawNonOptimizedIsoPassesStructuralTest()
    {
        var isoPath = CreateOptimizedXiso();
        RemoveOptimizedTag(isoPath);
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(isoPath, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.True(passed);
        Assert.True(_logger.HasMessage("not optimized"));
    }

    [Fact]
    public async Task RawNonOptimizedIsoPassesDeepScan()
    {
        var isoPath = CreateOptimizedXiso();
        RemoveOptimizedTag(isoPath);
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(isoPath, true, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.True(passed);
    }

    [Fact]
    public async Task RawNonOptimizedIsoWithCorruptTreeFails()
    {
        var isoPath = CreateOptimizedXiso();
        RemoveOptimizedTag(isoPath);
        CorruptRootDirectoryPointer(isoPath);
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(isoPath, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.False(passed);
    }

    [Fact]
    public async Task InvalidImageFailsWithoutBugReport()
    {
        var badIso = Path.Combine(_tempRoot, "bad.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(badIso, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.False(passed);
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public async Task MissingFileFailsWithoutBugReport()
    {
        var service = CreateService();

        var passed = await service.TestIsoIntegrityAsync(Path.Combine(_tempRoot, "missing.iso"), false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.False(passed);
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }
}