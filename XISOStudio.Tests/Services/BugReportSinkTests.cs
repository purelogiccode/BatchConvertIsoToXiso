using XISOStudio.Interfaces;
using XISOStudio.Services;
using Moq;
using Serilog;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

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
}