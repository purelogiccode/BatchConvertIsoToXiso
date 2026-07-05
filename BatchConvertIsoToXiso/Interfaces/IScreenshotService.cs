namespace BatchConvertIsoToXiso.Interfaces;

public interface IScreenshotService
{
    Task<string?> CaptureActiveWindowAsync();
}
