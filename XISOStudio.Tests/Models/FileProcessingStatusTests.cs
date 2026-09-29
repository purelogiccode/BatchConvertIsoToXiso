using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the numeric values of the <c>FileProcessingStatus</c> enum members.</summary>
public class FileProcessingStatusTests
{
    [Fact]
    public void FileProcessingStatusHasExpectedValues()
    {
        Assert.Equal(0, (int)FileProcessingStatus.Converted);
        Assert.Equal(1, (int)FileProcessingStatus.Skipped);
        Assert.Equal(2, (int)FileProcessingStatus.Failed);
        Assert.Equal(3, (int)FileProcessingStatus.AlreadyOptimized);
    }
}