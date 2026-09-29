using System.Diagnostics.CodeAnalysis;
using System.Net;
using XboxIsoStudio.Services;
using Serilog.Events;
using Xunit;

namespace XboxIsoStudio.Tests.Services;

public class BugReportServiceTests
{
    [Fact]
    public void BuildFullMessageMessageAlreadyContainsEnvironmentDetailsReturnsUnchanged()
    {
        const string message = "=== Environment Details ===\nSome existing details\nMore info";
        var result = BugReportService.BuildFullMessage(message);
        Assert.Equal(message, result);
    }

    [Fact]
    public void BuildFullMessageCaseInsensitiveReturnsUnchanged()
    {
        const string message = "=== environment details ===\nSome existing details";
        var result = BugReportService.BuildFullMessage(message);
        Assert.Equal(message, result);
    }

    [Fact]
    public void BuildFullMessageSimpleMessageContainsExpectedEnvironmentSections()
    {
        const string message = "Test bug report message";
        var result = BugReportService.BuildFullMessage(message);

        Assert.Contains("=== Environment Details ===", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Date:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Application Name:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Application Version:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OS Version:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Architecture:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bitness:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows Version:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Processor Count:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Base Directory:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Temp Path:", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(message, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFullMessageSimpleMessageContainsErrorDetailsSection()
    {
        const string message = "Test bug report message";
        var result = BugReportService.BuildFullMessage(message);

        Assert.Contains($"=== Error Details ==={Environment.NewLine}{message}", result,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFullMessageCreatesValidFormattedOutput()
    {
        const string message = "Something broke!";
        var result = BugReportService.BuildFullMessage(message);

        Assert.StartsWith("=== Environment Details ===", result, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ConstructorInitializesWithValidParameters()
    {
        using var httpClient = new HttpClient();
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp");
        Assert.NotNull(service);
    }

    [Fact]
    public void ConstructorWithDisposeDoesNotThrow()
    {
        using var httpClient = new HttpClient();
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp");
        Assert.NotNull(service);
    }

    [Fact]
    public void ConstructorDoesNotMutateInjectedHttpClient()
    {
        using var httpClient = new HttpClient();
        var originalTimeout = httpClient.Timeout;

        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp");

        Assert.NotNull(service);
        Assert.Equal(originalTimeout, httpClient.Timeout);
        Assert.False(httpClient.DefaultRequestHeaders.Contains("X-API-KEY"));
    }

    [Fact]
    public async Task SendBugReportAsyncSendsApiKeyHeaderPerRequest()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp");

        var sent = await service.SendBugReportAsync("test report");

        Assert.True(sent);
        Assert.Equal("test-key", handler.LastApiKey);
    }

    [Fact]
    public async Task SendBugReportAsyncNonSuccessResponseIsLogged()
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new CapturingHandler(HttpStatusCode.InternalServerError));
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp",
            logger.Logger);

        var sent = await service.SendBugReportAsync("test report");

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Bug report API returned"));
    }

    [Fact]
    public async Task SendBugReportAsyncTransportFailureIsLogged()
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new ThrowingHandler());
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp",
            logger.Logger);

        var sent = await service.SendBugReportAsync("test report");

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Failed to send bug report"));
    }

    [Fact]
    [SuppressMessage("xUnit", "xUnit1031",
        Justification = "The blocking wait is the scenario under test: it simulates the fatal-error path.")]
    public void SendBugReportAsyncCompletesWhenSynchronizationContextDoesNotPump()
    {
        // Simulates the fatal-error path: the UI thread blocks in Wait() while its
        // synchronization context never runs posted continuations. Without
        // ConfigureAwait(false) the report task would never complete (and never be sent).
        using var httpClient = new HttpClient(new DelayingHandler(TimeSpan.FromMilliseconds(50)));
        var service = new BugReportService(httpClient, "https://api.example.com", "test-key", "TestApp");

        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            var task = service.SendBugReportAsync("test report");

            Assert.True(task.Wait(TimeSpan.FromSeconds(5)), "Bug report did not complete within the timeout");
            Assert.True(task.Result);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Intentionally drop the callback: the "UI thread" is blocked.
        }
    }

    private sealed class DelayingHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        public DelayingHandler(TimeSpan delay)
        {
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public CapturingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastApiKey = request.Headers.TryGetValues("X-API-KEY", out var values)
                ? values.FirstOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("network down");
        }
    }
}
