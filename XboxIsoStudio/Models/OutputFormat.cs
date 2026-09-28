namespace XboxIsoStudio.Models;

/// <summary>
///     Output format produced by the conversion pipeline.
/// </summary>
public enum OutputFormat
{
    /// <summary>Optimized XISO image (default).</summary>
    Xiso = 0,

    /// <summary>ZArchive (<c>.zar</c>) compressed image — zstd blocks, loadable in Xenia canary.</summary>
    Zar = 1,

    /// <summary>Compressed ISO (<c>.cso</c>) — CISO v2 (LZ4), compatible with the xdvdfs ecosystem.</summary>
    Cso = 2
}
