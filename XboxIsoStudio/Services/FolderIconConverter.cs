using System.Globalization;
using Avalonia.Data.Converters;

namespace XboxIsoStudio.Services;

/// <inheritdoc />
/// <summary>
/// Converter to show folder or file icons in the XIso Explorer
/// </summary>
public class FolderIconConverter : IValueConverter
{
    /// <summary>
    /// Converts a Boolean folder indicator into the matching folder or file icon glyph.
    /// </summary>
    /// <param name="value">Value to convert; <c>true</c> selects the folder icon.</param>
    /// <param name="targetType">Type of the binding target (not used).</param>
    /// <param name="parameter">Converter parameter (not used).</param>
    /// <param name="culture">Culture for the conversion (not used).</param>
    /// <returns>The folder glyph when <paramref name="value"/> is <c>true</c>; otherwise the file glyph.</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "📁" : "📄";
    }

    /// <summary>
    /// Not supported; this converter is one-way only.
    /// </summary>
    /// <param name="value">Value to convert (ignored).</param>
    /// <param name="targetType">Type of the binding target (ignored).</param>
    /// <param name="parameter">Converter parameter (ignored).</param>
    /// <param name="culture">Culture for the conversion (ignored).</param>
    /// <returns>Never returns; always throws <see cref="NotSupportedException"/>.</returns>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("FolderIconConverter is a one-way converter.");
    }
}