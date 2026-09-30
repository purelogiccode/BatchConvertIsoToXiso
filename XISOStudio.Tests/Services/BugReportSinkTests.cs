using System.Globalization;
using XISOStudio.Interfaces;
using XISOStudio.Services;
using Moq;
using Serilog;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>Tests that the bug report sink forwards warning and error events to the bug report service.</summary>
public class BugReportSinkTests
{
    private static ILogger CreateLogger(IBugReportService? bugReportService, LogEventLevel minimumLevel)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(new BugReportSink(() => bugReportService, minimumLevel))
            .CreateLogger();
    }

    private static async Task<bool> WaitForAsync(TaskCompletionSource<bool> tcs)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        return completed == tcs.Task;
    }

    [Fact]
    public async Task WarningEventIsForwardedWithoutException()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);

        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);
        logger.Warning("Something suspicious happened");

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
        bugReport.Verify(static b => b.SendBugReportAsync(It.Is<string>(m => m.Contains("suspicious"))), Times.Once);
    }

    [Fact]
    public async Task ErrorEventWithExceptionIsForwardedWithException()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>(), It.IsAny<Exception>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);

        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);
        logger.Error(new InvalidOperationException("boom"), "Conversion failed");

        Assert.True(await WaitForAsync(tcs), "The error was not forwarded to the bug report service");
        bugReport.Verify(
            static b => b.SendBugReportAsync(
                It.Is<string>(m => m.Contains("Conversion failed")),
                It.Is<Exception>(e => e.Message == "boom")),
            Times.Once);
    }

    [Fact]
    public async Task InformationEventIsNotForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);

        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);
        logger.Information("Just a normal progress message");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
        Assert.NotSame(tcs.Task, completed);
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void NullServiceDoesNotThrow()
    {
        var logger = CreateLogger(null, LogEventLevel.Warning);

        var exception = Record.Exception(() => logger.Warning("No service available yet"));

        Assert.Null(exception);
    }

    [Fact]
    public void ThrowingServiceAccessorDoesNotThrow()
    {
        var sink = new BugReportSink(() => throw new InvalidOperationException("accessor failed"));
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var exception = Record.Exception(() => logger.Warning("Accessor failure"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task ServiceFailureDoesNotThrow()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ThrowsAsync(new HttpRequestException("network down"));

        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        var exception = Record.Exception(() => logger.Warning("This will fail to send"));
        Assert.Null(exception);

        // The sink's fire-and-forget task must swallow the failure.
        Assert.True(await WaitForAsync(tcs));
    }

    [Fact]
    public void AvaloniaSourceContextIsNotForwarded()
    {
        var bugReport = new Mock<IBugReportService>();
        var (logger, accessorCalls) = CreateCountingLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", "Avalonia").Warning("Framework warning");

        Assert.Equal(0, accessorCalls());
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void AvaloniaPrefixedSourceContextIsNotForwarded()
    {
        var bugReport = new Mock<IBugReportService>();
        var (logger, accessorCalls) = CreateCountingLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", "Avalonia.Controls.Grid").Warning("Framework warning");

        Assert.Equal(0, accessorCalls());
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void AvaloniaSourceContextErrorWithExceptionIsNotForwarded()
    {
        var bugReport = new Mock<IBugReportService>();
        var (logger, accessorCalls) = CreateCountingLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", "Avalonia.Diagnostics")
            .Error(new InvalidOperationException("framework failure"), "Framework error");

        Assert.Equal(0, accessorCalls());
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
    }

    [Fact]
    public async Task NonAvaloniaSourceContextIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", "XISOStudio.Services.OrchestratorService")
            .Warning("Application warning");

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
    }

    [Fact]
    public async Task LowercaseAvaloniaSourceContextIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", "avalonia").Warning("Application warning");

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
    }

    [Fact]
    public async Task MissingSourceContextIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        logger.Warning("Application warning");

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
    }

    [Fact]
    public async Task NonStringSourceContextIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        logger.ForContext("SourceContext", 42).Warning("Application warning");

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
    }

    [Fact]
    public void WarningBelowErrorMinimumIsNotForwarded()
    {
        var bugReport = new Mock<IBugReportService>();
        var (logger, accessorCalls) = CreateCountingLogger(bugReport.Object, LogEventLevel.Error);

        logger.Warning("Ignored warning");

        Assert.Equal(0, accessorCalls());
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ErrorAtErrorMinimumIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Error);

        logger.Error("Forwarded error");

        Assert.True(await WaitForAsync(tcs), "The error was not forwarded to the bug report service");
        bugReport.Verify(
            static b => b.SendBugReportAsync(It.Is<string>(m => m.Contains("Forwarded error"))),
            Times.Once);
    }

    [Fact]
    public async Task InformationAtVerboseMinimumIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Verbose);

        logger.Information("Forwarded information");

        Assert.True(await WaitForAsync(tcs), "The information message was not forwarded to the bug report service");
    }

    [Fact]
    public async Task FatalEventIsForwarded()
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => tcs.TrySetResult(true))
            .ReturnsAsync(true);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        logger.Fatal("Fatal failure");

        Assert.True(await WaitForAsync(tcs), "The fatal event was not forwarded to the bug report service");
        bugReport.Verify(
            static b => b.SendBugReportAsync(It.Is<string>(m => m.Contains("Fatal failure"))),
            Times.Once);
    }

    [Theory]
    [InlineData("fr-FR", "1,5")]
    [InlineData("en-US", "1.5")]
    public async Task RenderedMessageUsesFormatProviderCulture(string cultureName, string expectedValue)
    {
        var received = new List<string>();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback<string>(m =>
            {
                received.Add(m);
                tcs.TrySetResult(true);
            })
            .ReturnsAsync(true);
        var (logger, _) = CreateCountingLogger(
            bugReport.Object, LogEventLevel.Warning, CultureInfo.GetCultureInfo(cultureName));

        logger.Warning("Temperature {Value:0.0}", 1.5);

        Assert.True(await WaitForAsync(tcs), "The warning was not forwarded to the bug report service");
        Assert.Contains(received, m => m.Contains(expectedValue, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SlowSendTaskDoesNotBlockLoggingAndCompletes()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() => started.TrySetResult(true))
            .Returns(release.Task);
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        var exception = Record.Exception(() => logger.Warning("Slow report"));

        Assert.Null(exception);
        Assert.True(await WaitForAsync(started), "The sink never invoked the bug report service");
        Assert.False(release.Task.IsCompleted);

        release.SetResult(true);
        Assert.True(await WaitForAsync(release), "The send task did not complete");
        bugReport.Verify(static b => b.SendBugReportAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SynchronousServiceExceptionIsSwallowed()
    {
        var invoked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bugReport = new Mock<IBugReportService>();
        bugReport.Setup(static b => b.SendBugReportAsync(It.IsAny<string>()))
            .Callback(() =>
            {
                invoked.TrySetResult(true);
                throw new InvalidOperationException("synchronous failure");
            });
        var logger = CreateLogger(bugReport.Object, LogEventLevel.Warning);

        var exception = Record.Exception(() => logger.Warning("Sync throwing service"));

        Assert.Null(exception);
        Assert.True(await WaitForAsync(invoked), "The sink never invoked the bug report service");
    }

    private static (ILogger Logger, Func<int> AccessorCalls) CreateCountingLogger(
        IBugReportService? bugReportService, LogEventLevel minimumLevel, IFormatProvider? formatProvider = null)
    {
        var calls = 0;
        var sink = new BugReportSink(
            () =>
            {
                calls++;
                return bugReportService;
            },
            minimumLevel,
            formatProvider);
        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        return (logger, () => calls);
    }
}