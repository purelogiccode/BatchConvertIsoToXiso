using CHDSharp;
using CHDSharp.Models;
using XISOStudio.Interfaces;
using XISOStudio.Models;
using XISOSharp;

namespace XISOStudio.Services;

/// <summary>
/// <see cref="IImageExplorer"/> over a CHD (Compressed Hunks of Data) image. The CHD is
/// decompressed on demand through a keep-open <see cref="ChdImageStream"/> and the Xbox
/// filesystem is enumerated with XISOSharp, so navigation and copy-out share one handle.
/// Parsing is limited to Xbox DVD images; other CHD media types fail to open.
/// </summary>
internal sealed class ChdImageExplorer : IImageExplorer
{
    private readonly ChdImageStream _stream;
    private readonly string _imageName;

    public ChdImageExplorer(string chdPath)
    {
        _imageName = Path.GetFileName(chdPath);

        // Parsing is limited to Xbox DVD images: reject CD/GD-ROM/hard-disk/unknown CHDs at
        // open time instead of failing later in ListChildren/CopyOut.
        var classifyError = Chd.Classify(chdPath, out var classification);
        if (classifyError != ChdError.Chderrnone || !string.Equals(classification, "dvd", StringComparison.Ordinal))
        {
            var reason = classifyError != ChdError.Chderrnone
                ? classifyError.GetMessage()
                : classification ?? "unknown media type";
            throw new InvalidDataException($"CHD image is not an Xbox DVD image ({reason}): {chdPath}");
        }

        var error = ChdFile.OpenAsStream(chdPath, out var stream);
        if (error != ChdError.Chderrnone || stream is null)
        {
            throw new InvalidDataException($"Not a valid CHD image ({error.GetMessage()}): {chdPath}");
        }

        _stream = stream;
    }

    /// <inheritdoc/>
    public IReadOnlyList<ImageEntry> ListChildren(string internalPath)
    {
        var path = ImagePaths.Normalize(internalPath);
        var entries = XisoReader.ListDirectory(_stream, _imageName, path);

        return entries.Select(entry => new ImageEntry(entry.Name,
                ImagePaths.Combine(path, entry.Name), entry.IsDirectory,
                entry.IsDirectory ? 0 : entry.FileSize))
            .ToList();
    }

    /// <inheritdoc/>
    public void CopyOut(string internalPath, string destPath)
    {
        XisoReader.CopyOut(_stream, _imageName, ImagePaths.Normalize(internalPath), destPath);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _stream.Dispose();
    }
}