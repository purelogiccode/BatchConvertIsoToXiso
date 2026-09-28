using System.Windows;
using System.Windows.Navigation;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;
using Serilog;

namespace XboxIsoStudio;

public partial class AboutWindow
{
    private readonly IUrlOpener _urlOpener;
    private readonly IMessageBoxService _messageBoxService;
    private readonly ILogger _logger;

    public AboutWindow(IUrlOpener urlOpener, IMessageBoxService messageBoxService, ILogger logger)
    {
        _urlOpener = urlOpener;
        _messageBoxService = messageBoxService;
        _logger = logger.ForContext<AboutWindow>();
        InitializeComponent();

        AppVersionTextBlock.Text = $"Version: {GetApplicationVersion.GetProgramVersion()}";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            _urlOpener.OpenUrl(e.Uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unable to open link: {Uri}", e.Uri.AbsoluteUri);
            _messageBoxService.ShowError($"Unable to open link: {ex.Message}");
        }

        e.Handled = true;
    }
}