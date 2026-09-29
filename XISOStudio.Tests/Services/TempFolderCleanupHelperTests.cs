using XISOStudio.Services;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

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
        var tempDir = Path.Combine(Path.GetTempPath(), $"XISOStudio_Test_{Guid.NewGuid()}");
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
        var tempDir = Path.Combine(Path.GetTempPath(), $"XISOStudio_Test_{Guid.NewGuid()}");
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
        var tempDir = Path.Combine(Path.GetTempPath(), $"XISOStudio_Test_{Guid.NewGuid()}");
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

    [Fact]
    public async Task TryDeleteDirectoryWithRetryAsyncCanceledTokenDoesNotThrowOperationCanceled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"XISOStudio_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var lockedFile = Path.Combine(tempDir, "locked.txt");
        File.WriteAllText(lockedFile, "locked");
        var logger = new TestLogger();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using (new FileStream(lockedFile, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // Cleanup runs from finally blocks: a canceled token must stop the retry loop
            // without throwing and masking the original operation error.
            var exception = await Record.ExceptionAsync(() =>
                TempFolderCleanupHelper.TryDeleteDirectoryWithRetryAsync(tempDir, 3, 10, logger.Logger, cts.Token));

            Assert.Null(exception);
        }

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    [Fact]
    public void FindOrphanedWorkDirectoriesReturnsOnlyStaleGuidFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"CleanupScan_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var logger = new TestLogger();

        try
        {
            var stale = Path.Combine(root, "XISOStudio_Extract", Guid.NewGuid().ToString());
            var fresh = Path.Combine(root, "XISOStudio_Test", Guid.NewGuid().ToString("N"));
            var notGuid = Path.Combine(root, "XISOStudio_Extract", "not-a-guid");
            var userFolder = Path.Combine(root, "XISOStudio_backup");
            Directory.CreateDirectory(stale);
            Directory.CreateDirectory(fresh);
            Directory.CreateDirectory(notGuid);
            Directory.CreateDirectory(userFolder);

            Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-24));
            Directory.SetLastWriteTimeUtc(notGuid, DateTime.UtcNow.AddHours(-24));
            Directory.SetLastWriteTimeUtc(userFolder, DateTime.UtcNow.AddHours(-24));

            var found = TempFolderCleanupHelper.FindOrphanedWorkDirectories([root], DateTime.UtcNow, logger.Logger);

            Assert.Contains(found, path => path.Equals(stale, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(found, path => path.Equals(fresh, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(found, path => path.Equals(notGuid, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(found, path => path.Equals(userFolder, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}