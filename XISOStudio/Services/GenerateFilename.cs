namespace XISOStudio.Services;

/// <summary>
/// Generates file names for extracted or converted ISO images.
/// </summary>
public static class GenerateFilename
{
    /// <summary>
    /// Generates a zero-padded ISO file name for the specified index.
    /// </summary>
    /// <param name="fileIndex">Zero-based index of the file; must not be negative.</param>
    /// <returns>A file name in the form <c>iso_000000.iso</c>.</returns>
    public static string GenerateSimpleFilename(int fileIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fileIndex);
        return $"iso_{fileIndex:D6}.iso";
    }
}