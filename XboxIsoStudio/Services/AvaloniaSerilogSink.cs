using Avalonia.Logging;
using Serilog;
using AvaloniaLogEventLevel = Avalonia.Logging.LogEventLevel;
using SerilogLogEventLevel = Serilog.Events.LogEventLevel;

namespace XboxIsoStudio.Services;

/// <summary>
/// Forwards Avalonia's internal diagnostic log events into the Serilog pipeline, so the
/// application has a single logging path for both its own messages and the framework's.
/// </summary>
public sealed class AvaloniaSerilogSink : ILogSink
{
    private const AvaloniaLogEventLevel MinimumLevel = AvaloniaLogEventLevel.Warning;

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaSerilogSink"/> class.
    /// </summary>
    /// <param name="logger">Serilog logger that receives Avalonia's log events.</param>
    public AvaloniaSerilogSink(ILogger logger)
    {
        _logger = logger.ForContext("SourceContext", "Avalonia");
    }

    /// <inheritdoc/>
    public bool IsEnabled(AvaloniaLogEventLevel level, string area)
    {
        return level >= MinimumLevel;
    }

    /// <inheritdoc/>
    public void Log(AvaloniaLogEventLevel level, string area, object? source, string messageTemplate)
    {
        Log(level, area, source, messageTemplate, []);
    }

    /// <inheritdoc/>
    public void Log(AvaloniaLogEventLevel level, string area, object? source, string messageTemplate,
        params object?[] propertyValues)
    {
        _logger
            .ForContext("Area", area)
            .ForContext("Source", source)
            .Write((SerilogLogEventLevel)(int)level, messageTemplate, propertyValues);
    }
}
