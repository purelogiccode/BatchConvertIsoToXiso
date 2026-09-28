using System.Diagnostics;
using BatchConvertIsoToXiso.Interfaces;
using Serilog;

namespace BatchConvertIsoToXiso.Services;

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
            _logger.Error(ex, "Error opening URL: {Url}", url);
            throw; // Re-throw the exception for the caller to handle UI
        }
    }
}