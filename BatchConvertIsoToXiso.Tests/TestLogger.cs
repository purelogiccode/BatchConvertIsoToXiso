using System.Globalization;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace BatchConvertIsoToXiso.Tests;

/// <summary>
/// A Serilog sink that captures log events so tests can assert on logged messages
/// and levels. Replaces the previous <c>Mock&lt;ILogger&gt;</c> usage.
/// </summary>
internal sealed class TestLogger : ILogEventSink
{
    private readonly List<LogEvent> _events = [];
    private readonly Lock _lock = new();

    public TestLogger()
    {
        Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(this)
            .CreateLogger();
    }

    public ILogger Logger { get; }

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }

    public bool HasMessage(string substring)
    {
        return Events.Any(e => Render(e).Contains(substring, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasMessage(LogEventLevel level, string substring)
    {
        return Events.Any(e => e.Level == level && Render(e).Contains(substring, StringComparison.OrdinalIgnoreCase));
    }

    public bool HasLevel(LogEventLevel level)
    {
        return Events.Any(e => e.Level == level);
    }

    private static string Render(LogEvent logEvent)
    {
        return logEvent.RenderMessage(CultureInfo.InvariantCulture);
    }
}