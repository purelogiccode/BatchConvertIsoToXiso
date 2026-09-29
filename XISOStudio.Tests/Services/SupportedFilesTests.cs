using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

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

    [Theory]
    [InlineData("game.iso")]
    [InlineData("GAME.ISO")]
    [InlineData(@"C:\folder\game.iso")]
    [InlineData("game.cso")]
    [InlineData("GAME.CSO")]
    [InlineData("game.1.cso")]
    [InlineData(@"C:\folder\game.1.cso")]
    [InlineData("my.game.1.cso")]
    [InlineData("game.zar")]
    [InlineData("GAME.ZAR")]
    [InlineData(@"C:\folder\game.zar")]
    [InlineData("game.chd")]
    [InlineData("GAME.CHD")]
    [InlineData(@"C:\folder\game.chd")]
    public void IsTestableSupportedImagesReturnsTrue(string path)
    {
        Assert.True(SupportedFiles.IsTestable(path));
    }

    [Theory]
    [InlineData("game.2.cso")]
    [InlineData("game.10.cso")]
    [InlineData(@"C:\folder\game.2.cso")]
    [InlineData("game.zip")]
    [InlineData("game.xiso")]
    [InlineData("game.bin")]
    [InlineData("game")]
    public void IsTestableUnsupportedFilesReturnsFalse(string path)
    {
        Assert.False(SupportedFiles.IsTestable(path));
    }
}