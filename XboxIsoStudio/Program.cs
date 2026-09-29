using Avalonia;

namespace XboxIsoStudio;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        // Avalonia's own diagnostics are routed to Serilog by AvaloniaSerilogSink,
        // configured in the App constructor, instead of the Trace-based default.
        return AppBuilder.Configure<App>()
            .UsePlatformDetect();
    }
}