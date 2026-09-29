using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

public class IsoTestResultStatusTests
{
    [Fact]
    public void IsoTestResultStatusHasExpectedValues()
    {
        Assert.Equal(0, (int)IsoTestResultStatus.Passed);
        Assert.Equal(1, (int)IsoTestResultStatus.Failed);
    }
}