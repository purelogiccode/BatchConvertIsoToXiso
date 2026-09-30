using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace XISOStudio.Services;

/// <summary>
/// A Serilog sink that forwards rendered log messages to the application UI.
/// The MainWindow subscribes to <see cref="MessageLogged"/> to display messages
/// in the on-screen log viewer while keeping the logging pipeline decoupled from the UI.
/// </summary>
public class UiLogSink : ILogEventSink
{
    private readonly IFormatProvider? _formatProvider;
    private readonly MessageTemplateTextFormatter _messageFormatter;

    // Guards against a subscriber that logs again (directly or through the pipeline),
    // which would otherwise recurse into Emit while it is still running.
    [ThreadStatic] private static bool _isEmitting;

    /// <summary>
    /// Event arguments for <see cref="MessageLogged"/>: a pre-formatted, timestamped
    /// log line ready for display.
    /// </summary>
    /// <param name="message">The formatted log line.</param>
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
        // Serilog 4 renders string properties with JSON-style quotes by default
        // (for example "Game.iso"); the literal ("l") format keeps the viewer readable,
        // matching the rolling file log.
        _messageFormatter = new MessageTemplateTextFormatter("{Message:l}", formatProvider);
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        if (_isEmitting) return;

        var handler = MessageLogged;
        if (handler is null) return;

        try
        {
            _isEmitting = true;
            using var writer = new StringWriter(_formatProvider);
            _messageFormatter.Format(logEvent, writer);
            var line = $"[{logEvent.Timestamp:HH:mm:ss}] {writer}";
            handler(null, new LogMessageEventArgs(line));
        }
        catch (Exception ex)
        {
            // A failing subscriber must never propagate back into the Serilog pipeline, and
            // logging about the failure through Serilog would recurse, so use the self-log.
            Serilog.Debugging.SelfLog.WriteLine("UiLogSink subscriber failed to handle a log event: {0}", ex);
        }
        finally
        {
            _isEmitting = false;
        }
    }
}