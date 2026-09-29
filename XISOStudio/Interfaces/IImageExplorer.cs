using XISOStudio.Models;

namespace XISOStudio.Interfaces;

/// <summary>
/// A UI-agnostic explorer over one open image or archive: plain ISO and CISO
/// (<c>.cso</c>) images via XISOSharp, ZAR archives via ZArchiveSharp, and CHD
/// images via CHDSharp (decompressed on demand). Internal paths use forward
/// slashes and are case-insensitive; <c>"/"</c> is the root.
/// </summary>
public interface IImageExplorer : IDisposable
{
    /// <summary>
    /// Lists the direct children of a directory within the container.
    /// </summary>
    /// <param name="internalPath">Directory path (<c>"/"</c> for the root).</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when the path does not exist or names a file instead of a directory.
    /// </exception>
    IReadOnlyList<ImageEntry> ListChildren(string internalPath);

    /// <summary>
    /// Copies a single file or directory out of the container (directories recurse).
    /// An existing destination is overwritten.
    /// </summary>
    /// <param name="internalPath">Source path within the container.</param>
    /// <param name="destPath">Destination path on the local filesystem.</param>
    /// <exception cref="InvalidDataException">Thrown when the internal path does not exist.</exception>
    void CopyOut(string internalPath, string destPath);
}