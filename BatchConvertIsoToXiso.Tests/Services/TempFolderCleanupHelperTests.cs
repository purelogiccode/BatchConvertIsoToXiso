using BatchConvertIsoToXiso.Services;
using Serilog.Events;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class TempFolderCleanupHelperTests
{
    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncNonExistentDirectoryDoesNotThrow()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_Test_{Guid.NewGuid()}");
        var logger = new TestLogger();

        var exception = await Record.ExceptionAsync(() =>
            TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(nonExistentPath, 3, 100, logger.Logger));

        Assert.Null(exception);
        Assert.DoesNotContain(logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncExistingEmptyDirectoryGetsDeleted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"BatchConvertIsoToXiso_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var logger = new TestLogger();

        try
        {
            Assert.True(Directory.Exists(tempDir));

            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 100, logger.Logger);

            Assert.False(Directory.Exists(tempDir));
            Assert.True(logger.HasMessage("Successfully deleted"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncNullLoggerDoesNotThrow()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_Test_{Guid.NewGuid()}");

        var exception = await Record.ExceptionAsync(() =>
            TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(nonExistentPath, 3, 100, null));

        Assert.Null(exception);
    }

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncExistingDirectoryCallsLogSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"BatchConvertIsoToXiso_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var logger = new TestLogger();

        try
        {
            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 100, logger.Logger);

            Assert.True(logger.HasMessage("Successfully deleted temp folder:"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncLockedDirectoryDoesNotThrowAndLogsWarning()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"BatchConvertIsoToXiso_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var lockedFile = Path.Combine(tempDir, "locked.txt");
        File.WriteAllText(lockedFile, "locked");
        var logger = new TestLogger();

        await using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = await Record.ExceptionAsync(() =>
                TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 2, 10, logger.Logger));

            Assert.Null(exception);
            Assert.True(logger.HasMessage(LogEventLevel.Warning, "Could not delete"));
        }

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}