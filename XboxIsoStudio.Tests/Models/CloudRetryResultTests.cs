using XboxIsoStudio.Models;
using Xunit;

namespace XboxIsoStudio.Tests.Models;

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