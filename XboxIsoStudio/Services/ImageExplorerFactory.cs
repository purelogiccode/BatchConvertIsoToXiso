using System.IO;
using XboxIsoStudio.Interfaces;

namespace XboxIsoStudio.Services;

/// <summary>
/// Opens an <see cref="IImageExplorer"/> for a supported image path: ZAR archives
/// are read with ZArchiveSharp, everything else (plain ISO, CISO) with XISOSharp.
/// </summary>
public static class ImageExplorerFactory
{
    public static IImageExplorer Open(string imagePath)
    {
        return Path.GetExtension(imagePath).Equals(".zar", StringComparison.OrdinalIgnoreCase)
            ? new ZarImageExplorer(imagePath)
            : new XisoImageExplorer(imagePath);
    }
}
