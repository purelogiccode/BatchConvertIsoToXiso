using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests construction, value equality, and <c>with</c> expressions for the <c>ImageEntry</c> record.</summary>
public class ImageEntryTests
{
    [Fact]
    public void ConstructorSetsAllProperties()
    {
        var entry = new ImageEntry("readme.txt", "/sub/readme.txt", false, 1234);

        Assert.Equal("readme.txt", entry.Name);
        Assert.Equal("/sub/readme.txt", entry.FullPath);
        Assert.False(entry.IsDirectory);
        Assert.Equal(1234, entry.Size);
    }

    [Fact]
    public void DirectoryEntryStoresZeroSize()
    {
        var entry = new ImageEntry("media", "/media", true, 0);

        Assert.Equal("media", entry.Name);
        Assert.Equal("/media", entry.FullPath);
        Assert.True(entry.IsDirectory);
        Assert.Equal(0, entry.Size);
    }

    [Fact]
    public void RootEntryUsesRootPath()
    {
        var entry = new ImageEntry(string.Empty, "/", true, 0);

        Assert.Equal(string.Empty, entry.Name);
        Assert.Equal("/", entry.FullPath);
        Assert.True(entry.IsDirectory);
    }

    [Fact]
    public void SizeAcceptsLongMaxValue()
    {
        var entry = new ImageEntry("big.bin", "/big.bin", false, long.MaxValue);

        Assert.Equal(long.MaxValue, entry.Size);
    }

    [Fact]
    public void RecordsWithSameValuesAreEqual()
    {
        var first = new ImageEntry("a.bin", "/a.bin", false, 10);
        var second = new ImageEntry("a.bin", "/a.bin", false, 10);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Theory]
    [InlineData("b.bin", "/a.bin", false, 10L)]
    [InlineData("a.bin", "/b.bin", false, 10L)]
    [InlineData("a.bin", "/a.bin", true, 10L)]
    [InlineData("a.bin", "/a.bin", false, 11L)]
    public void RecordsDifferingInAnyValueAreNotEqual(string name, string fullPath, bool isDirectory, long size)
    {
        var original = new ImageEntry("a.bin", "/a.bin", false, 10);
        var other = new ImageEntry(name, fullPath, isDirectory, size);

        Assert.NotEqual(original, other);
    }

    [Fact]
    public void WithExpressionChangesName()
    {
        var entry = new ImageEntry("a.bin", "/dir/a.bin", false, 10);

        var renamed = entry with { Name = "b.bin" };

        Assert.Equal("b.bin", renamed.Name);
        Assert.Equal("/dir/a.bin", renamed.FullPath);
        Assert.Equal(10, renamed.Size);
        Assert.Equal("a.bin", entry.Name);
    }

    [Fact]
    public void WithExpressionChangesFullPath()
    {
        var entry = new ImageEntry("a.bin", "/a.bin", false, 10);

        var moved = entry with { FullPath = "/dir/a.bin" };

        Assert.Equal("/dir/a.bin", moved.FullPath);
        Assert.Equal("a.bin", moved.Name);
        Assert.Equal("/a.bin", entry.FullPath);
    }

    [Fact]
    public void WithExpressionChangesIsDirectory()
    {
        var entry = new ImageEntry("media", "/media", false, 0);

        var directory = entry with { IsDirectory = true };

        Assert.True(directory.IsDirectory);
        Assert.False(entry.IsDirectory);
    }

    [Fact]
    public void WithExpressionChangesSize()
    {
        var entry = new ImageEntry("a.bin", "/a.bin", false, 10);

        var resized = entry with { Size = 2048 };

        Assert.Equal(2048, resized.Size);
        Assert.Equal(10, entry.Size);
    }

    [Fact]
    public void WithExpressionReturnsNewInstance()
    {
        var entry = new ImageEntry("a.bin", "/a.bin", false, 10);

        var copy = entry with { };

        Assert.NotSame(entry, copy);
        Assert.Equal(entry, copy);
    }

    [Fact]
    public void ToStringContainsAllValues()
    {
        var entry = new ImageEntry("a.bin", "/a.bin", false, 12);

        Assert.Equal("ImageEntry { Name = a.bin, FullPath = /a.bin, IsDirectory = False, Size = 12 }",
            entry.ToString());
    }
}