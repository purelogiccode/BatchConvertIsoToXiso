using System.Diagnostics;
using XISOStudio.Interfaces;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Opens URLs with the operating system's default browser or handler.
/// </summary>
public class UrlOpenerService : IUrlOpener
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UrlOpenerService"/> class.
    /// </summary>
    /// <param name="logger">Logger used for diagnostics.</param>
    public UrlOpenerService(ILogger logger)
    {
        _logger = logger.ForContext<UrlOpenerService>();
    }

    /// <summary>
    /// Opens the given URL with the operating system's default handler.
    /// </summary>
    /// <param name="url">The URL to open.</param>
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