using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using XISOStudio.Models;
using XISOStudio.Services;
using Moq;
using Moq.Protected;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests GitHub release version comparison and update detection in <c>UpdateChecker</c>.</summary>
[SuppressMessage("ReSharper", "UnusedVariable")]
public class UpdateCheckerTests
{
    private const string GitHubLatestReleaseUrl =
        "https://api.github.com/repos/purelogiccode/XISOStudio/releases/latest";

    private readonly TestLogger _logger = new();

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return new HttpClient(handlerMock.Object);
    }

    private static string CreateReleaseJson(string tagName, string htmlUrl)
    {
        var release = new GitHubReleaseInfo { TagName = tagName, HtmlUrl = htmlUrl };
        return JsonSerializer.Serialize(release);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNewVersionAvailableReturnsTrueAndVersionInfo()
    {
        var json = CreateReleaseJson("v2.4.0", "https://github.com/test/releases/tag/v2.4.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.1", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("2.4.0", latestVersion);
        Assert.Equal("https://github.com/test/releases/tag/v2.4.0", downloadUrl);
    }

    [Fact]
    public void ConstructorDoesNotMutateInjectedHttpClient()
    {
        using var httpClient = new HttpClient();

        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        Assert.NotNull(checker);
        Assert.Empty(httpClient.DefaultRequestHeaders.UserAgent);
    }

    [Fact]
    public async Task CheckForUpdateAsyncSendsUserAgentHeaderPerRequest()
    {
        string? capturedUserAgent = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                capturedUserAgent = req.Headers.UserAgent.ToString())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateReleaseJson("v1.0.0", "https://example.com"))
            });
        using var httpClient = new HttpClient(handlerMock.Object);

        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);
        await checker.CheckForUpdateAsync();

        Assert.NotNull(capturedUserAgent);
        Assert.Contains("XISOStudio/1.0.0", capturedUserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckForUpdateAsyncSameVersionReturnsFalse()
    {
        var json = CreateReleaseJson("v2.3.1", "https://github.com/test/releases/tag/v2.3.1");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.1", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncRemoteVersionOlderReturnsFalse()
    {
        var json = CreateReleaseJson("v2.2.0", "https://github.com/test/releases/tag/v2.2.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.1", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncTagWithoutVersionPrefixDetectsVersion()
    {
        var json = CreateReleaseJson("release-3.0.0", "https://github.com/test/releases/tag/release-3.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.1", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("3.0.0", latestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNullTagNameReturnsFalse()
    {
        var json = CreateReleaseJson(null!, "https://github.com/test/releases/tag/v1.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (_, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNullHtmlUrlReturnsFalse()
    {
        var json = CreateReleaseJson("v2.0.0", null!);
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (_, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNoVersionInTagReturnsFalse()
    {
        var json = CreateReleaseJson("latest-stable", "https://github.com/test/releases/tag/latest-stable");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncHttpRequestFailsReturnsFalse()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));
        var httpClient = new HttpClient(handlerMock.Object);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncInvalidJsonReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{invalid-json");
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncUnexpectedVersionFormatReturnsFalse()
    {
        var json = CreateReleaseJson("not-a-version-v1.2.3", "https://github.com/test/releases/tag/nonsense");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "not-a-valid-ver", _logger.Logger);

        var (_, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
    }

    [Fact]
    public async Task CheckForUpdateAsyncServerErrorReturnsFalse()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "Server error");
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncMajorVersionBumpReturnsTrue()
    {
        var json = CreateReleaseJson("v3.0.0", "https://github.com/test/releases/tag/v3.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.9.9", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("3.0.0", latestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsyncFourComponentVersionReturnsTrue()
    {
        var json = CreateReleaseJson("v1.2.3.4", "https://github.com/test/releases/tag/v1.2.3.4");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.2.3.3", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("1.2.3.4", latestVersion);
    }

    [Theory]
    [InlineData("v1.2.4", "1.2.4")]
    [InlineData("v2.0.0", "2.0.0")]
    [InlineData("release-3.5.7", "3.5.7")]
    [InlineData("V4.0.0", "4.0.0")]
    [InlineData("1.0.1", "1.0.1")]
    [InlineData("build/9.9.9", "9.9.9")]
    public async Task CheckForUpdateAsyncNewerTagReturnsParsedVersionAndHtmlUrl(string tagName, string expectedVersion)
    {
        var expectedUrl = $"https://github.com/test/releases/tag/{tagName}";
        var json = CreateReleaseJson(tagName, expectedUrl);
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal(expectedVersion, latestVersion);
        Assert.Equal(expectedUrl, downloadUrl);
    }

    [Theory]
    [InlineData("v1.0.0")]
    [InlineData("v0.9.9")]
    [InlineData("v1.0")]
    [InlineData("version-1.0")]
    public async Task CheckForUpdateAsyncSameOrOlderTagReturnsNoUpdate(string tagName)
    {
        var json = CreateReleaseJson(tagName, "https://github.com/test/releases/tag/old");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNewerMinorVersionReturnsTrue()
    {
        var json = CreateReleaseJson("v2.4.0", "https://github.com/test/releases/tag/v2.4.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.9", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("2.4.0", latestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNewerPatchVersionReturnsTrue()
    {
        var json = CreateReleaseJson("v2.3.2", "https://github.com/test/releases/tag/v2.3.2");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "2.3.1", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("2.3.2", latestVersion);
    }

    [Theory]
    [InlineData("v1.1.0-beta.1", "1.1.0")]
    [InlineData("v2.0.0-rc.1", "2.0.0")]
    [InlineData("v1.0.1-alpha+build.5", "1.0.1")]
    public async Task CheckForUpdateAsyncPrereleaseTagUsesNumericVersion(string tagName, string expectedVersion)
    {
        var json = CreateReleaseJson(tagName, "https://github.com/test/releases/tag/prerelease");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal(expectedVersion, latestVersion);
        Assert.Equal("https://github.com/test/releases/tag/prerelease", downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncPrereleaseOfSameVersionReturnsNoUpdate()
    {
        var json = CreateReleaseJson("v1.0.0-beta.2", "https://github.com/test/releases/tag/v1.0.0-beta.2");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncThreeComponentTagIsOlderThanFourComponentCurrentVersion()
    {
        var json = CreateReleaseJson("v1.2.3", "https://github.com/test/releases/tag/v1.2.3");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.2.3.4", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncFourComponentTagRevisionIsNewerThanThreeComponentCurrentVersion()
    {
        var json = CreateReleaseJson("v1.2.3.1", "https://github.com/test/releases/tag/v1.2.3.1");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.2.3", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("1.2.3.1", latestVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsyncEqualVersionWithZeroRevisionReturnsNoUpdate()
    {
        var json = CreateReleaseJson("v1.2.3", "https://github.com/test/releases/tag/v1.2.3");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.2.3.0", _logger.Logger);

        var (_, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
    }

    [Fact]
    public async Task CheckForUpdateAsyncTwoComponentTagIsComparedAsVersion()
    {
        var json = CreateReleaseJson("v1.5", "https://github.com/test/releases/tag/v1.5");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.4", _logger.Logger);

        var (_, isNew, latestVersion, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.Equal("1.5", latestVersion);
    }

    [Theory]
    [InlineData("{invalid-json")]
    [InlineData("[]")]
    [InlineData("\"just a string\"")]
    [InlineData("")]
    public async Task CheckForUpdateAsyncMalformedBodyReturnsNoUpdateWithoutThrowing(string body)
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var exception = await Record.ExceptionAsync(checker.CheckForUpdateAsync);
        Assert.Null(exception);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();
        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Theory]
    [InlineData("{\"html_url\":\"https://example.com/release\"}")]
    [InlineData("{\"tag_name\":\"v2.0.0\"}")]
    [InlineData("{}")]
    public async Task CheckForUpdateAsyncMissingRequiredFieldsReturnsNoUpdate(string body)
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task CheckForUpdateAsyncHttpErrorStatusReturnsNoUpdate(HttpStatusCode statusCode)
    {
        var httpClient = CreateHttpClient(statusCode, "error");
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var exception = await Record.ExceptionAsync(checker.CheckForUpdateAsync);
        Assert.Null(exception);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();
        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncNetworkExceptionIsLoggedAsInformation()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("no network"));
        using var httpClient = new HttpClient(handlerMock.Object);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Failed to check for updates"));
    }

    [Fact]
    public async Task CheckForUpdateAsyncInvalidJsonIsLoggedAsInformation()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK, "{invalid-json");
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Failed to check for updates"));
    }

    [Fact]
    public async Task CheckForUpdateAsyncHttpErrorIsLoggedAsInformation()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not found");
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Failed to check for updates"));
    }

    [Fact]
    public async Task CheckForUpdateAsyncCanceledRequestIsLoggedAsInformation()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("The request timed out"));
        using var httpClient = new HttpClient(handlerMock.Object);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Failed to check for updates"));
    }

    [Fact]
    public async Task CheckForUpdateAsyncHonorsHttpClientTimeout()
    {
        using var httpClient = new HttpClient(new WaitingHandler());
        httpClient.Timeout = TimeSpan.FromMilliseconds(100);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Failed to check for updates"));
    }

    [Fact]
    public async Task CheckForUpdateAsyncSendsGetRequestToGitHubLatestReleaseUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, CreateReleaseJson("v1.0.0", "https://example.com"));
        using var httpClient = new HttpClient(handler);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal(new Uri(GitHubLatestReleaseUrl), handler.LastRequestUri);
    }

    [Fact]
    public async Task CheckForUpdateAsyncSendsUserAgentWithApplicationNameAndVersion()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, CreateReleaseJson("v1.0.0", "https://example.com"));
        using var httpClient = new HttpClient(handler);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.NotNull(handler.LastUserAgent);
        Assert.StartsWith("XISOStudio/1.0.0", handler.LastUserAgent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckForUpdateAsyncPassesCancellableTokenToHandler()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, CreateReleaseJson("v1.0.0", "https://example.com"));
        using var httpClient = new HttpClient(handler);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        await checker.CheckForUpdateAsync();

        Assert.True(handler.LastTokenCanBeCanceled);
    }

    [Fact]
    public async Task CheckForUpdateAsyncSuccessfulCheckDoesNotLogWarning()
    {
        var json = CreateReleaseJson("v2.0.0", "https://github.com/test/releases/tag/v2.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (_, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.True(isNew);
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void ConstructorDoesNotMutateInjectedHttpClientTimeout()
    {
        using var httpClient = new HttpClient();
        var originalTimeout = httpClient.Timeout;

        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        Assert.NotNull(checker);
        Assert.Equal(originalTimeout, httpClient.Timeout);
    }

    [Fact]
    public async Task CheckForUpdateAsyncFailedCheckReportsNotSucceeded()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        using var httpClient = new HttpClient(handlerMock.Object);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, latestVersion, downloadUrl) = await checker.CheckForUpdateAsync();

        // A failed check must not look like "the running version is up to date".
        Assert.False(checkSucceeded);
        Assert.False(isNew);
        Assert.Null(latestVersion);
        Assert.Null(downloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsyncUpToDateReportsSucceededWithoutUpdate()
    {
        var json = CreateReleaseJson("v1.0.0", "https://github.com/test/releases/tag/v1.0.0");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, _, _) = await checker.CheckForUpdateAsync();

        Assert.True(checkSucceeded);
        Assert.False(isNew);
    }

    [Fact]
    public async Task CheckForUpdateAsyncUnparsableTagReportsNotSucceeded()
    {
        var json = CreateReleaseJson("latest-stable", "https://github.com/test/releases/tag/latest-stable");
        var httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        var checker = new UpdateChecker(httpClient, "1.0.0", _logger.Logger);

        var (checkSucceeded, isNew, _, _) = await checker.CheckForUpdateAsync();

        // A release whose tag carries no version cannot confirm the app is up to date.
        Assert.False(checkSucceeded);
        Assert.False(isNew);
    }

    /// <summary>HTTP handler double that records request details and returns a fixed response.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        internal RecordingHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastUserAgent { get; private set; }

        public bool LastTokenCanBeCanceled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastUserAgent = request.Headers.UserAgent.ToString();
            LastTokenCanBeCanceled = cancellationToken.CanBeCanceled;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            });
        }
    }

    /// <summary>HTTP handler double that waits until the caller cancels the request.</summary>
    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}