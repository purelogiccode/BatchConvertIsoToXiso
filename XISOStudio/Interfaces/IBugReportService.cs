namespace XISOStudio.Interfaces;

/// <summary>
/// Sends diagnostic bug reports to the remote Bug Report API.
/// </summary>
public interface IBugReportService
{
    /// <summary>
    /// Sends a bug report containing a message and the current environment details.
    /// </summary>
    /// <param name="message">The message describing the issue.</param>
    /// <returns><c>true</c> when the report was accepted by the API; otherwise <c>false</c>.</returns>
    Task<bool> SendBugReportAsync(string message);

    /// <summary>
    /// Sends a bug report containing an error message, the current environment details and
    /// the full details of an exception.
    /// </summary>
    /// <param name="errorMessage">The message describing the issue.</param>
    /// <param name="exception">The exception whose details are included in the report.</param>
    /// <returns><c>true</c> when the report was accepted by the API; otherwise <c>false</c>.</returns>
    Task<bool> SendBugReportAsync(string errorMessage, Exception exception);
}