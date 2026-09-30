using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests system temp path detection in <c>CheckForTempPath</c>.</summary>
public class CheckForTempPathTests
{
    [Fact]
    public void IsSystemTempPathWithSystemTempPathReturnsTrue()
    {
        var tempPath = Path.GetTempPath();
        Assert.True(CheckForTempPath.IsSystemTempPath(tempPath));
    }

    [Fact]
    public void IsSystemTempPathWithSubfolderOfTempPathReturnsTrue()
    {
        var tempPath = Path.GetTempPath();
        var subfolder = Path.Combine(tempPath, "XISOStudio_Test");
        Assert.True(CheckForTempPath.IsSystemTempPath(subfolder));
    }

    [Fact]
    public void IsSystemTempPathWithNonTempPathReturnsFalse()
    {
        var path = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory) ?? throw new InvalidOperationException(),
            "SomeRandomFolder");
        Assert.False(CheckForTempPath.IsSystemTempPath(path));
    }

    [Fact]
    public void IsSystemTempPathWithParentOfTempPathReturnsFalse()
    {
        // Use the temp path's parent instead of the current directory: CI runners may
        // run the tests from a folder inside the temp tree, which would make the
        // assertion meaningless (and environment-dependent).
        var tempPath = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Directory.GetParent(tempPath)?.FullName;
        if (parent == null) return; // temp path is a drive root; nothing outside to test

        Assert.False(CheckForTempPath.IsSystemTempPath(parent));
    }

    [Fact]
    public void IsSystemTempPathComparisonMatchesFileSystemCaseSensitivity()
    {
        // On case-insensitive file systems (Windows/macOS) a differently-cased temp path
        // is still the temp path; on case-sensitive file systems (Linux) it is not.
        var differentCase = Path.GetTempPath().ToUpperInvariant();

        var result = CheckForTempPath.IsSystemTempPath(differentCase);

        Assert.Equal(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(), result);
    }

    [Fact]
    public void IsSystemTempPathNullThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(static () => CheckForTempPath.IsSystemTempPath(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void IsSystemTempPathEmptyOrWhitespaceThrowsArgumentException(string path)
    {
        Assert.Throws<ArgumentException>(() => CheckForTempPath.IsSystemTempPath(path));
    }

    [Fact]
    public void IsSystemTempPathWithTrailingSeparatorsReturnsTrue()
    {
        var trimmed = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        Assert.True(CheckForTempPath.IsSystemTempPath(trimmed + Path.DirectorySeparatorChar));
        Assert.True(CheckForTempPath.IsSystemTempPath(trimmed + Path.AltDirectorySeparatorChar));
    }

    [Fact]
    public void IsSystemTempPathDeeplyNestedSubfolderReturnsTrue()
    {
        var nested = Path.Combine(Path.GetTempPath(), "iso_studio", "nested", "deeper");
        Assert.True(CheckForTempPath.IsSystemTempPath(nested));
    }

    [Fact]
    public void IsSystemTempPathPathWithDotAndParentSegmentsNormalizesReturnsTrue()
    {
        var withSegments = Path.Combine(Path.GetTempPath(), "sub", ".", "..", "other");
        Assert.True(CheckForTempPath.IsSystemTempPath(withSegments));
    }

    [Fact]
    public void IsSystemTempPathWithSharedPrefixButNotInsideReturnsFalse()
    {
        var trimmed = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0) return;

        Assert.False(CheckForTempPath.IsSystemTempPath(trimmed + "NotTemp"));
        Assert.False(CheckForTempPath.IsSystemTempPath(trimmed + "_sibling"));
    }

    [Fact]
    public void IsSystemTempPathWithAltSeparatorSubfolderReturnsTrue()
    {
        var trimmed = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0) return;

        var subfolder = trimmed + Path.AltDirectorySeparatorChar + "sub";
        Assert.True(CheckForTempPath.IsSystemTempPath(subfolder));
    }
}