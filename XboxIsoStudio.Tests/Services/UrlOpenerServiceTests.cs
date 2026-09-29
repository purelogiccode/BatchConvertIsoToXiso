using XboxIsoStudio.Services;
using Serilog.Events;
using Xunit;

namespace XboxIsoStudio.Tests.Services;

public class UrlOpenerServiceTests
{
    private readonly TestLogger _logger = new();

    [Fact]
    public void ConstructorInitializesProperties()
    {
        var service = new UrlOpenerService(_logger.Logger);
        Assert.NotNull(service);
    }

    [Fact]
    public void OpenUrlWithInvalidUrlThrowsAndLogs()
    {
        var service = new UrlOpenerService(_logger.Logger);

        var ex = Record.Exception(() => service.OpenUrl("not_a_valid_url"));

        Assert.NotNull(ex);
        // A failed browser launch is environmental and must not be uploaded as a bug report.
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Error opening URL"));
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }
}