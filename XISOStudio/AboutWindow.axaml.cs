using Avalonia.Controls;
using Avalonia.Interactivity;
using XISOStudio.Interfaces;
using XISOStudio.Services;
using Serilog;

namespace XISOStudio;

/// <summary>
/// About dialog: shows the application version and links to the project pages.
/// </summary>
public partial class AboutWindow : Window
{
    private readonly IUrlOpener _urlOpener = null!;
    private readonly IMessageBoxService _messageBoxService = null!;
    private readonly ILogger _logger = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class for the XAML designer;
    /// use the dependency-injection constructor at runtime.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class with its runtime
    /// dependencies and fills in the current application version.
    /// </summary>
    /// <param name="urlOpener">Service used to open links in the default browser.</param>
    /// <param name="messageBoxService">Service used to report link failures.</param>
    /// <param name="logger">Serilog logger for the window.</param>
    public AboutWindow(IUrlOpener urlOpener, IMessageBoxService messageBoxService, ILogger logger)
        : this()
    {
        _urlOpener = urlOpener;
        _messageBoxService = messageBoxService;
        _logger = logger.ForContext<AboutWindow>();

        AppVersionTextBlock.Text = $"Version: {GetApplicationVersion.GetProgramVersion()}";
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Hyperlink_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string url }) return;

        try
        {
            _urlOpener.OpenUrl(url);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unable to open link: {Uri}", url);
            ShowErrorSafe($"Unable to open link: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows an error message box without awaiting it, swallowing failures so a dialog
    /// failure cannot surface as an unobserved task exception.
    /// </summary>
    /// <param name="message">Error message to display.</param>
    private void ShowErrorSafe(string message)
    {
        _ = ShowErrorSafeAsync(message);
    }

    /// <summary>Shows an error message box, logging (not throwing) when the dialog itself fails.</summary>
    /// <param name="message">Error message to display.</param>
    private async Task ShowErrorSafeAsync(string message)
    {
        try
        {
            await _messageBoxService.ShowErrorAsync(message);
        }
        catch (Exception ex)
        {
            _logger.Information(ex, "Failed to show an error message box");
        }
    }
}