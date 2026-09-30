using System.Globalization;
using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests the folder/file emoji conversion performed by <c>FolderIconConverter</c>.</summary>
public class FolderIconConverterTests
{
    private readonly FolderIconConverter _converter = new();

    [Fact]
    public void ConvertWithTrueReturnsFolderEmoji()
    {
        var result = _converter.Convert(true, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("📁", result);
    }

    [Fact]
    public void ConvertWithFalseReturnsFileEmoji()
    {
        var result = _converter.Convert(false, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("📄", result);
    }

    [Fact]
    public void ConvertWithNullReturnsFileEmoji()
    {
        var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("📄", result);
    }

    [Fact]
    public void ConvertBackThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("📁", typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(true, "📁")]
    [InlineData(false, "📄")]
    [InlineData(null, "📄")]
    public void ConvertNullableBoolReturnsExpectedGlyph(bool? value, string expected)
    {
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(1.0)]
    [InlineData("📁")]
    public void ConvertNonBoolValuesReturnFileGlyph(object value)
    {
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("📄", result);
    }

    [Fact]
    public void ConvertBoxedTrueReturnsFolderGlyph()
    {
        object boxed = true;
        var result = _converter.Convert(boxed, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.Equal("📁", result);
    }

    [Fact]
    public void ConvertIgnoresTargetTypeAndParameter()
    {
        Assert.Equal("📁", _converter.Convert(true, typeof(int), "folder", CultureInfo.InvariantCulture));
        Assert.Equal("📄", _converter.Convert(false, typeof(object), 42, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertIsIndependentOfDecimalSeparatorCulture()
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";

        Assert.Equal("📁", _converter.Convert(true, typeof(string), null, culture));
        Assert.Equal("📄", _converter.Convert(false, typeof(string), null, culture));
        Assert.Equal("📄", _converter.Convert(null, typeof(string), null, culture));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ConvertReturnsStringGlyphObject(bool? value)
    {
        var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);
        Assert.IsType<string>(result);
    }

    [Theory]
    [InlineData("📄")]
    [InlineData(null)]
    [InlineData(false)]
    public void ConvertBackWithAnyValueThrowsNotSupportedException(object? value)
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(value, typeof(bool), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBackExceptionMessageExplainsOneWayConversion()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack(false, typeof(bool), null, CultureInfo.InvariantCulture));
        Assert.Contains("one-way", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}