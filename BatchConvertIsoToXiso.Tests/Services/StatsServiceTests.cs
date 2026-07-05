using System.Net;
using System.Text.Json;
using BatchConvertIsoToXiso.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class StatsServiceTests
{
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content = "")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return new HttpClient(handlerMock.Object);
    }

    private static HttpClient CreateHttpClientThatThrows(Exception exception)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);
        return new HttpClient(handlerMock.Object);
    }

    [Fact]
    public async Task SendStatsAsyncSuccessDoesNotThrow()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.OK);
        var service = new StatsService(httpClient, "https://api.example.com/stats", "test-key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendStatsAsyncServerErrorDoesNotThrow()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.InternalServerError);
        var service = new StatsService(httpClient, "https://api.example.com/stats", "test-key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendStatsAsyncNetworkErrorDoesNotThrow()
    {
        var httpClient = CreateHttpClientThatThrows(new HttpRequestException("Network unreachable"));
        var service = new StatsService(httpClient, "https://api.example.com/stats", "test-key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendStatsAsyncTimeoutDoesNotThrow()
    {
        var httpClient = CreateHttpClientThatThrows(new TaskCanceledException("Timeout"));
        var service = new StatsService(httpClient, "https://api.example.com/stats", "test-key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendStatsAsyncSendsPostRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => { capturedRequest = req; })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handlerMock.Object);

        var service = new StatsService(httpClient, "https://api.example.com/stats", "my-api-key", "TestApp");
        await service.SendStatsAsync();

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal(new Uri("https://api.example.com/stats"), capturedRequest.RequestUri);
    }

    [Fact]
    public async Task SendStatsAsyncSetsAuthorizationHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => { capturedRequest = req; })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handlerMock.Object);

        var service = new StatsService(httpClient, "https://api.example.com/stats", "my-secret-key", "TestApp");
        await service.SendStatsAsync();

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Authorization is not null);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal("my-secret-key", capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task SendStatsAsyncSendsJsonContentType()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => { capturedRequest = req; })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handlerMock.Object);

        var service = new StatsService(httpClient, "https://api.example.com/stats", "key", "MyApp");
        await service.SendStatsAsync();

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.Content);
        Assert.Equal("application/json", capturedRequest.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task SendStatsAsyncSendsPayloadWithApplicationIdAndVersion()
    {
        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async void (req, _) =>
            {
                try
                {
                    if (req.Content != null)
                    {
                        capturedBody = await req.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception)
                {
                    // Ignore
                }
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handlerMock.Object);

        var service = new StatsService(httpClient, "https://api.example.com/stats", "key", "BatchConvertIsoToXiso");
        await service.SendStatsAsync();

        Assert.NotNull(capturedBody);
        var json = JsonDocument.Parse(capturedBody);
        Assert.True(json.RootElement.TryGetProperty("applicationId", out var appId));
        Assert.Equal("BatchConvertIsoToXiso", appId.GetString());
        Assert.True(json.RootElement.TryGetProperty("version", out _));
    }

    [Fact]
    public async Task SendStatsAsyncBadRequestDoesNotThrow()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.BadRequest, "Bad request");
        var service = new StatsService(httpClient, "https://api.example.com/stats", "key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }

    [Fact]
    public async Task SendStatsAsyncForbiddenDoesNotThrow()
    {
        var httpClient = CreateHttpClient(HttpStatusCode.Forbidden, "Forbidden");
        var service = new StatsService(httpClient, "https://api.example.com/stats", "key", "TestApp");

        var exception = await Record.ExceptionAsync(service.SendStatsAsync);
        Assert.Null(exception);
    }
}
