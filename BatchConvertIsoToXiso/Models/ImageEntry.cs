namespace BatchConvertIsoToXiso.Models;

/// <summary>
/// A single file or directory inside an image or archive, as surfaced by
/// <see cref="Interfaces.IImageExplorer"/>. Paths are container-internal,
/// <c>/</c>-separated and case-insensitive (<c>"/sub/readme.txt"</c>).
/// </summary>
/// <param name="Name">Entry file name (no separators).</param>
/// <param name="FullPath">Container-internal path (<c>"/"</c> for the root).</param>
/// <param name="IsDirectory">Whether this entry is a directory.</param>
/// <param name="Size">File byte size (0 for directories).</param>
public sealed record ImageEntry(string Name, string FullPath, bool IsDirectory, long Size);
