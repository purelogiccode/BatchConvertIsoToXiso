using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests <c>XisoExplorerItem</c> defaults, folder/file type resolution, and property initialization.</summary>
public class XisoExplorerItemTests
{
    [Fact]
    public void DefaultValuesAreSetCorrectly()
    {
        var item = new XisoExplorerItem();

        Assert.Equal(string.Empty, item.Name);
        Assert.Equal(string.Empty, item.SizeFormatted);
        Assert.False(item.IsDirectory);
        Assert.Equal("File", item.Type);
    }

    [Fact]
    public void TypeReturnsFolderWhenIsDirectoryIsTrue()
    {
        var item = new XisoExplorerItem { IsDirectory = true };
        Assert.Equal("Folder", item.Type);
    }

    [Fact]
    public void TypeReturnsFileWhenIsDirectoryIsFalse()
    {
        var item = new XisoExplorerItem { IsDirectory = false };
        Assert.Equal("File", item.Type);
    }

    [Fact]
    public void PropertiesCanBeInitialized()
    {
        var entry = new ImageEntry("default.xbe", "/default.xbe", false, 1024);
        var item = new XisoExplorerItem
        {
            Name = "default.xbe",
            SizeFormatted = "1.5 MB",
            IsDirectory = false,
            Entry = entry
        };

        Assert.Equal("default.xbe", item.Name);
        Assert.Equal("1.5 MB", item.SizeFormatted);
        Assert.False(item.IsDirectory);
        Assert.Same(entry, item.Entry);
    }

    [Fact]
    public void EntryDefaultsToNull()
    {
        var item = new XisoExplorerItem();

        Assert.Null(item.Entry);
    }

    [Fact]
    public void NameCanBeInitializedFromImageEntry()
    {
        var entry = new ImageEntry("media", "/media", true, 0);

        var item = new XisoExplorerItem
        {
            Name = entry.Name,
            IsDirectory = entry.IsDirectory,
            Entry = entry
        };

        Assert.Equal(entry.Name, item.Name);
        Assert.True(item.IsDirectory);
        Assert.Equal("Folder", item.Type);
        Assert.Same(entry, item.Entry);
    }

    [Fact]
    public void TypeDependsOnIsDirectoryNotEntry()
    {
        var directoryEntry = new ImageEntry("file.bin", "/file.bin", true, 0);

        var item = new XisoExplorerItem
        {
            Name = "file.bin",
            IsDirectory = false,
            Entry = directoryEntry
        };

        Assert.True(item.Entry.IsDirectory);
        Assert.Equal("File", item.Type);
    }

    [Fact]
    public void SizeFormattedCanBeChanged()
    {
        var item = new XisoExplorerItem { SizeFormatted = "2.5 GB" };

        Assert.Equal("2.5 GB", item.SizeFormatted);

        item.SizeFormatted = "1 KB";

        Assert.Equal("1 KB", item.SizeFormatted);
    }

    [Fact]
    public void DistinctInstancesWithSameValuesAreNotEqual()
    {
        var entry = new ImageEntry("a.bin", "/a.bin", false, 1);
        var first = new XisoExplorerItem { Name = "a.bin", SizeFormatted = "1 B", Entry = entry };
        var second = new XisoExplorerItem { Name = "a.bin", SizeFormatted = "1 B", Entry = entry };

        Assert.NotEqual(first, second);
        Assert.Same(first, first);
    }

    [Fact]
    public void EqualImageEntriesDoNotMakeItemsEqual()
    {
        var first = new XisoExplorerItem
        {
            Name = "a.bin",
            Entry = new ImageEntry("a.bin", "/a.bin", false, 1)
        };
        var second = new XisoExplorerItem
        {
            Name = "a.bin",
            Entry = new ImageEntry("a.bin", "/a.bin", false, 1)
        };

        Assert.Equal(first.Entry, second.Entry);
        Assert.NotEqual(first, second);
    }
}