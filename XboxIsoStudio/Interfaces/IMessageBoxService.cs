namespace XboxIsoStudio.Interfaces;

public interface IMessageBoxService
{
    Task<UiMessageBoxResult> ShowAsync(string message, string title, UiMessageBoxButton buttons, UiMessageBoxImage icon);
    Task ShowErrorAsync(string message);
    Task ShowWarningAsync(string message, string title);
}
