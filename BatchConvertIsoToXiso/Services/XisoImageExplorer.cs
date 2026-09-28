using BatchConvertIsoToXiso.Interfaces;
using BatchConvertIsoToXiso.Models;
using XISOSharp;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// <see cref="IImageExplorer"/> over a plain ISO or CISO (<c>.cso</c>, including
/// split <c>.1.cso</c> part sets) image, backed by <see cref="XisoExplorer"/> in
/// keep-open mode (one image handle for the explorer's lifetime).
/// </summary>
internal sealed class XisoImageExplorer : IImageExplorer
{
    private readonly XisoExplorer _explorer;

    public XisoImageExplorer(string imagePath)
    {
        _explorer = new XisoExplorer(imagePath, new XisoExplorerOptions { KeepOpen = true });
    }

    /// <inheritdoc/>
    public IReadOnlyList<ImageEntry> ListChildren(string internalPath)
    {
        return _explorer.ListChildren(internalPath)
            .Select(static node => new ImageEntry(node.Name, node.FullPath, node.IsDirectory, node.Size))
            .ToList();
    }

    /// <inheritdoc/>
    public void CopyOut(string internalPath, string destPath)
    {
        _explorer.CopyOut(internalPath, destPath);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _explorer.Dispose();
    }
}
