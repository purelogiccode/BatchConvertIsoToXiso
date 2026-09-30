using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the numeric values of the <c>CloudRetryResult</c> enum members.</summary>
public class CloudRetryResultTests
{
    [Fact]
    public void CloudRetryResultHasExpectedValues()
    {
        Assert.Equal(0, (int)CloudRetryResult.Retry);
        Assert.Equal(1, (int)CloudRetryResult.Skip);
        Assert.Equal(2, (int)CloudRetryResult.Cancel);
    }

    [Fact]
    public void EnumDefinesExactlyThreeValuesInDeclarationOrder()
    {
        var values = Enum.GetValues<CloudRetryResult>();

        Assert.Equal(3, values.Length);
        Assert.Equal([CloudRetryResult.Retry, CloudRetryResult.Skip, CloudRetryResult.Cancel], values);
    }

    [Fact]
    public void EnumNamesMatchExpectedOrder()
    {
        Assert.Equal(["Retry", "Skip", "Cancel"], Enum.GetNames<CloudRetryResult>());
    }

    [Theory]
    [InlineData("Retry", CloudRetryResult.Retry)]
    [InlineData("Skip", CloudRetryResult.Skip)]
    [InlineData("Cancel", CloudRetryResult.Cancel)]
    public void ParseResolvesEachName(string name, CloudRetryResult expected)
    {
        Assert.Equal(expected, Enum.Parse<CloudRetryResult>(name));
    }

    [Fact]
    public void DefaultValueIsRetry()
    {
        Assert.Equal(CloudRetryResult.Retry, default);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(42)]
    public void UndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((CloudRetryResult)value));
    }

    [Fact]
    public void UnderlyingTypeIsInt()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(CloudRetryResult)));
    }
}