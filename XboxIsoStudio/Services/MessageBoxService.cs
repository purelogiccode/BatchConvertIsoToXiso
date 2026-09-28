using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using XboxIsoStudio.Dialogs;
using XboxIsoStudio.Interfaces;

namespace XboxIsoStudio.Services;

public class MessageBoxService : IMessageBoxService
{
    public async Task<UiMessageBoxResult> ShowAsync(string message, string title, UiMessageBoxButton buttons,
        UiMessageBoxImage icon)
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

    public Task ShowErrorAsync(string message)
    {
        return ShowAsync(message, "Error", UiMessageBoxButton.Ok, UiMessageBoxImage.Error);
    }

    public Task ShowWarningAsync(string message, string title)
    {
        return ShowAsync(message, title, UiMessageBoxButton.Ok, UiMessageBoxImage.Warning);
    }
}
