using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests application version retrieval in <c>GetApplicationVersion</c>.</summary>
public class GetApplicationVersionTests
{
    [Fact]
    public void GetProgramVersionReturnsNonNullString()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        Assert.False(string.IsNullOrEmpty(version));
    }

    [Fact]
    public void GetProgramVersionReturnsVersionFormat()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        // Should be in format x.x.x.x or similar
        Assert.Contains('.', version);
    }
}