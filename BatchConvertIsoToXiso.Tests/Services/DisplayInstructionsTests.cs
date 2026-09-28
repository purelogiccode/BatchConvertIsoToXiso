using BatchConvertIsoToXiso.Services;
using Xunit;

namespace BatchConvertIsoToXiso.Tests.Services;

public class DisplayInstructionsTests
{
    [Fact]
    public void DisplayInitialInstructionsWhenNotInitializedDoesNotThrow()
    {
        var exception = Record.Exception(DisplayInstructions.DisplayInitialInstructions);
        Assert.Null(exception);
    }

    [Fact]
    public void DisplayInitialInstructionsWhenInitializedLogsWelcomeMessage()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("Welcome to 'Batch Convert ISO to XISO'."));
    }

    [Fact]
    public void DisplayInitialInstructionsLogsApplicationFunctions()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("This application provides"));
        Assert.True(logger.HasMessage("Convert"));
        Assert.True(logger.HasMessage("Test Integrity"));
        Assert.True(logger.HasMessage("Explorer"));
    }

    [Fact]
    public void DisplayInitialInstructionsLogsPlatformWarning()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("IMPORTANT: This tool ONLY works with Xbox"));
    }

    [Fact]
    public void DisplayInitialInstructionsLogsReadyMessage()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("--- Ready ---"));
    }

    [Fact]
    public void DisplayInitialInstructionsLogsXisoSharpStatus()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("XISOSharp"));
    }
}