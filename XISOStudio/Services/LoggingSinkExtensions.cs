using XISOStudio.Interfaces;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace XISOStudio.Services;

/// <summary>
/// Serilog configuration extensions for registering the application's custom sinks.
/// </summary>
public static class LoggingSinkExtensions
{
    extension(LoggerSinkConfiguration sinkConfiguration)
    {
        /// <summary>
        /// Writes log events to the on-screen log viewer through <see cref="UiLogSink"/>.
        /// </summary>
        /// <param name="restrictedToMinimumLevel">Lowest level written to the UI.</param>
        /// <param name="formatProvider">Optional format provider used to render log messages.</param>
        /// <returns>The logger configuration for chaining.</returns>
        public LoggerConfiguration Ui(LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
            IFormatProvider? formatProvider = null)
        {
            return sinkConfiguration.Sink(new UiLogSink(formatProvider), restrictedToMinimumLevel);
        }

        /// <summary>
        /// Forwards log events at or above <paramref name="minimumLevel"/> to the Bug Report API.
        /// </summary>
        /// <param name="bugReportServiceAccessor">
        /// Accessor returning the bug report service; may return <c>null</c> during startup.
        /// </param>
        /// <param name="minimumLevel">Lowest level forwarded to the Bug Report API.</param>
        /// <param name="formatProvider">Optional format provider used to render log messages.</param>
        /// <returns>The logger configuration for chaining.</returns>
        public LoggerConfiguration BugReport(Func<IBugReportService?> bugReportServiceAccessor,
            LogEventLevel minimumLevel = LogEventLevel.Warning,
            IFormatProvider? formatProvider = null)
        {
            return sinkConfiguration.Sink(
                new BugReportSink(bugReportServiceAccessor, minimumLevel, formatProvider),
                minimumLevel);
        }
    }
}