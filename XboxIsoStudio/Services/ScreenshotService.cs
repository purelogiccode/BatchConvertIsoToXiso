using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class ScreenshotService : IScreenshotService
{
    private readonly ILogger _logger;

    public ScreenshotService(ILogger logger)
    {
        _logger = logger.ForContext<ScreenshotService>();
    }

    public async Task<string?> CaptureActiveWindowAsync()
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(CaptureActiveWindow);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error capturing screenshot");
            return null;
        }
    }

    private string? CaptureActiveWindow()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;

        if (window?.IsVisible != true)
        {
            _logger.Information("Screenshot: No active window found.");
            return null;
        }

        var width = window.ClientSize.Width;
        var height = window.ClientSize.Height;

        if (width <= 0 || height <= 0)
        {
            _logger.Information("Screenshot: Invalid window dimensions.");
            return null;
        }

        // Render at the window's device scale so the screenshot matches what is on screen.
        var scaling = window.RenderScaling;
        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Round(width * scaling)),
            Math.Max(1, (int)Math.Round(height * scaling)));

        using var bitmap = new RenderTargetBitmap(pixelSize, new Vector(96 * scaling, 96 * scaling));
        bitmap.Render(window);

        var screenshotsDir = Path.Combine(AppContext.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(screenshotsDir);

        // Milliseconds plus a uniqueness probe: two captures in the same second (or even the
        // same millisecond) must not overwrite each other.
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(screenshotsDir, $"Screenshot_{timestamp}.png");
        for (var counter = 2; File.Exists(filePath); counter++)
        {
            filePath = Path.Combine(screenshotsDir,
                $"Screenshot_{timestamp}_{counter.ToString(CultureInfo.InvariantCulture)}.png");
        }

        bitmap.Save(filePath, PngBitmapEncoderOptions.Default);

        _logger.Information("Screenshot saved: {FilePath}", filePath);
        return filePath;
    }
}