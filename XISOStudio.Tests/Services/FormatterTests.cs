using System.Globalization;
using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests byte and transfer rate formatting in <c>Formatter</c>.</summary>
public class FormatterTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    public void FormatBytesSmallValuesReturnsBytes(long bytes, string expected)
    {
        var result = Formatter.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1024, 1d, "KB")]
    [InlineData(1536, 1.5d, "KB")]
    [InlineData(1048576, 1d, "MB")]
    [InlineData(1073741824, 1d, "GB")]
    [InlineData(1099511627776, 1d, "TB")]
    [InlineData(2199023255552, 2d, "TB")]
    public void FormatBytesLargeValuesReturnsFormattedWithUnit(long bytes, double expectedValue, string unit)
    {
        var result = Formatter.FormatBytes(bytes);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0d, 0d, "B/s")]
    [InlineData(512d, 512d, "B/s")]
    [InlineData(1023d, 1023d, "B/s")]
    [InlineData(1024d, 1d, "KB/s")]
    [InlineData(1536d, 1.5d, "KB/s")]
    [InlineData(1048576d, 1d, "MB/s")]
    [InlineData(10485760d, 10d, "MB/s")]
    public void FormatBytesPerSecondReturnsCorrectString(double bytesPerSecond, double expectedValue, string unit)
    {
        var result = Formatter.FormatBytesPerSecond(bytesPerSecond);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1024, 1f, "KB")]
    [InlineData(1048576, 1f, "MB")]
    [InlineData(1073741824, 1f, "GB")]
    [InlineData(1099511627776, 1f, "TB")]
    public void FormatBytesExactBoundaryValues(long bytes, float expectedValue, string unit)
    {
        var result = Formatter.FormatBytes(bytes);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesNegativeValueReturnsFormattedBytes()
    {
        var result = Formatter.FormatBytes(-1);
        Assert.Equal("-1 B", result);
    }

    [Theory]
    [InlineData(1024d, 1d, "KB/s")]
    [InlineData(1048576d, 1d, "MB/s")]
    public void FormatBytesPerSecondExactBoundaryValues(double bytesPerSecond, double expectedValue, string unit)
    {
        var result = Formatter.FormatBytesPerSecond(bytesPerSecond);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesPerSecondNegativeValueReturnsFormattedBytes()
    {
        var result = Formatter.FormatBytesPerSecond(-1d);
        var expected = $"{(-1d).ToString("F1", CultureInfo.CurrentCulture)} B/s";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1048575, 1024d, "KB")]
    [InlineData(1048576, 1d, "MB")]
    [InlineData(1073741823, 1024d, "MB")]
    [InlineData(1073741824, 1d, "GB")]
    [InlineData(1099511627775, 1024d, "GB")]
    [InlineData(1099511627776, 1d, "TB")]
    [InlineData(1125899906842624, 1024d, "TB")]
    public void FormatBytesUnitBoundariesReturnExpectedUnit(long bytes, double expectedValue, string unit)
    {
        var result = Formatter.FormatBytes(bytes);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1536, 1.5d)]
    [InlineData(2047, 2d)]
    [InlineData(2048, 2d)]
    [InlineData(3071, 3d)]
    [InlineData(3072, 3d)]
    public void FormatBytesPreservesFractionalUnits(long bytes, double expectedValue)
    {
        var result = Formatter.FormatBytes(bytes);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} KB";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesLongMaxValueReturnsTerabytes()
    {
        var result = Formatter.FormatBytes(long.MaxValue);
        var expected = $"{(long.MaxValue / 1099511627776d).ToString("F1", CultureInfo.CurrentCulture)} TB";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesLongMinValueReturnsBytes()
    {
        var result = Formatter.FormatBytes(long.MinValue);
        Assert.Equal($"{long.MinValue} B", result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1024)]
    [InlineData(-1048576)]
    [InlineData(-1099511627776)]
    [InlineData(long.MinValue)]
    public void FormatBytesNegativeValuesAlwaysReturnBytes(long bytes)
    {
        var result = Formatter.FormatBytes(bytes);
        Assert.Equal($"{bytes} B", result);
    }

    [Theory]
    [InlineData(-1024d, -1024d)]
    [InlineData(-1048576d, -1048576d)]
    [InlineData(-0.5d, -0.5d)]
    public void FormatBytesPerSecondNegativeRatesStayInBytesPerSecond(double bytesPerSecond, double expectedValue)
    {
        var result = Formatter.FormatBytesPerSecond(bytesPerSecond);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} B/s";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1536.5d, 1.5d, "KB/s")]
    [InlineData(1572864d, 1.5d, "MB/s")]
    [InlineData(2560.25d, 2.5d, "KB/s")]
    [InlineData(0.5d, 0.5d, "B/s")]
    public void FormatBytesPerSecondFractionalRatesReturnFractionalValue(double bytesPerSecond, double expectedValue,
        string unit)
    {
        var result = Formatter.FormatBytesPerSecond(bytesPerSecond);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(1.05d, 1.1d, "B/s")]
    [InlineData(1.04d, 1.0d, "B/s")]
    [InlineData(0.05d, 0.1d, "B/s")]
    [InlineData(1023.99d, 1024.0d, "B/s")]
    [InlineData(1075.2d, 1.1d, "KB/s")]
    [InlineData(1048575.9d, 1024.0d, "KB/s")]
    public void FormatBytesPerSecondRoundingCases(double bytesPerSecond, double expectedValue, string unit)
    {
        var result = Formatter.FormatBytesPerSecond(bytesPerSecond);
        var expected = $"{expectedValue.ToString("F1", CultureInfo.CurrentCulture)} {unit}";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesPerSecondAboveMegabyteStaysInMegabytes()
    {
        var result = Formatter.FormatBytesPerSecond(1073741824d);
        var expected = $"{(1073741824d / 1048576d).ToString("F1", CultureInfo.CurrentCulture)} MB/s";
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatBytesUsesCurrentCultureDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        try
        {
            CultureInfo.CurrentCulture = culture;
            Assert.Equal("1,0 KB", Formatter.FormatBytes(1024));
            Assert.Equal("3,0 MB", Formatter.FormatBytes(3145728));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatBytesPerSecondUsesCurrentCultureDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        try
        {
            CultureInfo.CurrentCulture = culture;
            Assert.Equal("1,5 KB/s", Formatter.FormatBytesPerSecond(1536d));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FormatterWithInvariantCultureUsesDotSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("1.0 KB", Formatter.FormatBytes(1024));
            Assert.Equal("1.5 KB/s", Formatter.FormatBytesPerSecond(1536d));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}