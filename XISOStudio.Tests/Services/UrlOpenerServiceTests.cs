using System.ComponentModel;
using System.Diagnostics;
using XISOStudio.Interfaces;
using XISOStudio.Services;
using Serilog.Events;
using Xunit;

namespace XISOStudio.Tests.Services;

/// <summary>
/// Tests URL opening and failure logging in <c>UrlOpenerService</c>. A fake process starter is
/// injected so unit tests never invoke the real OS shell, which would show an "open with" picker
/// for unregistered schemes.
/// </summary>
public class UrlOpenerServiceTests
{
    private readonly TestLogger _logger = new();

    private UrlOpenerService CreateService(Func<ProcessStartInfo, Process?>? startProcess = null)
    {
        return new UrlOpenerService(_logger.Logger, startProcess ?? (static _ => null));
    }

    [Fact]
    public void ConstructorInitializesProperties()
    {
        var service = new UrlOpenerService(_logger.Logger);
        Assert.NotNull(service);
    }

    [Fact]
    public void ConstructorCreatesServiceImplementingIUrlOpener()
    {
        var service = new UrlOpenerService(_logger.Logger);
        Assert.IsType<IUrlOpener>(service, exactMatch: false);
    }

    [Fact]
    public void OpenUrlForwardsUrlToProcessStarter()
    {
        ProcessStartInfo? captured = null;
        var service = CreateService(info =>
        {
            captured = info;
            return null;
        });

        service.OpenUrl("https://example.com");

        Assert.NotNull(captured);
        Assert.Equal("https://example.com", captured.FileName);
        Assert.True(captured.UseShellExecute);
    }

    [Fact]
    public void OpenUrlWithSuccessfulStarterDoesNotThrow()
    {
        var service = CreateService();

        var exception = Record.Exception(() => service.OpenUrl("https://example.com"));

        Assert.Null(exception);
    }

    [Fact]
    public void OpenUrlWithSuccessfulStarterDoesNotLogWarningOrError()
    {
        var service = CreateService();

        service.OpenUrl("https://example.com");

        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void OpenUrlWithNullProcessResultDoesNotThrow()
    {
        var service = CreateService(static _ => null);

        var exception = Record.Exception(() => service.OpenUrl("https://example.com"));

        Assert.Null(exception);
    }

    [Fact]
    public void OpenUrlWhenStarterThrowsLogsInformationAndRethrows()
    {
        var failure = new Win32Exception("The system cannot find the file specified.");
        var service = CreateService(_ => throw failure);

        var ex = Record.Exception(() => service.OpenUrl("not_a_valid_url"));

        Assert.Same(failure, ex);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Error opening URL"));
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void OpenUrlFailureLogsTheRequestedUrl()
    {
        const string url = "not_a_valid_url_for-logging";
        var service = CreateService(static _ => throw new Win32Exception("No application is associated"));

        _ = Record.Exception(() => service.OpenUrl(url));

        Assert.True(_logger.HasMessage(LogEventLevel.Information, url));
    }

    [Fact]
    public void OpenUrlRepeatedFailureThrowsSameExceptionType()
    {
        var service = CreateService(static _ => throw new Win32Exception("No application is associated"));

        var first = Record.Exception(() => service.OpenUrl("not_a_valid_url"));
        var second = Record.Exception(() => service.OpenUrl("not_a_valid_url"));

        Assert.IsType<Win32Exception>(first);
        Assert.IsType<Win32Exception>(second);
    }

    [Fact]
    public void OpenUrlInvokesStarterOncePerCall()
    {
        var calls = 0;
        var service = CreateService(_ =>
        {
            calls++;
            return null;
        });

        service.OpenUrl("https://example.com");
        service.OpenUrl("https://example.com");

        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenUrlWithNullOrBlankUrlPropagatesStarterFailure(string? url)
    {
        var service = CreateService(static _ => throw new ArgumentException("Invalid URL"));

        var ex = Record.Exception(() => service.OpenUrl(url!));

        Assert.IsType<ArgumentException>(ex);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Error opening URL"));
    }

    [Fact]
    public void OpenUrlWithUnsupportedSchemeLogsInformationAndRethrows()
    {
        var failure = new Win32Exception("No application is associated with the specified file for this operation.");
        var service = CreateService(_ => throw failure);

        var ex = Record.Exception(() => service.OpenUrl("xisostudio-no-handler-7f3a://open"));

        Assert.Same(failure, ex);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Error opening URL"));
        Assert.DoesNotContain(_logger.Events, e => e.Level >= LogEventLevel.Warning);
    }

    [Fact]
    public void OpenUrlWithLongFileNamePropagatesStarterFailure()
    {
        var service = CreateService(static _ => throw new PathTooLongException("too long"));

        var ex = Record.Exception(() => service.OpenUrl(new string('x', 5000)));

        Assert.IsType<PathTooLongException>(ex);
        Assert.True(_logger.HasMessage(LogEventLevel.Information, "Error opening URL"));
    }
}