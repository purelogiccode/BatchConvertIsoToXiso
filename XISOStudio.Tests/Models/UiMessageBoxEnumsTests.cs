using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the defined members and numeric values of the UI message box enums.</summary>
public class UiMessageBoxEnumsTests
{
    [Fact]
    public void UiMessageBoxButtonHasExpectedValues()
    {
        Assert.Equal(0, (int)UiMessageBoxButton.Ok);
        Assert.Equal(1, (int)UiMessageBoxButton.YesNo);
        Assert.Equal(2, (int)UiMessageBoxButton.YesNoCancel);
    }

    [Fact]
    public void UiMessageBoxButtonNamesMapToExpectedValues()
    {
        Assert.Equal(0, (int)Enum.Parse<UiMessageBoxButton>(nameof(UiMessageBoxButton.Ok)));
        Assert.Equal(1, (int)Enum.Parse<UiMessageBoxButton>(nameof(UiMessageBoxButton.YesNo)));
        Assert.Equal(2, (int)Enum.Parse<UiMessageBoxButton>(nameof(UiMessageBoxButton.YesNoCancel)));
    }

    [Fact]
    public void UiMessageBoxButtonDefinesExactlyThreeMembers()
    {
        var values = Enum.GetValues<UiMessageBoxButton>();

        Assert.Equal(3, values.Length);
        Assert.Equal([UiMessageBoxButton.Ok, UiMessageBoxButton.YesNo, UiMessageBoxButton.YesNoCancel], values);
        Assert.Equal(["Ok", "YesNo", "YesNoCancel"], Enum.GetNames<UiMessageBoxButton>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void UiMessageBoxButtonUndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((UiMessageBoxButton)value));
    }

    [Fact]
    public void UiMessageBoxImageHasExpectedValues()
    {
        Assert.Equal(0, (int)UiMessageBoxImage.Information);
        Assert.Equal(1, (int)UiMessageBoxImage.Warning);
        Assert.Equal(2, (int)UiMessageBoxImage.Error);
    }

    [Fact]
    public void UiMessageBoxImageNamesMapToExpectedValues()
    {
        Assert.Equal(0, (int)Enum.Parse<UiMessageBoxImage>(nameof(UiMessageBoxImage.Information)));
        Assert.Equal(1, (int)Enum.Parse<UiMessageBoxImage>(nameof(UiMessageBoxImage.Warning)));
        Assert.Equal(2, (int)Enum.Parse<UiMessageBoxImage>(nameof(UiMessageBoxImage.Error)));
    }

    [Fact]
    public void UiMessageBoxImageDefinesExactlyThreeMembers()
    {
        var values = Enum.GetValues<UiMessageBoxImage>();

        Assert.Equal(3, values.Length);
        Assert.Equal([UiMessageBoxImage.Information, UiMessageBoxImage.Warning, UiMessageBoxImage.Error], values);
        Assert.Equal(["Information", "Warning", "Error"], Enum.GetNames<UiMessageBoxImage>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(99)]
    public void UiMessageBoxImageUndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((UiMessageBoxImage)value));
    }

    [Fact]
    public void UiMessageBoxResultHasExpectedValues()
    {
        Assert.Equal(0, (int)UiMessageBoxResult.Ok);
        Assert.Equal(1, (int)UiMessageBoxResult.Yes);
        Assert.Equal(2, (int)UiMessageBoxResult.No);
        Assert.Equal(3, (int)UiMessageBoxResult.Cancel);
    }

    [Fact]
    public void UiMessageBoxResultNamesMapToExpectedValues()
    {
        Assert.Equal(0, (int)Enum.Parse<UiMessageBoxResult>(nameof(UiMessageBoxResult.Ok)));
        Assert.Equal(1, (int)Enum.Parse<UiMessageBoxResult>(nameof(UiMessageBoxResult.Yes)));
        Assert.Equal(2, (int)Enum.Parse<UiMessageBoxResult>(nameof(UiMessageBoxResult.No)));
        Assert.Equal(3, (int)Enum.Parse<UiMessageBoxResult>(nameof(UiMessageBoxResult.Cancel)));
    }

    [Fact]
    public void UiMessageBoxResultDefinesExactlyFourMembers()
    {
        var values = Enum.GetValues<UiMessageBoxResult>();

        Assert.Equal(4, values.Length);
        Assert.Equal([UiMessageBoxResult.Ok, UiMessageBoxResult.Yes, UiMessageBoxResult.No, UiMessageBoxResult.Cancel],
            values);
        Assert.Equal(["Ok", "Yes", "No", "Cancel"], Enum.GetNames<UiMessageBoxResult>());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    public void UiMessageBoxResultUndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((UiMessageBoxResult)value));
    }

    [Fact]
    public void AllEnumsUseIntUnderlyingType()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(UiMessageBoxButton)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(UiMessageBoxImage)));
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(UiMessageBoxResult)));
    }
}