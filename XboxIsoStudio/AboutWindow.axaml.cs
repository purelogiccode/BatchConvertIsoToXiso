using Avalonia.Controls;
using Avalonia.Interactivity;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;
using Serilog;

namespace XboxIsoStudio;

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
            _ = _messageBoxService.ShowErrorAsync($"Unable to open link: {ex.Message}");
        }
    }
}