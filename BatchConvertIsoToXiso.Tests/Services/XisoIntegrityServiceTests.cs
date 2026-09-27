using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using BatchConvertIsoToXiso.Services;
using Moq;
using XISOSharp;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public sealed class XisoIntegrityServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"XisoIntegrityServiceTests_{Guid.NewGuid():N}");

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

    private static XisoIntegrityService CreateService(Mock<IBugReportService>? bugReport = null)
    {
        var logger = new Mock<ILogger>();
        bugReport ??= new Mock<IBugReportService>();
        return new XisoIntegrityService(logger.Object, bugReport.Object);
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
    public async Task InvalidImageFailsWithoutBugReport()
    {
        var badIso = Path.Combine(_tempRoot, "bad.iso");
        File.WriteAllText(badIso, "this is not an xiso image");
        var bugReport = new Mock<IBugReportService>();
        var service = CreateService(bugReport);

        var passed = await service.TestIsoIntegrityAsync(badIso, false, new Progress<BatchOperationProgress>(),
            CancellationToken.None);

        Assert.False(passed);
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task MissingFileFailsWithoutBugReport()
    {
        var bugReport = new Mock<IBugReportService>();
        var service = CreateService(bugReport);

        var passed = await service.TestIsoIntegrityAsync(Path.Combine(_tempRoot, "missing.iso"), false,
            new Progress<BatchOperationProgress>(), CancellationToken.None);

        Assert.False(passed);
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }
}
