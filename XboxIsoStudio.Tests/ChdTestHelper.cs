using CHDSharp.Encoder;
using CHDSharp.Encoder.Models;

namespace XboxIsoStudio.Tests;

/// <summary>
/// Builds CHD v5 test images with the same DVD preset the application uses, so the
/// integrity and explorer tests exercise real CHD files. Zlib-only compression keeps
/// the tests fast; the preset codec list is irrelevant to the tested code paths.
/// </summary>
internal static class ChdTestHelper
{
    public static string CreateDvdChd(string sourcePath, string chdPath)
    {
        var options = new ChdEncodeOptions { Metadata = [MetadataWriter.BuildDvdMetadata()] };
        using var source = File.OpenRead(sourcePath);
        ChdEncoder.EncodeRaw(source, chdPath, 4096, 2048, [CodecTags.Zlib], options);
        return chdPath;
    }

    public static string CreateRawChd(string sourcePath, string chdPath)
    {
        using var source = File.OpenRead(sourcePath);
        ChdEncoder.EncodeRaw(source, chdPath, 4096, 512, [CodecTags.Zlib]);
        return chdPath;
    }
}
