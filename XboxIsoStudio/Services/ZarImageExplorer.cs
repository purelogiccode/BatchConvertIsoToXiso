using System.IO;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using ZArchiveSharp;

namespace XboxIsoStudio.Services;

/// <summary>
/// <see cref="IImageExplorer"/> over a ZAR archive (ZArchive/zstd), backed by
/// <see cref="ZArchiveReader"/>. The reader is kept open for the explorer's
/// lifetime, so navigation and copy-out share one archive handle and the block
/// cache.
/// </summary>
internal sealed class ZarImageExplorer : IImageExplorer
{
    private readonly ZArchiveReader _reader;

    public ZarImageExplorer(string zarPath)
    {
        _reader = ZArchiveReader.TryOpen(zarPath, out var failure)
                  ?? throw new InvalidDataException($"Not a valid ZAR archive ({failure}): {zarPath}");
    }

    /// <inheritdoc/>
    public IReadOnlyList<ImageEntry> ListChildren(string internalPath)
    {
        var path = ImagePaths.Normalize(internalPath);
        var node = LookUpOrThrow(path, internalPath);
        if (!_reader.IsDirectory(node))
        {
            throw new InvalidDataException($"Not a directory: {internalPath}");
        }

        var count = _reader.GetDirEntryCount(node);
        var entries = new List<ImageEntry>((int)Math.Min(count, int.MaxValue));
        for (uint i = 0; i < count; i++)
        {
            if (!_reader.TryGetDirEntry(node, i, out _, out var entry)) continue;

            var fullPath = ImagePaths.Combine(path, entry.Name);
            entries.Add(new ImageEntry(entry.Name, fullPath, entry.IsDirectory,
                entry.IsDirectory ? 0 : (long)Math.Min(entry.Size, long.MaxValue)));
        }

        return entries;
    }

    /// <inheritdoc/>
    public void CopyOut(string internalPath, string destPath)
    {
        var path = ImagePaths.Normalize(internalPath);
        var node = LookUpOrThrow(path, internalPath);
        CopyNodeOut(node, destPath);
    }

    private uint LookUpOrThrow(string normalizedPath, string internalPath)
    {
        var node = _reader.LookUp(normalizedPath);
        if (node == ZArchiveReader.InvalidNode)
        {
            throw new InvalidDataException($"Path not found: {internalPath}");
        }

        return node;
    }

    private void CopyNodeOut(uint node, string destPath)
    {
        if (_reader.IsFile(node))
        {
            var directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using var source = _reader.OpenRead(node);
            using var destination = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            source.CopyTo(destination);
            return;
        }

        Directory.CreateDirectory(destPath);
        var count = _reader.GetDirEntryCount(node);
        for (uint i = 0; i < count; i++)
        {
            if (!_reader.TryGetDirEntry(node, i, out var child, out var entry))
            {
                throw new InvalidDataException("Archive directory contains an invalid entry.");
            }

            // Entry names come from the archive; reject names that would escape the
            // destination root (zip-slip), mirroring ZArchiveSharp's extractor.
            if (!IsSafeEntryName(entry.Name))
            {
                throw new InvalidDataException($"Archive entry name is not safe to extract: '{entry.Name}'.");
            }

            CopyNodeOut(child, Path.Combine(destPath, entry.Name));
        }
    }

    /// <summary>
    /// True when an archive entry name is a single, plain filesystem component:
    /// no separators, not relative (".", ".."), not rooted or drive-qualified, and
    /// not a reserved Windows device name.
    /// </summary>
    private static bool IsSafeEntryName(string name)
    {
        if (name.Length == 0 || name is "." or ".."
                             || name.Contains('/') || name.Contains('\\')
                             || Path.IsPathRooted(name))
        {
            return false;
        }

        if (name.Length >= 2 && name[1] == ':' && char.IsAsciiLetter(name[0]))
        {
            return false;
        }

        return !IsReservedDeviceName(name);
    }

    private static bool IsReservedDeviceName(string name)
    {
        var dot = name.IndexOf('.');
        var stem = dot < 0 ? name : name[..dot];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return stem.Length == 4
               && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                   || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
               && stem[3] is >= '1' and <= '9';
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _reader.Dispose();
    }
}
