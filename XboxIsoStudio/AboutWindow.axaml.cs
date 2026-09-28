using Avalonia.Controls;
using Avalonia.Interactivity;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;
using Serilog;

namespace XboxIsoStudio;

public partial class AboutWindow : Window
{
    private readonly IUrlOpener _urlOpener = null!;
    private readonly IMessageBoxService _messageBoxService = null!;
    private readonly ILogger _logger = null!;

    public AboutWindow()
    {
        InitializeComponent();
    }

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