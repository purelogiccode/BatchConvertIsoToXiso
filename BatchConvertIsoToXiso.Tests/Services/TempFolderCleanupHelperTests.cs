using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Services;
using Moq;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class TempFolderCleanupHelperTests
{
    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncNonExistentDirectoryDoesNotThrow()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"NonExistent_Test_{Guid.NewGuid()}");
        var mockLogger = new Mock<ILogger>();

        var exception = await Record.ExceptionAsync(() =>
            TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(nonExistentPath, 3, 100, mockLogger.Object));

        Assert.Null(exception);
        mockLogger.Verify(
            static x => x.LogMessage(It.Is<string>(static s => s.Contains("WARNING") || s.Contains("Failed"))),
            Times.Never);
    }

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncExistingEmptyDirectoryGetsDeleted()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"BatchConvertIsoToXiso_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var mockLogger = new Mock<ILogger>();

        try
        {
            Assert.True(Directory.Exists(tempDir));

            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 100, mockLogger.Object);

            Assert.False(Directory.Exists(tempDir));
            mockLogger.Verify(static x => x.LogMessage(It.Is<string>(static s => s.Contains("Successfully deleted"))),
                Times.Once);
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
        var mockLogger = new Mock<ILogger>();

        try
        {
            await TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 100, mockLogger.Object);

            mockLogger.Verify(
                static x => x.LogMessage(It.Is<string>(static s => s.Contains("Successfully deleted temp folder:"))),
                Times.Once);
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
        var mockLogger = new Mock<ILogger>();

        await using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var exception = await Record.ExceptionAsync(() =>
                TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 2, 10, mockLogger.Object));

            Assert.Null(exception);
            mockLogger.Verify(
                static x => x.LogMessage(It.Is<string>(static s => s.Contains("WARNING: Could not delete"))),
                Times.Once);
        }

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}