using BatchConvertIsoToXiso.Services;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class SupportedFilesTests
{
    [Theory]
    [InlineData("game.iso")]
    [InlineData("game.ISO")]
    [InlineData("games.zip")]
    [InlineData("games.7z")]
    [InlineData("games.rar")]
    [InlineData(@"C:\folder\game.iso")]
    public void IsConvertibleSupportedExtensionsReturnsTrue(string path)
    {
        Assert.True(SupportedFiles.IsConvertible(path));
    }

    [Theory]
    [InlineData("game.bin")]
    [InlineData("game.txt")]
    [InlineData("game.iso.bak")]
    [InlineData("game")]
    public void IsConvertibleUnsupportedExtensionsReturnsFalse(string path)
    {
        Assert.False(SupportedFiles.IsConvertible(path));
    }

    [Theory]
    [InlineData("game.iso")]
    [InlineData("GAME.ISO")]
    [InlineData(@"C:\folder\game.iso")]
    public void IsIsoIsoFilesReturnsTrue(string path)
    {
        Assert.True(SupportedFiles.IsIso(path));
    }

    [Theory]
    [InlineData("game.zip")]
    [InlineData("game.xiso")]
    public void IsIsoNonIsoFilesReturnsFalse(string path)
    {
        Assert.False(SupportedFiles.IsIso(path));
    }
}
