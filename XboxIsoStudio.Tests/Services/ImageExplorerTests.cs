using XboxIsoStudio.Services;
using XISOSharp;
using Xunit;

namespace XboxIsoStudio.Tests.Services;

public sealed class ImageExplorerTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ImageExplorerTests_{Guid.NewGuid():N}");

    public ImageExplorerTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignored
        }
    }

    private string CreateXiso(string name = "game.iso")
    {
        var sourceDir = Path.Combine(_tempRoot, "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "default.xbe"), "fake xbe content");
        var mediaDir = Path.Combine(sourceDir, "media");
        Directory.CreateDirectory(mediaDir);
        File.WriteAllBytes(Path.Combine(mediaDir, "data.bin"), [1, 2, 3, 4, 5]);

        var isoPath = Path.Combine(_tempRoot, name);
        Assert.Equal(0, XisoWriter.PackFromDirectory(sourceDir, isoPath));
        return isoPath;
    }

    private string CreateCiso()
    {
        var isoPath = CreateXiso();
        var csoPath = Path.Combine(_tempRoot, "game.cso");
        Assert.Equal(0, CisoWriter.CompressToCso(isoPath, csoPath));
        return csoPath;
    }

    private string CreateZar()
    {
        var isoPath = CreateXiso();
        var zarPath = Path.Combine(_tempRoot, "game.zar");
        Assert.True(XisoZarchive.CreateZar(isoPath, zarPath, quiet: true));
        return zarPath;
    }

    private string CreateChd()
    {
        var isoPath = CreateXiso();
        var chdPath = Path.Combine(_tempRoot, "game.chd");
        return ChdTestHelper.CreateDvdChd(isoPath, chdPath);
    }

    private void AssertImageExplorerWorks(string imagePath)
    {
        using var explorer = ImageExplorerFactory.Open(imagePath);

        var root = explorer.ListChildren("/");
        Assert.Contains(root, static e => e is { Name: "default.xbe", IsDirectory: false, Size: > 0 });
        Assert.Contains(root, static e => e is { Name: "media", IsDirectory: true, Size: 0 });

        var media = explorer.ListChildren("/media");
        var data = Assert.Single(media);
        Assert.Equal("data.bin", data.Name);
        Assert.False(data.IsDirectory);
        Assert.Equal(5, data.Size);
        Assert.Equal("/media/data.bin", data.FullPath);

        var dest = Path.Combine(_tempRoot, "out", "data.bin");
        explorer.CopyOut("/media/data.bin", dest);
        Assert.Equal([1, 2, 3, 4, 5], File.ReadAllBytes(dest));
    }

    [Fact]
    public void XisoImageExplorerListsAndCopiesFiles()
    {
        AssertImageExplorerWorks(CreateXiso());
    }

    [Fact]
    public void CisoImageExplorerListsAndCopiesFiles()
    {
        AssertImageExplorerWorks(CreateCiso());
    }

    [Fact]
    public void ZarImageExplorerListsAndCopiesFiles()
    {
        AssertImageExplorerWorks(CreateZar());
    }

    [Fact]
    public void ChdImageExplorerListsAndCopiesFiles()
    {
        AssertImageExplorerWorks(CreateChd());
    }

    [Fact]
    public void ChdImageExplorerCopiesDirectoriesRecursively()
    {
        var chdPath = CreateChd();
        using var explorer = ImageExplorerFactory.Open(chdPath);

        var dest = Path.Combine(_tempRoot, "media-out");
        explorer.CopyOut("/media", dest);

        Assert.True(File.Exists(Path.Combine(dest, "data.bin")));
        Assert.Equal([1, 2, 3, 4, 5], File.ReadAllBytes(Path.Combine(dest, "data.bin")));
    }

    [Fact]
    public void ChdImageExplorerMissingPathThrows()
    {
        var chdPath = CreateChd();
        using var explorer = ImageExplorerFactory.Open(chdPath);

        Assert.Throws<InvalidDataException>(() => explorer.ListChildren("/missing"));
        Assert.Throws<InvalidDataException>(() => explorer.CopyOut("/missing", Path.Combine(_tempRoot, "x")));
    }

    [Fact]
    public void ChdImageExplorerInvalidChdThrows()
    {
        var badChd = Path.Combine(_tempRoot, "bad.chd");
        File.WriteAllText(badChd, "not a chd");

        Assert.Throws<InvalidDataException>(() => ImageExplorerFactory.Open(badChd));
    }

    [Fact]
    public void ZarImageExplorerCopiesDirectoriesRecursively()
    {
        var zarPath = CreateZar();
        using var explorer = ImageExplorerFactory.Open(zarPath);

        var dest = Path.Combine(_tempRoot, "media-out");
        explorer.CopyOut("/media", dest);

        Assert.True(File.Exists(Path.Combine(dest, "data.bin")));
        Assert.Equal([1, 2, 3, 4, 5], File.ReadAllBytes(Path.Combine(dest, "data.bin")));
    }

    [Fact]
    public void ZarImageExplorerMissingPathThrows()
    {
        var zarPath = CreateZar();
        using var explorer = ImageExplorerFactory.Open(zarPath);

        Assert.Throws<InvalidDataException>(() => explorer.ListChildren("/missing"));
        Assert.Throws<InvalidDataException>(() => explorer.CopyOut("/missing", Path.Combine(_tempRoot, "x")));
    }

    [Fact]
    public void ZarImageExplorerInvalidArchiveThrows()
    {
        var badZar = Path.Combine(_tempRoot, "bad.zar");
        File.WriteAllText(badZar, "not a zar");

        Assert.Throws<InvalidDataException>(() => ImageExplorerFactory.Open(badZar));
    }

    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("media\\sub/", "/media/sub")]
    [InlineData("media", "/media")]
    public void ImagePathsNormalizeReturnsCanonicalPath(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.Normalize(input));
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/media", "/")]
    [InlineData("/media/sub", "/media")]
    public void ImagePathsGetParentReturnsParent(string input, string expected)
    {
        Assert.Equal(expected, ImagePaths.GetParent(input));
    }
}