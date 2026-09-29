using XISOStudio.Interfaces;

namespace XISOStudio.Services;

/// <summary>
/// Opens an <see cref="IImageExplorer"/> for a supported image path: ZAR archives
/// are read with ZArchiveSharp, CHD images with CHDSharp (decompressed on demand),
/// and everything else (plain ISO, CISO) with XISOSharp.
/// </summary>
public static class ImageExplorerFactory
{
    /// <summary>
    /// Opens an image explorer appropriate for the format of the specified file.
    /// </summary>
    /// <param name="imagePath">Path of the image to open.</param>
    /// <returns>An explorer for the image, selected by its file extension.</returns>
    public static IImageExplorer Open(string imagePath)
    {
        var extension = Path.GetExtension(imagePath);

        if (extension.Equals(".zar", StringComparison.OrdinalIgnoreCase))
            return new ZarImageExplorer(imagePath);

        if (extension.Equals(".chd", StringComparison.OrdinalIgnoreCase))
            return new ChdImageExplorer(imagePath);

        return new XisoImageExplorer(imagePath);
    }
}