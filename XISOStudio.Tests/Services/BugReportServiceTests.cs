using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using XISOStudio.Services;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests bug report message construction and sending behavior of <c>BugReportService</c>.</summary>
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
        Assert.Contains($"{BugReportService.GetPlatformVersionLabel()}:", result, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void BuildFullMessageContainsDateLineWithInvariantFormat()
    {
        var result = BugReportService.BuildFullMessage("message");

        var match = Regex.Match(result, @"Date: \d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} [+-]\d{2}:\d{2}",
            RegexOptions.NonBacktracking);
        Assert.True(match.Success, $"No formatted date line was found in: {result}");
    }

    [Fact]
    public void BuildFullMessageContainsApplicationNameLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Application Name: {App.ApplicationName}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsApplicationVersionLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Application Version: {GetApplicationVersion.GetProgramVersion()}", result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsOsVersionLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"OS Version: {Environment.OSVersion}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsArchitectureLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Architecture: {RuntimeInformation.ProcessArchitecture}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsBitnessLineMatchingProcessBitness()
    {
        var result = BugReportService.BuildFullMessage("message");
        var expectedBitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";

        Assert.Contains($"Bitness: {expectedBitness}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsPlatformVersionLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"{BugReportService.GetPlatformVersionLabel()}: ", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsProcessorCountLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Processor Count: {Environment.ProcessorCount}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsBaseDirectoryLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Base Directory: {AppContext.BaseDirectory}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageContainsTempPathLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.Contains($"Temp Path: {Path.GetTempPath()}", result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessagePlacesEnvironmentDetailsBeforeErrorDetailsAndMessage()
    {
        const string message = "unique-ordering-marker";
        var result = BugReportService.BuildFullMessage(message);

        var environmentIndex = result.IndexOf("=== Environment Details ===", StringComparison.Ordinal);
        var errorIndex = result.IndexOf("=== Error Details ===", StringComparison.Ordinal);
        var messageIndex = result.IndexOf(message, StringComparison.Ordinal);

        Assert.True(environmentIndex >= 0);
        Assert.True(errorIndex > environmentIndex);
        Assert.True(messageIndex > errorIndex);
    }

    [Fact]
    public void BuildFullMessageEndsWithNewLine()
    {
        var result = BugReportService.BuildFullMessage("message");

        Assert.EndsWith(Environment.NewLine, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageEmptyMessageStillContainsErrorDetailsSection()
    {
        var result = BugReportService.BuildFullMessage(string.Empty);

        Assert.Contains("=== Error Details ===", result, StringComparison.Ordinal);
        Assert.EndsWith($"=== Error Details ==={Environment.NewLine}{Environment.NewLine}", result,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageWhitespaceMessageIsPreserved()
    {
        const string message = "   ";
        var result = BugReportService.BuildFullMessage(message);

        Assert.EndsWith(message + Environment.NewLine, result, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFullMessageNullMessageThrowsNullReferenceException()
    {
        Assert.Throws<NullReferenceException>(() => BugReportService.BuildFullMessage(null!));
    }

    [Fact]
    public void GetPlatformVersionLabelMatchesCurrentOperatingSystem()
    {
        var label = BugReportService.GetPlatformVersionLabel();

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("Windows Version", label);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("MacOsX Version", label);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("Linux Version", label);
        }
        else
        {
            Assert.Equal("OS Platform Version", label);
        }
    }

    [Fact]
    public void GetPlatformVersionLabelIsNotBlank()
    {
        Assert.False(string.IsNullOrWhiteSpace(BugReportService.GetPlatformVersionLabel()));
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionIncludesExceptionDetailsSection()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        var sent = await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.True(sent);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("=== Exception Details ===", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionIncludesExceptionTypeAndMessage()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.NotNull(handler.LastBody);
        Assert.Contains(typeof(InvalidOperationException).FullName!, handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("boom", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionIncludesInnerExceptionDetails()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.NotNull(handler.LastBody);
        Assert.Contains("Inner Exception:", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains(typeof(ArgumentException).FullName!, handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("inner-boom", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionIncludesAggregateExceptionMembers()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");
        var aggregate = new AggregateException(new InvalidOperationException("first-failure"),
            new ArgumentException("second-failure"));

        var sent = await service.SendBugReportAsync("failure", aggregate);

        Assert.True(sent);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("Inner Exception [0]:", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("Inner Exception [1]:", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("first-failure", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("second-failure", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionReturnsTrueOnSuccess()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        var sent = await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.True(sent);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionPayloadContainsAllApiFields()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");
        var exception = CreateThrownException();

        await service.SendBugReportAsync("failure", exception);

        var payload = ParsePayload(handler.LastBody!);
        Assert.True(payload.TryGetProperty("message", out var message));
        Assert.Contains("=== Exception Details ===", message.GetString(), StringComparison.Ordinal);
        Assert.Equal("TestApp", payload.GetProperty("applicationName").GetString());
        Assert.Equal(GetApplicationVersion.GetProgramVersion(), payload.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("environment").GetString()));
        Assert.Equal(exception.StackTrace, payload.GetProperty("stackTrace").GetString());
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionPayloadStackTraceMatchesException()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");
        var exception = CreateThrownException();

        await service.SendBugReportAsync("failure", exception);

        var payload = ParsePayload(handler.LastBody!);
        var stackTrace = payload.GetProperty("stackTrace").GetString();
        Assert.False(string.IsNullOrWhiteSpace(stackTrace));
        Assert.Equal(exception.StackTrace, stackTrace);
    }

    [Fact]
    public async Task SendBugReportAsyncWithoutExceptionPayloadContainsCoreFields()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        var payload = ParsePayload(handler.LastBody!);
        Assert.Equal(BugReportService.BuildFullMessage("plain report"), payload.GetProperty("message").GetString());
        Assert.Equal("TestApp", payload.GetProperty("applicationName").GetString());
        Assert.Equal(GetApplicationVersion.GetProgramVersion(), payload.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("environment").GetString()));
    }

    [Fact]
    public async Task SendBugReportAsyncWithoutExceptionStackTraceFieldIsNullOrAbsent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        var payload = ParsePayload(handler.LastBody!);
        var hasStackTrace = payload.TryGetProperty("stackTrace", out var stackTrace);
        Assert.True(!hasStackTrace || stackTrace.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task SendBugReportAsyncPayloadEnvironmentSummaryFitsApiLimit()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        var payload = ParsePayload(handler.LastBody!);
        var environment = payload.GetProperty("environment").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(environment));
        Assert.True(environment.Length <= 50, $"Environment summary was {environment.Length} characters long.");
        var bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
        Assert.Contains(bitness, environment, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncPayloadVersionComesFromGetApplicationVersion()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        var payload = ParsePayload(handler.LastBody!);
        Assert.Equal(GetApplicationVersion.GetProgramVersion(), payload.GetProperty("version").GetString());
    }

    [Fact]
    public async Task SendBugReportAsyncPayloadApplicationNameMatchesConstructorArgument()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "CustomAppName");

        await service.SendBugReportAsync("plain report");

        var payload = ParsePayload(handler.LastBody!);
        Assert.Equal("CustomAppName", payload.GetProperty("applicationName").GetString());
    }

    [Fact]
    public async Task SendBugReportAsyncPostsToConfiguredUrl()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        const string url = "https://bugreport.example.org/api/send";
        var service = new BugReportService(httpClient, url, "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal(new Uri(url), handler.LastRequestUri);
    }

    [Fact]
    public async Task SendBugReportAsyncSendsJsonContentType()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        Assert.Equal("application/json", handler.LastMediaType);
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionSendsApiKeyHeaderPerRequest()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "secret-key", "TestApp");

        await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.Equal("secret-key", handler.LastApiKey);
    }

    [Fact]
    public async Task SendBugReportAsyncPassesCancellableTimeoutTokenToHandler()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await service.SendBugReportAsync("plain report");

        Assert.True(handler.LastTokenCanBeCanceled);
    }

    [Fact]
    public async Task SendBugReportAsyncEmptyMessageStillSendsEnvironmentDetails()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        var sent = await service.SendBugReportAsync(string.Empty);

        Assert.True(sent);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("=== Environment Details ===", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("=== Error Details ===", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBugReportAsyncNullMessageThrowsNullReferenceException()
    {
        using var httpClient = new HttpClient(new RecordingHandler(HttpStatusCode.OK));
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        // The failure must surface through the returned task, not synchronously from the call.
        await Assert.ThrowsAsync<NullReferenceException>(() => service.SendBugReportAsync(null!));
    }

    [Fact]
    public async Task SendBugReportAsyncNullMessageWithExceptionThrowsNullReferenceException()
    {
        using var httpClient = new HttpClient(new RecordingHandler(HttpStatusCode.OK));
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp");

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            service.SendBugReportAsync(null!, new InvalidOperationException("boom")));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task SendBugReportAsyncNonSuccessStatusReturnsFalseAndLogs(HttpStatusCode statusCode)
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new RecordingHandler(statusCode));
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp", logger.Logger);

        var sent = await service.SendBugReportAsync("test report");

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Bug report API returned"));
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionNonSuccessReturnsFalseAndLogs()
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new RecordingHandler(HttpStatusCode.InternalServerError));
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp", logger.Logger);

        var sent = await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Bug report API returned"));
    }

    [Fact]
    public async Task SendBugReportAsyncCanceledRequestReturnsFalseAndLogs()
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new CanceledHandler());
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp", logger.Logger);

        var sent = await service.SendBugReportAsync("test report");

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Failed to send bug report"));
    }

    [Fact]
    public async Task SendBugReportAsyncWithExceptionTransportFailureReturnsFalseAndLogs()
    {
        var logger = new TestLogger();
        using var httpClient = new HttpClient(new ThrowingHandler());
        var service = new BugReportService(httpClient, "https://api.example.com", "key", "TestApp", logger.Logger);

        var sent = await service.SendBugReportAsync("failure", CreateThrownException());

        Assert.False(sent);
        Assert.True(logger.HasMessage(LogEventLevel.Information, "Failed to send bug report"));
    }

    private static Exception CreateThrownException()
    {
        try
        {
            throw new InvalidOperationException("boom", new ArgumentException("inner-boom"));
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static JsonElement ParsePayload(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    /// <summary>Synchronization context double that never runs posted continuations, simulating a blocked UI thread.</summary>
    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // Intentionally drop the callback: the "UI thread" is blocked.
        }
    }

    /// <summary>HTTP handler double that delays the response by a fixed interval.</summary>
    private sealed class DelayingHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;

        internal DelayingHandler(TimeSpan delay)
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

    /// <summary>HTTP handler double that captures the API key header and returns a fixed status code.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        internal CapturingHandler(HttpStatusCode statusCode)
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

    /// <summary>HTTP handler double that captures request details and returns a fixed status code.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        internal RecordingHandler(HttpStatusCode statusCode, string responseContent = "")
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        public string? LastBody { get; private set; }

        public string? LastApiKey { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public Uri? LastRequestUri { get; private set; }

        public string? LastMediaType { get; private set; }

        public bool LastTokenCanBeCanceled { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastApiKey = request.Headers.TryGetValues("X-API-KEY", out var values)
                ? values.FirstOrDefault()
                : null;
            LastMediaType = request.Content?.Headers.ContentType?.MediaType;
            LastTokenCanBeCanceled = cancellationToken.CanBeCanceled;
            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
        }
    }

    /// <summary>HTTP handler double that always throws an <c>HttpRequestException</c>.</summary>
    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("network down");
        }
    }

    /// <summary>HTTP handler double that always fails the request with a cancellation exception.</summary>
    private sealed class CanceledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException("the request timed out"));
        }
    }
}