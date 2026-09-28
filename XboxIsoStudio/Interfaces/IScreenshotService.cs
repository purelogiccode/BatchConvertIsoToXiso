namespace XboxIsoStudio.Interfaces;

public interface IScreenshotService
{
    Task<string?> CaptureActiveWindowAsync();
}