namespace XboxIsoStudio.Models;

/// <summary>
/// A row displayed in the XISO explorer: a file or directory inside an image or archive.
/// </summary>
public class XisoExplorerItem
{
    /// <summary>Gets the entry name (file or folder name).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets or sets the formatted display size (for example "1.5 MB").</summary>
    public string SizeFormatted { get; set; } = string.Empty;

    /// <summary>Gets the display type, either "Folder" or "File".</summary>
    public string Type => IsDirectory ? "Folder" : "File";

    /// <summary>Gets a value indicating whether this entry is a directory.</summary>
    public bool IsDirectory { get; init; }

    // ReSharper disable once NullableWarningSuppressionIsUsed
    /// <summary>Gets the underlying image entry this row represents.</summary>
    public ImageEntry Entry { get; init; } = null!;
}