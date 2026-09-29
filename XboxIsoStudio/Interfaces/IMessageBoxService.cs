namespace XboxIsoStudio.Interfaces;

/// <summary>
/// Shows message boxes to the user.
/// </summary>
public interface IMessageBoxService
{
    /// <summary>
    /// Shows a message box with the given buttons and icon and waits for the user's choice.
    /// </summary>
    /// <param name="message">Message text displayed in the dialog.</param>
    /// <param name="title">Dialog title.</param>
    /// <param name="buttons">Buttons shown in the dialog.</param>
    /// <param name="icon">Icon shown in the dialog.</param>
    /// <returns>The button the user clicked.</returns>
    Task<UiMessageBoxResult> ShowAsync(string message, string title, UiMessageBoxButton buttons,
        UiMessageBoxImage icon);

    /// <summary>Shows an error message box with an OK button.</summary>
    /// <param name="message">Error message to display.</param>
    Task ShowErrorAsync(string message);

    /// <summary>Shows a warning message box with an OK button.</summary>
    /// <param name="message">Warning message to display.</param>
    /// <param name="title">Dialog title.</param>
    Task ShowWarningAsync(string message, string title);
}