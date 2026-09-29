namespace XboxIsoStudio.Interfaces;

/// <summary>
/// Opens URLs with the user's default browser or handler.
/// </summary>
public interface IUrlOpener
{
    /// <summary>Opens the given URL with the default shell handler.</summary>
    /// <param name="url">URL to open.</param>
    void OpenUrl(string url);
}