using Avalonia.Controls;
using Avalonia.Media;
using XboxIsoStudio.Interfaces;

namespace XboxIsoStudio.Dialogs;

public partial class MessageBoxWindow : Window
{
    private readonly TaskCompletionSource<UiMessageBoxResult> _result = new();

    /// <summary>Completion source used when the dialog is shown without an owner window.</summary>
    public Task<UiMessageBoxResult> Result => _result.Task;

    public MessageBoxWindow()
    {
        InitializeComponent();

        // Closing via the window chrome completes the no-owner path with the default result.
        Closed += (_, _) => _result.TrySetResult(UiMessageBoxResult.Ok);
    }

    public MessageBoxWindow(string message, string title, UiMessageBoxButton buttons, UiMessageBoxImage icon)
        : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        (IconText.Text, IconText.Foreground) = icon switch
        {
            UiMessageBoxImage.Warning => ("⚠", new SolidColorBrush(Color.Parse("#ffa726"))),
            UiMessageBoxImage.Error => ("⛔", new SolidColorBrush(Color.Parse("#f85149"))),
            _ => ("ℹ", new SolidColorBrush(Color.Parse("#00bcd4")))
        };

        YesButton.IsVisible = buttons is UiMessageBoxButton.YesNo or UiMessageBoxButton.YesNoCancel;
        NoButton.IsVisible = buttons is UiMessageBoxButton.YesNo or UiMessageBoxButton.YesNoCancel;
        CancelButton.IsVisible = buttons is UiMessageBoxButton.YesNoCancel;
        OkButton.IsVisible = buttons is UiMessageBoxButton.Ok;

        if (buttons == UiMessageBoxButton.Ok)
        {
            OkButton.Focus();
        }
        else
        {
            YesButton.Focus();
        }
    }

    private void YesButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Complete(UiMessageBoxResult.Yes);
    }

    private void NoButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Complete(UiMessageBoxResult.No);
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Complete(UiMessageBoxResult.Cancel);
    }

    private void OkButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Complete(UiMessageBoxResult.Ok);
    }

    private void Complete(UiMessageBoxResult result)
    {
        _result.TrySetResult(result);
        Close(result);
    }
}
