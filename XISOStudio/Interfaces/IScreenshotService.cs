namespace XISOStudio.Interfaces;

/// <summary>
/// Captures screenshots of the application window.
/// </summary>
public interface IScreenshotService
{
    /// <summary>
    /// Captures the active application window and saves it as a PNG file in the
    /// application's "Screenshot" folder, falling back to the per-user application data
    /// folder when the application folder is not writable.
    /// </summary>
    /// <returns>The full path of the saved screenshot, or <c>null</c> when no window is visible or the capture failed.</returns>
    Task<string?> CaptureActiveWindowAsync();
}