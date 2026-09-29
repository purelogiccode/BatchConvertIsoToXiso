using System.Diagnostics;
using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class UrlOpenerService : IUrlOpener
{
    private readonly ILogger _logger;

    public UrlOpenerService(ILogger logger)
    {
        _logger = logger.ForContext<UrlOpenerService>();
    }

    public void OpenUrl(string url)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            process?.Dispose();
        }
        catch (Exception ex)
        {
            // No browser/handler configured is an environment condition, not a defect.
            _logger.Information(ex, "Error opening URL: {Url}", url);
            throw; // Re-throw the exception for the caller to handle UI
        }
    }
}
