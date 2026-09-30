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

    [Fact]
    public void InvalidInputHasValueFour()
    {
        Assert.Equal(4, (int)FileProcessingStatus.InvalidInput);
    }

    [Fact]
    public void EnumDefinesExactlyFiveValuesInDeclarationOrder()
    {
        var values = Enum.GetValues<FileProcessingStatus>();

        Assert.Equal(5, values.Length);
        Assert.Equal(
            [
                FileProcessingStatus.Converted,
                FileProcessingStatus.Skipped,
                FileProcessingStatus.Failed,
                FileProcessingStatus.AlreadyOptimized,
                FileProcessingStatus.InvalidInput
            ],
            values);
    }

    [Fact]
    public void EnumNamesMatchExpectedOrder()
    {
        Assert.Equal(["Converted", "Skipped", "Failed", "AlreadyOptimized", "InvalidInput"],
            Enum.GetNames<FileProcessingStatus>());
    }

    [Theory]
    [InlineData("Converted", FileProcessingStatus.Converted)]
    [InlineData("Skipped", FileProcessingStatus.Skipped)]
    [InlineData("Failed", FileProcessingStatus.Failed)]
    [InlineData("AlreadyOptimized", FileProcessingStatus.AlreadyOptimized)]
    [InlineData("InvalidInput", FileProcessingStatus.InvalidInput)]
    public void ParseResolvesEachName(string name, FileProcessingStatus expected)
    {
        Assert.Equal(expected, Enum.Parse<FileProcessingStatus>(name));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public void UndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((FileProcessingStatus)value));
    }

    [Fact]
    public void UnderlyingTypeIsInt()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(FileProcessingStatus)));
    }
}