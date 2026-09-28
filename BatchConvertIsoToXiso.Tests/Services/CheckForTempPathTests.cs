using BatchConvertIsoToXiso.Services;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

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
        var subfolder = Path.Combine(tempPath, "BatchConvertIsoToXiso_Test");
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
}