using XISOStudio.Services;
using Xunit;

namespace XISOStudio.Tests.Services;

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

        Assert.True(logger.HasMessage("Welcome to 'XISO Studio'."));
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
        Assert.True(logger.HasMessage("CHD"));
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
    public void DisplayInitialInstructionsLogsEngineStatus()
    {
        var logger = new TestLogger();
        DisplayInstructions.Initialize(logger.Logger);

        DisplayInstructions.DisplayInitialInstructions();

        Assert.True(logger.HasMessage("XISOSharp"));
        Assert.True(logger.HasMessage("CHDSharp"));
    }
}