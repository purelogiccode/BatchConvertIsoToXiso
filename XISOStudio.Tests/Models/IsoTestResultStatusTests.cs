using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the numeric values of the <c>IsoTestResultStatus</c> enum members.</summary>
public class IsoTestResultStatusTests
{
    [Fact]
    public void IsoTestResultStatusHasExpectedValues()
    {
        Assert.Equal(0, (int)IsoTestResultStatus.Passed);
        Assert.Equal(1, (int)IsoTestResultStatus.Failed);
    }

    [Fact]
    public void EnumDefinesExactlyTwoValuesInDeclarationOrder()
    {
        var values = Enum.GetValues<IsoTestResultStatus>();

        Assert.Equal(2, values.Length);
        Assert.Equal([IsoTestResultStatus.Passed, IsoTestResultStatus.Failed], values);
    }

    [Fact]
    public void EnumNamesMatchExpectedOrder()
    {
        Assert.Equal(["Passed", "Failed"], Enum.GetNames<IsoTestResultStatus>());
    }

    [Theory]
    [InlineData("Passed", IsoTestResultStatus.Passed)]
    [InlineData("Failed", IsoTestResultStatus.Failed)]
    public void ParseResolvesEachName(string name, IsoTestResultStatus expected)
    {
        Assert.Equal(expected, Enum.Parse<IsoTestResultStatus>(name));
    }

    [Fact]
    public void DefaultValueIsPassed()
    {
        Assert.Equal(IsoTestResultStatus.Passed, default);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void UndefinedValuesAreNotDefined(int value)
    {
        Assert.False(Enum.IsDefined((IsoTestResultStatus)value));
    }

    [Fact]
    public void UnderlyingTypeIsInt()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(IsoTestResultStatus)));
    }
}