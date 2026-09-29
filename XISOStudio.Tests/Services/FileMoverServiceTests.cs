using XISOStudio.Interfaces;
using XISOStudio.Services;
using Moq;
using Xunit;

namespace XISOStudio.Tests.Services;

public class FileMoverServiceTests : IDisposable
{
    private readonly TestLogger _logger = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"FileMoverTests_{Guid.NewGuid():N}");

    public FileMoverServiceTests()
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

    private FileMoverService CreateService(long availableFreeSpace)
    {
        var diskMonitor = new Mock<IDiskMonitorService>();
        diskMonitor.Setup(static d => d.GetAvailableFreeSpace(It.IsAny<string>())).Returns(availableFreeSpace);
        return new FileMoverService(_logger.Logger, diskMonitor.Object);
    }

    [Fact]
    public async Task MoveTestedFileAsyncSameVolumeMovesFileDespiteLowReportedFreeSpace()
    {
        var source = Path.Combine(_tempDir, "game.iso");
        await File.WriteAllTextAsync(source, new string('x', 4096));
        var destinationFolder = Path.Combine(_tempDir, "_success");
        var service = CreateService(1);

        await service.MoveTestedFileAsync(source, destinationFolder, "successfully tested", CancellationToken.None);

        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destinationFolder, "game.iso")));
    }

    [Fact]
    public async Task MoveTestedFileAsyncSkipsWhenDestinationAlreadyExists()
    {
        var source = Path.Combine(_tempDir, "game.iso");
        await File.WriteAllTextAsync(source, "source data");
        var destinationFolder = Path.Combine(_tempDir, "_success");
        Directory.CreateDirectory(destinationFolder);
        await File.WriteAllTextAsync(Path.Combine(destinationFolder, "game.iso"), "existing data");
        var service = CreateService(long.MaxValue);

        await service.MoveTestedFileAsync(source, destinationFolder, "successfully tested", CancellationToken.None);

        Assert.True(File.Exists(source));
        Assert.Equal("existing data", await File.ReadAllTextAsync(Path.Combine(destinationFolder, "game.iso")));
    }
}