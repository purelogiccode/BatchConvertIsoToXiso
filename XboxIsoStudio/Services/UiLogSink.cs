using Serilog.Core;
using Serilog.Events;

namespace XboxIsoStudio.Services;

/// <summary>
/// A Serilog sink that forwards rendered log messages to the application UI.
/// The MainWindow subscribes to <see cref="MessageLogged"/> to display messages
/// in the on-screen log viewer while keeping the logging pipeline decoupled from the UI.
/// </summary>
public class UiLogSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;

    /// <summary>
    /// Event arguments for <see cref="MessageLogged"/>: a pre-formatted, timestamped
    /// log line ready for display.
    /// </summary>
    public sealed class LogMessageEventArgs(string message) : EventArgs
    {
        /// <summary>The formatted log line.</summary>
        public string Message { get; } = message;
    }

    /// <summary>
    /// Raised for every log event, providing a pre-formatted, timestamped line ready for display.
    /// </summary>
    public static event EventHandler<LogMessageEventArgs>? MessageLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="UiLogSink"/> class.
    /// </summary>
    /// <param name="formatProvider">Optional format provider used to render log messages.</param>
    public UiLogSink(IFormatProvider? formatProvider = null)
    {
        _formatProvider = formatProvider;
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        var handler = MessageLogged;
        if (handler is null) return;

        var message = logEvent.RenderMessage(_formatProvider);
        var line = $"[{logEvent.Timestamp:HH:mm:ss}] {message}";
        handler(null, new LogMessageEventArgs(line));
    }
}