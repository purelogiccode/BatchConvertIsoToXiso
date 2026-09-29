using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using XISOStudio.Dialogs;
using XISOStudio.Interfaces;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Shows message boxes to the user through an Avalonia dialog window, displaying them modally
/// when the main window is visible and non-modally otherwise.
/// </summary>
public class MessageBoxService : IMessageBoxService
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBoxService"/> class.
    /// </summary>
    /// <param name="logger">Logger used to record dialog failures.</param>
    public MessageBoxService(ILogger logger)
    {
        _logger = logger.ForContext<MessageBoxService>();
    }

    /// <summary>
    /// Shows a message box with the given message, title, buttons and icon.
    /// </summary>
    /// <param name="message">The message text to display.</param>
    /// <param name="title">The window title.</param>
    /// <param name="buttons">The buttons offered to the user.</param>
    /// <param name="icon">The icon shown in the message box.</param>
    /// <returns>The button that the user selected.</returns>
    public async Task<UiMessageBoxResult> ShowAsync(string message, string title, UiMessageBoxButton buttons,
        UiMessageBoxImage icon)
    {
        try
        {
            var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var dialog = new MessageBoxWindow(message, title, buttons, icon);

            if (owner is { IsVisible: true })
            {
                return await dialog.ShowDialog<UiMessageBoxResult>(owner);
            }

            dialog.Show();
            return await dialog.Result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to show the message box '{Title}'", title);
            throw;
        }
    }

    /// <summary>
    /// Shows an error message box with an OK button.
    /// </summary>
    /// <param name="message">The error message text to display.</param>
    /// <returns>A task that completes when the message box is closed.</returns>
    public Task ShowErrorAsync(string message)
    {
        return ShowAsync(message, "Error", UiMessageBoxButton.Ok, UiMessageBoxImage.Error);
    }

    /// <summary>
    /// Shows a warning message box with an OK button and the given title.
    /// </summary>
    /// <param name="message">The warning message text to display.</param>
    /// <param name="title">The window title.</param>
    /// <returns>A task that completes when the message box is closed.</returns>
    public Task ShowWarningAsync(string message, string title)
    {
        return ShowAsync(message, title, UiMessageBoxButton.Ok, UiMessageBoxImage.Warning);
    }
}