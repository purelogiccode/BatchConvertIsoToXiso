using BatchConvertIsoToXiso.Services;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

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
}