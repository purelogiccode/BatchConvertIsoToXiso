using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using BatchConvertIsoToXiso.Interfaces;

namespace BatchConvertIsoToXiso.Services;

public class ScreenshotService : IScreenshotService
{
    private readonly ILogger _logger;
    private readonly IBugReportService _bugReportService;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);

    private const uint Srccopy = 0x00CC0020;
    private const int DwmwaExtendedFrameBounds = 9;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public ScreenshotService(ILogger logger, IBugReportService bugReportService)
    {
        _logger = logger;
        _bugReportService = bugReportService;
    }

    public async Task<string?> CaptureActiveWindowAsync()
    {
        try
        {
            return await Task.Run(CaptureActiveWindow);
        }
        catch (Exception ex)
        {
            _logger.LogMessage($"Error capturing screenshot: {ex.Message}");
            _ = _bugReportService.SendBugReportAsync("Error capturing screenshot", ex);
            return null;
        }
    }

    private string? CaptureActiveWindow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogMessage("Screenshot: No active window found.");
            return null;
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            _logger.LogMessage("Screenshot: Failed to get window rectangle.");
            return null;
        }

        var hr = DwmGetWindowAttribute(hwnd, DwmwaExtendedFrameBounds, out var extendedRect, Marshal.SizeOf<Rect>());
        if (hr == 0)
        {
            rect = extendedRect;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        if (width <= 0 || height <= 0)
        {
            _logger.LogMessage("Screenshot: Invalid window dimensions.");
            return null;
        }

        var screenDc = GetDC(IntPtr.Zero);
        var memDc = CreateCompatibleDC(screenDc);
        var hBitmap = CreateCompatibleBitmap(screenDc, width, height);
        var oldBitmap = SelectObject(memDc, hBitmap);

        BitBlt(memDc, 0, 0, width, height, screenDc, rect.Left, rect.Top, Srccopy);

        SelectObject(memDc, oldBitmap);

        var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
            hBitmap,
            IntPtr.Zero,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        DeleteObject(hBitmap);
        DeleteDC(memDc);
        _ = ReleaseDC(IntPtr.Zero, screenDc);

        var screenshotsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(screenshotsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(screenshotsDir, $"Screenshot_{timestamp}.png");

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
            encoder.Save(fileStream);
        }

        _logger.LogMessage($"Screenshot saved: {filePath}");
        return filePath;
    }
}
