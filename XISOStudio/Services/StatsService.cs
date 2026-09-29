using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XISOStudio.Interfaces;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Sends anonymous startup statistics (application ID and version) to the configured stats API.
/// </summary>
public class StatsService : IStatsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiKey;
    private readonly string _applicationId;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to send the statistics request.</param>
    /// <param name="apiUrl">URL of the stats API endpoint.</param>
    /// <param name="apiKey">Bearer token used to authorize the request.</param>
    /// <param name="applicationId">Identifier sent with the statistics payload.</param>
    /// <param name="logger">Logger used for diagnostics.</param>
    public StatsService(HttpClient httpClient, string apiUrl, string apiKey, string applicationId, ILogger logger)
    {
        _apiUrl = apiUrl;
        _apiKey = apiKey;
        _applicationId = applicationId;
        _httpClient = httpClient;
        _logger = logger.ForContext<StatsService>();
    }

    /// <summary>
    /// Sends the current application version to the stats API. Failures are best-effort and
    /// are only logged.
    /// </summary>
    /// <returns>A task that completes when the request has finished.</returns>
    public async Task SendStatsAsync()
    {
        try
        {
            var version = GetApplicationVersion.GetProgramVersion();
            var payload = new { applicationId = _applicationId, version };
            var json = JsonSerializer.Serialize(payload);

            // Authorization is applied per request instead of mutating the injected client.
            using var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // The stats API reports authorization failures as HTTP 200 with an error body,
            // so the body is inspected instead of trusting the status code alone.
            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (responseBody.Contains("\"error\"", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Information("Application statistics were not recorded: {Response}", responseBody);
                return;
            }

            _logger.Information("Application statistics sent for {ApplicationId} v{Version}", _applicationId,
                version);
        }
        catch (Exception ex)
        {
            // Stats failures are best-effort and must not be forwarded as bug reports
            _logger.Debug(ex, "Failed to send startup statistics.");
        }
    }
}