using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests the file placement and naming helpers of <c>ScreenshotService</c>.</summary>
public class ScreenshotServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"ScreenshotServiceTests_{Guid.NewGuid():N}");

    public ScreenshotServiceTests()
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

    [Fact]
    public void ScreenshotFolderNameMatchesSpecification()
    {
        Assert.Equal("Screenshot", ScreenshotService.ScreenshotFolderName);
    }

    [Fact]
    public void BuildUniqueFilePathUsesTimestampedPngName()
    {
        var timestamp = new DateTime(2026, 9, 29, 17, 4, 9, 123);

        var path = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);

        Assert.Equal(Path.Combine(_tempDir, "Screenshot_20260929_170409_123.png"), path);
    }

    [Fact]
    public void BuildUniqueFilePathAddsSuffixWhenFileExists()
    {
        var timestamp = new DateTime(2026, 9, 29, 17, 4, 9, 123);
        var first = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);
        File.WriteAllText(first, "existing screenshot");

        var second = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);

        Assert.Equal(Path.Combine(_tempDir, "Screenshot_20260929_170409_123_2.png"), second);
        Assert.False(File.Exists(second));
    }

    [Fact]
    public void BuildUniqueFilePathSkipsEveryExistingSuffix()
    {
        var timestamp = new DateTime(2026, 9, 29, 17, 4, 9, 123);
        var first = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);
        File.WriteAllText(first, "1");
        var second = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);
        File.WriteAllText(second, "2");

        var third = ScreenshotService.BuildUniqueFilePath(_tempDir, timestamp);

        Assert.Equal(Path.Combine(_tempDir, "Screenshot_20260929_170409_123_3.png"), third);
    }

    [Fact]
    public void FallbackDirectoryIsUnderLocalApplicationData()
    {
        var fallback = ScreenshotService.GetFallbackDirectory();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(Path.GetFullPath(localAppData), Path.GetFullPath(fallback),
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(ScreenshotService.ScreenshotFolderName, fallback, StringComparison.Ordinal);
    }
}