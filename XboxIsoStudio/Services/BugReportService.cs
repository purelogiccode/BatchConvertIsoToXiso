using System.Globalization;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class BugReportService : IBugReportService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiKey;
    private readonly string _applicationName;
    private readonly ILogger? _logger;

    public BugReportService(HttpClient httpClient, string apiUrl, string apiKey, string applicationName,
        ILogger? logger = null)
    {
        _apiUrl = apiUrl;
        _apiKey = apiKey;
        _applicationName = applicationName;

        // The injected client is never mutated: headers and the timeout are applied per
        // request so a shared or reused client cannot be corrupted by this service.
        _httpClient = httpClient;
        _logger = logger?.ForContext<BugReportService>();
    }

    public Task<bool> SendBugReportAsync(string message)
    {
        var fullMessage = BuildFullMessage(message);
        var version = GetApplicationVersion.GetProgramVersion();
        return SendToApiAsync(fullMessage, version, null);
    }

    public Task<bool> SendBugReportAsync(string errorMessage, Exception exception)
    {
        var sb = new StringBuilder(BuildFullMessage(errorMessage));
        sb.AppendLine();
        sb.AppendLine("=== Exception Details ===");
        ExceptionFormatter.AppendExceptionDetails(sb, exception);
        var version = GetApplicationVersion.GetProgramVersion();
        return SendToApiAsync(sb.ToString(), version, exception);
    }

    private async Task<bool> SendToApiAsync(string fullMessage, string version, Exception? exception)
    {
        try
        {
            var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                { "message", fullMessage },
                { "applicationName", _applicationName },
                { "version", version },
                { "environment", GetEnvironmentSummary() },
                { "stackTrace", exception?.StackTrace }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            request.Headers.Add("X-API-KEY", _apiKey);
            request.Content = JsonContent.Create(payload);

            using var timeoutCts = new CancellationTokenSource(RequestTimeout);
            // ConfigureAwait(false) is required: TryReportFatal blocks the UI thread with
            // Wait() on this task, and a captured synchronization context would deadlock
            // until the timeout (the report would never be sent).
            using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);

            if (response.IsSuccessStatusCode) return true;

            // Record the failure so "the error was reported" is never silently false.
            _logger?.Information("Bug report API returned {StatusCode}.", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.Information(ex, "Failed to send bug report to {ApiUrl}.", _apiUrl);
            return false;
        }
    }

    internal static string BuildFullMessage(string message)
    {
        if (message.Contains("=== Environment Details ===", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        var sb = new StringBuilder();
        AppendEnvironmentDetails(sb);
        sb.AppendLine();
        sb.AppendLine("=== Error Details ===");
        sb.AppendLine(message);

        return sb.ToString();
    }

    internal static void AppendEnvironmentDetails(StringBuilder sb)
    {
        sb.AppendLine("=== Environment Details ===");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Date: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Application Name: {App.ApplicationName}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Application Version: {GetApplicationVersion.GetProgramVersion()}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS Version: {Environment.OSVersion}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Architecture: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Bitness: {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Windows Version: {Environment.OSVersion.Version.Major}.{Environment.OSVersion.Version.Minor}.{Environment.OSVersion.Version.Build}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Base Directory: {AppContext.BaseDirectory}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Temp Path: {Path.GetTempPath()}");
    }

    private static string GetEnvironmentSummary()
    {
        var bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
        var summary = $"{Environment.OSVersion} {bitness}";
        return summary.Length <= 50 ? summary : summary[..50];
    }
}
