using System.Globalization;
using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests fallback file name generation in <c>GenerateFilename</c>.</summary>
public class GenerateFilenameTests
{
    [Theory]
    [InlineData(0, "iso_000000.iso")]
    [InlineData(1, "iso_000001.iso")]
    [InlineData(999, "iso_000999.iso")]
    [InlineData(1000, "iso_001000.iso")]
    [InlineData(999999, "iso_999999.iso")]
    public void GenerateSimpleFilenameReturnsExpectedFilename(int index, string expected)
    {
        var result = GenerateFilename.GenerateSimpleFilename(index);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GenerateSimpleFilenameNegativeIndexThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(static () => GenerateFilename.GenerateSimpleFilename(-1));
    }

    [Fact]
    public void GenerateSimpleFilenameOverflowIndexFormatsBeyondSixDigits()
    {
        var result = GenerateFilename.GenerateSimpleFilename(1000000);
        Assert.Equal("iso_1000000.iso", result);
    }

    [Fact]
    public void GenerateSimpleFilenameEndsWithIsoExtension()
    {
        var result = GenerateFilename.GenerateSimpleFilename(42);
        Assert.EndsWith(".iso", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSimpleFilenameStartsWithIsoPrefix()
    {
        var result = GenerateFilename.GenerateSimpleFilename(42);
        Assert.StartsWith("iso_", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(2, "iso_000002.iso")]
    [InlineData(7, "iso_000007.iso")]
    [InlineData(12, "iso_000012.iso")]
    [InlineData(99, "iso_000099.iso")]
    [InlineData(99999, "iso_099999.iso")]
    [InlineData(100000, "iso_100000.iso")]
    [InlineData(2000000, "iso_2000000.iso")]
    [InlineData(2147483647, "iso_2147483647.iso")]
    public void GenerateSimpleFilenameAdditionalIndicesReturnPaddedName(int index, string expected)
    {
        var result = GenerateFilename.GenerateSimpleFilename(index);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(-100)]
    [InlineData(-1000000)]
    [InlineData(int.MinValue)]
    public void GenerateSimpleFilenameAnyNegativeIndexThrowsArgumentOutOfRange(int index)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GenerateFilename.GenerateSimpleFilename(index));
    }

    [Fact]
    public void GenerateSimpleFilenameMatchesExpectedPattern()
    {
        var result = GenerateFilename.GenerateSimpleFilename(12345);
        Assert.Matches(@"^iso_\d{6,}\.iso$", result);
    }

    [Fact]
    public void GenerateSimpleFilenamePadsSmallIndicesToSixDigits()
    {
        var result = GenerateFilename.GenerateSimpleFilename(5);
        var digits = result["iso_".Length..^".iso".Length];
        Assert.Equal(6, digits.Length);
        Assert.Equal("000005", digits);
    }

    [Fact]
    public void GenerateSimpleFilenameProducesUniqueNamePerIndex()
    {
        var names = Enumerable.Range(0, 1000).Select(GenerateFilename.GenerateSimpleFilename).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GenerateSimpleFilenameIsDeterministicForSameIndex()
    {
        Assert.Equal(GenerateFilename.GenerateSimpleFilename(321), GenerateFilename.GenerateSimpleFilename(321));
    }

    [Fact]
    public void GenerateSimpleFilenameIsCultureIndependent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        try
        {
            CultureInfo.CurrentCulture = culture;
            Assert.Equal("iso_000042.iso", GenerateFilename.GenerateSimpleFilename(42));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void GenerateSimpleFilenameZeroIndexRoundTripsThroughPath()
    {
        var result = GenerateFilename.GenerateSimpleFilename(0);
        Assert.Equal("iso_000000.iso", Path.GetFileName(result));
        Assert.Equal(".iso", Path.GetExtension(result));
    }
}