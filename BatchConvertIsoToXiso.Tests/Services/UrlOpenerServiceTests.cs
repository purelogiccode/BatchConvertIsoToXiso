using BatchConvertIsoToXiso.Services;
using Serilog.Events;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

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
        Assert.True(_logger.HasMessage(LogEventLevel.Error, "Error opening URL"));
    }
}