using XboxIsoStudio.Models;
using Xunit;

namespace XboxIsoStudio.Tests.Models;

public class FileItemTests
{
    [Fact]
    public void IsSelectedDefaultsToTrue()
    {
        var item = new FileItem();

        Assert.True(item.IsSelected);
    }

    [Fact]
    public void IsSelectedRaisesPropertyChanged()
    {
        var item = new FileItem();
        var raised = false;
        item.PropertyChanged += (_, e) =>
            raised = string.Equals(e.PropertyName, nameof(FileItem.IsSelected), StringComparison.Ordinal);

        item.IsSelected = false;

        Assert.True(raised);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void IsSelectedSameValueDoesNotRaisePropertyChanged()
    {
        var item = new FileItem();
        var raised = false;
        item.PropertyChanged += (_, _) => raised = true;

        item.IsSelected = true;

        Assert.False(raised);
    }

    [Fact]
    public void FileSizeSetterUpdatesDisplaySize()
    {
        var item = new FileItem();

        item.FileSize = 1024;

        Assert.Equal("1 KB", item.DisplaySize);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void FileSizeFormatsDisplaySize(long bytes, string expected)
    {
        var item = new FileItem { FileSize = bytes };

        Assert.Equal(expected, item.DisplaySize);
    }

    [Fact]
    public void FileNameSameValueDoesNotRaisePropertyChanged()
    {
        var item = new FileItem { FileName = "game.iso" };
        var raised = false;
        item.PropertyChanged += (_, _) => raised = true;

        item.FileName = "game.iso";

        Assert.False(raised);
    }

    [Fact]
    public void FullPathRaisesPropertyChanged()
    {
        var item = new FileItem();
        var raised = false;
        item.PropertyChanged += (_, e) =>
            raised = string.Equals(e.PropertyName, nameof(FileItem.FullPath), StringComparison.Ordinal);

        item.FullPath = @"C:\games\game.iso";

        Assert.True(raised);
    }
}