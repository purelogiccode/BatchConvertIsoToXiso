using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests path normalization, combination, and parent resolution in <c>ImagePaths</c>.</summary>
public class ImagePathsTests
{
    [Fact]
    public void NormalizeNullReturnsRoot()
    {
        Assert.Equal("/", ImagePaths.Normalize(null));
    }

    [Fact]
    public void NormalizeEmptyReturnsRoot()
    {
        Assert.Equal("/", ImagePaths.Normalize(string.Empty));
    }

    [Fact]
    public void NormalizeRootReturnsRoot()
    {
        Assert.Equal("/", ImagePaths.Normalize("/"));
    }

    [Theory]
    [InlineData("media", "/media")]
    [InlineData("sub", "/sub")]
    [InlineData("a/b/c", "/a/b/c")]
    public void NormalizeAddsLeadingSlash(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("media\\sub", "/media/sub")]
    [InlineData("\\a\\b", "/a/b")]
    [InlineData("a\\b\\c", "/a/b/c")]
    public void NormalizeConvertsBackslashesToForwardSlashes(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("media/", "/media")]
    [InlineData("/media/", "/media")]
    [InlineData("a/b/", "/a/b")]
    [InlineData("/Media/File.TXT/", "/Media/File.TXT")]
    public void NormalizeStripsTrailingSlash(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("/media//", "/media")]
    [InlineData("a/b//", "/a/b")]
    [InlineData("/media///", "/media")]
    public void NormalizeStripsRepeatedTrailingSlashes(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("//")]
    [InlineData("///")]
    [InlineData("\\")]
    [InlineData("\\\\")]
    public void NormalizeSlashOnlyInputReturnsRoot(string input)
    {
        Assert.Equal("/", ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("a//b", "/a//b")]
    [InlineData("a//b//c", "/a//b//c")]
    [InlineData("media\\\\sub/", "/media//sub")]
    public void NormalizeKeepsInteriorRepeatedSlashes(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("Media/File.TXT", "/Media/File.TXT")]
    [InlineData("GAME.ISO", "/GAME.ISO")]
    public void NormalizePreservesCharacterCase(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/media")]
    [InlineData("/media/sub")]
    [InlineData("/Media/File.TXT")]
    public void NormalizeAlreadyNormalizedPathIsUnchanged(string path)
    {
        Assert.Equal(path, ImagePaths.Normalize(path));
    }

    [Fact]
    public void CombineRootAndNameReturnsRootedPath()
    {
        Assert.Equal("/file.txt", ImagePaths.Combine("/", "file.txt"));
    }

    [Fact]
    public void CombineDirectoryAndNameReturnsJoinedPath()
    {
        Assert.Equal("/media/data.bin", ImagePaths.Combine("/media", "data.bin"));
    }

    [Fact]
    public void CombineNestedDirectoryAndNameReturnsJoinedPath()
    {
        Assert.Equal("/media/sub/a.txt", ImagePaths.Combine("/media/sub", "a.txt"));
    }

    [Theory]
    [InlineData("/media/")]
    [InlineData("media/")]
    public void CombineIgnoresDirectoryTrailingSlash(string directory)
    {
        Assert.Equal("/media/data.bin", ImagePaths.Combine(directory, "data.bin"));
    }

    [Fact]
    public void CombineAddsLeadingSlashToDirectory()
    {
        Assert.Equal("/media/data.bin", ImagePaths.Combine("media", "data.bin"));
    }

    [Fact]
    public void CombineEmptyDirectoryReturnsRootedName()
    {
        Assert.Equal("/file.txt", ImagePaths.Combine(string.Empty, "file.txt"));
    }

    [Fact]
    public void CombineUnnormalizedNestedDirectoryReturnsJoinedPath()
    {
        Assert.Equal("/media/sub/file.bin", ImagePaths.Combine("media/sub", "file.bin"));
    }

    [Fact]
    public void GetParentRootReturnsRoot()
    {
        Assert.Equal("/", ImagePaths.GetParent("/"));
    }

    [Theory]
    [InlineData("/media")]
    [InlineData("media")]
    [InlineData("/top")]
    [InlineData("top")]
    public void GetParentTopLevelReturnsRoot(string path)
    {
        Assert.Equal("/", ImagePaths.GetParent(path));
    }

    [Fact]
    public void GetParentNestedReturnsDirectory()
    {
        Assert.Equal("/media", ImagePaths.GetParent("/media/sub"));
    }

    [Fact]
    public void GetParentDeeplyNestedReturnsDirectory()
    {
        Assert.Equal("/media/sub", ImagePaths.GetParent("/media/sub/deep"));
    }

    [Fact]
    public void GetParentEmptyReturnsRoot()
    {
        Assert.Equal("/", ImagePaths.GetParent(string.Empty));
    }

    [Theory]
    [InlineData("/media/sub/")]
    [InlineData("media/sub/")]
    public void GetParentIgnoresTrailingSlash(string path)
    {
        Assert.Equal("/media", ImagePaths.GetParent(path));
    }

    [Theory]
    [InlineData("/media\\sub")]
    [InlineData("\\media\\sub\\")]
    public void GetParentNormalizesBackslashes(string path)
    {
        Assert.Equal("/media", ImagePaths.GetParent(path));
    }
}