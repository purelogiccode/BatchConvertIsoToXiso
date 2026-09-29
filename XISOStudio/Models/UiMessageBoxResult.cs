namespace XISOStudio.Models;

/// <summary>
/// Button the user clicked in a message box.
/// </summary>
public enum UiMessageBoxResult
{
    /// <summary>The user clicked OK.</summary>
    Ok,

    /// <summary>The user clicked Yes.</summary>
    Yes,

    /// <summary>The user clicked No.</summary>
    No,

    /// <summary>The user clicked Cancel or closed the dialog.</summary>
    Cancel
}