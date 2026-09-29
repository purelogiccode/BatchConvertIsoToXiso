using Avalonia;

namespace XISOStudio;

/// <summary>Application entry point and Avalonia bootstrap.</summary>
internal static class Program
{
    /// <summary>Configures logging and starts the Avalonia desktop application.</summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure logging before Avalonia initializes its platform subsystems so
        // framework diagnostics emitted during startup are captured as well.
        App.ConfigureLogging();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Builds the Avalonia application with platform detection.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        // Avalonia's own diagnostics are routed to Serilog by AvaloniaSerilogSink,
        // configured in the App constructor, instead of the Trace-based default.
        return AppBuilder.Configure<App>()
            .UsePlatformDetect();
    }
}