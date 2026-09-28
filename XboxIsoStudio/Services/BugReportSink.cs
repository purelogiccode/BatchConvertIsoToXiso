using XboxIsoStudio.Interfaces;
using Serilog.Core;
using Serilog.Events;

namespace XboxIsoStudio.Services;

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
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < _minimumLevel) return;

        var bugReportService = _bugReportServiceAccessor();
        if (bugReportService is null) return;

        var message = logEvent.RenderMessage(_formatProvider);
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
            catch
            {
                // Silently ignore reporting failures.
            }
        });
    }
}