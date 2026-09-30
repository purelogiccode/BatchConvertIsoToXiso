using System.Globalization;
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

    [Fact]
    public void GetProgramVersionIsNotUnknown()
    {
        Assert.NotEqual("Unknown", GetApplicationVersion.GetProgramVersion(), StringComparer.Ordinal);
    }

    [Fact]
    public void GetProgramVersionMatchesExecutingAssemblyVersion()
    {
        var expected = typeof(GetApplicationVersion).Assembly.GetName().Version?.ToString() ?? "Unknown";
        Assert.Equal(expected, GetApplicationVersion.GetProgramVersion());
    }

    [Fact]
    public void GetProgramVersionIsParseableAsVersion()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        Assert.True(Version.TryParse(version, out _), $"'{version}' is not a valid version.");
    }

    [Fact]
    public void GetProgramVersionHasTwoToFourNumericComponents()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        var parts = version.Split('.');

        Assert.InRange(parts.Length, 2, 4);
        Assert.All(parts, static part =>
            Assert.True(int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out _)));
    }

    [Fact]
    public void GetProgramVersionMajorAndMinorAreNonNegative()
    {
        var parsed = Version.Parse(GetApplicationVersion.GetProgramVersion());
        Assert.True(parsed.Major >= 0);
        Assert.True(parsed.Minor >= 0);
    }

    [Fact]
    public void GetProgramVersionContainsOnlyDigitsAndDots()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        Assert.All(version, static character => Assert.True(char.IsAsciiDigit(character) || character == '.'));
    }

    [Fact]
    public void GetProgramVersionContainsNoWhitespace()
    {
        var version = GetApplicationVersion.GetProgramVersion();
        Assert.DoesNotContain(" ", version, StringComparison.Ordinal);
        Assert.DoesNotContain("\t", version, StringComparison.Ordinal);
    }

    [Fact]
    public void GetProgramVersionIsDeterministicAcrossCalls()
    {
        Assert.Equal(GetApplicationVersion.GetProgramVersion(), GetApplicationVersion.GetProgramVersion());
    }
}