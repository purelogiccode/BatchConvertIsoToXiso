using XISOStudio.Interfaces;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace XISOStudio.Services;

/// <summary>
/// A Serilog sink that forwards log events at or above a minimum level (Warning by default)
/// to the Bug Report API using the application's <see cref="IBugReportService"/>.
/// Reporting is fire-and-forget and failures are swallowed so that logging never throws.
/// </summary>
public class BugReportSink : ILogEventSink
{
    private readonly Func<IBugReportService?> _bugReportServiceAccessor;
    private readonly IFormatProvider? _formatProvider;
    private readonly LogEventLevel _minimumLevel;
    private readonly MessageTemplateTextFormatter _messageFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="BugReportSink"/> class.
    /// </summary>
    /// <param name="bugReportServiceAccessor">
    /// Accessor returning the bug report service, or <c>null</c> while the application is
    /// still starting up. The service is resolved lazily so the sink can be registered before
    /// the dependency injection container is built.
    /// </param>
    /// <param name="minimumLevel">Lowest level forwarded to the Bug Report API.</param>
    /// <param name="formatProvider">Optional format provider used to render log messages.</param>
    public BugReportSink(
        Func<IBugReportService?> bugReportServiceAccessor,
        LogEventLevel minimumLevel = LogEventLevel.Warning,
        IFormatProvider? formatProvider = null)
    {
        _bugReportServiceAccessor = bugReportServiceAccessor;
        _minimumLevel = minimumLevel;
        _formatProvider = formatProvider;
        // Report the message as plain text, without Serilog 4's JSON-style quotes around
        // string properties.
        _messageFormatter = new MessageTemplateTextFormatter("{Message:l}", formatProvider);
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        // The sink promises that logging never throws, so even the service accessor and the
        // message rendering are guarded: a throwing accessor would otherwise surface through
        // Serilog's pipeline.
        try
        {
            if (logEvent.Level < _minimumLevel) return;

            // Avalonia's internal diagnostics are captured for the log viewer and the log file,
            // but framework warnings are not application defects and must not be auto-reported.
            if (IsFrameworkEvent(logEvent)) return;

            var bugReportService = _bugReportServiceAccessor();
            if (bugReportService is null) return;

            using var writer = new StringWriter(_formatProvider);
            _messageFormatter.Format(logEvent, writer);
            var message = writer.ToString();
            var exception = logEvent.Exception;

            // Fire-and-forget: never block the logging pipeline and never let reporting failures surface.
            _ = Task.Run(async () =>
            {
                try
                {
                    if (exception != null)
                    {
                        await bugReportService.SendBugReportAsync(message, exception);
                    }
                    else
                    {
                        await bugReportService.SendBugReportAsync(message);
                    }
                }
                catch (Exception ex)
                {
                    // Silently ignore reporting failures, but keep a trace through the self-log:
                    // writing to the logging pipeline here would recurse into this sink.
                    Serilog.Debugging.SelfLog.WriteLine("BugReportSink failed to forward a bug report: {0}", ex);
                }
            });
        }
        catch (Exception ex)
        {
            // Never throw from a Serilog sink: writing to the pipeline here would recurse.
            Serilog.Debugging.SelfLog.WriteLine("BugReportSink failed to emit a log event: {0}", ex);
        }
    }

    /// <summary>
    /// Checks whether the log event originates from Avalonia's internal diagnostics, which
    /// must not be auto-reported.
    /// </summary>
    /// <param name="logEvent">Log event to inspect.</param>
    /// <returns><c>true</c> when the event came from Avalonia; otherwise <c>false</c>.</returns>
    private static bool IsFrameworkEvent(LogEvent logEvent)
    {
        return logEvent.Properties.TryGetValue("SourceContext", out var value) &&
               value is ScalarValue { Value: string context } &&
               context.StartsWith("Avalonia", StringComparison.Ordinal);
    }
}