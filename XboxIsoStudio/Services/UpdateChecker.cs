using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using Serilog;

namespace XboxIsoStudio.Services;

public partial class UpdateChecker : IUpdateChecker
{
    private const string GitHubApiUrl =
        "https://api.github.com/repos/purelogiccode/XboxIsoStudio/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;
    private readonly ILogger _logger;

    public UpdateChecker(HttpClient httpClient, ILogger logger)
        : this(httpClient, GetApplicationVersion.GetProgramVersion(), logger)
    {
    }

    internal UpdateChecker(HttpClient httpClient, string currentVersion, ILogger logger)
    {
        // The injected client is never mutated: the User-Agent header and timeout are
        // applied per request so a shared or reused client cannot be corrupted.
        _httpClient = httpClient;
        _currentVersion = currentVersion;
        _logger = logger.ForContext<UpdateChecker>();
    }

    public async Task<(bool IsNewVersionAvailable, string? LatestVersion, string? DownloadUrl)> CheckForUpdateAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("XboxIsoStudio", _currentVersion));

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var response = await _httpClient.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(responseBody);

            if (releaseInfo?.TagName is null || releaseInfo.HtmlUrl is null)
            {
                return (false, null, null);
            }

            var versionMatch = MyRegex().Match(releaseInfo.TagName);
            if (!versionMatch.Success)
            {
                return (false, null, null);
            }

            var latestVersionStr = versionMatch.Value;

            if (Version.TryParse(latestVersionStr, out var latestVersion) &&
                Version.TryParse(_currentVersion, out var currentVersion) &&
                latestVersion > currentVersion)
            {
                return (true, latestVersion.ToString(), releaseInfo.HtmlUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to check for updates.");
            return (false, null, null);
        }

        return (false, null, null);
    }

    [GeneratedRegex(@"\d+(\.\d+){1,3}", RegexOptions.None | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex MyRegex();
}