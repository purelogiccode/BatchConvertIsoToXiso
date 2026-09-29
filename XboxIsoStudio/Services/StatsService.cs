using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using XboxIsoStudio.Interfaces;
using Serilog;

namespace XboxIsoStudio.Services;

public class StatsService : IStatsService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiKey;
    private readonly string _applicationId;
    private readonly ILogger _logger;

    public StatsService(HttpClient httpClient, string apiUrl, string apiKey, string applicationId, ILogger logger)
    {
        _apiUrl = apiUrl;
        _apiKey = apiKey;
        _applicationId = applicationId;
        _httpClient = httpClient;
        _logger = logger.ForContext<StatsService>();
    }

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
        }
        catch (Exception ex)
        {
            // Stats failures are best-effort and must not be forwarded as bug reports
            _logger.Debug(ex, "Failed to send startup statistics.");
        }
    }
}