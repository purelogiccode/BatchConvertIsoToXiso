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

    /// <summary>The user clicked No, or closed a dialog that offered only Yes and No.</summary>
    No,

    /// <summary>The user clicked Cancel, or closed a dialog that offered Cancel.</summary>
    Cancel
}