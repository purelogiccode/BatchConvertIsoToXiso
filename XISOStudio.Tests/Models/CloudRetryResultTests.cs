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
}