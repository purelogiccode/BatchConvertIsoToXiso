using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests the default values and settable properties of <c>BatchOperationProgress</c>.</summary>
public class BatchOperationProgressTests
{
    [Fact]
    public void DefaultValuesAreNullOrDefault()
    {
        var progress = new BatchOperationProgress();

        Assert.Null(progress.LogMessage);
        Assert.Null(progress.StatusText);
        Assert.Null(progress.TotalFiles);
        Assert.Null(progress.ProcessedCount);
        Assert.Null(progress.SuccessCount);
        Assert.Null(progress.FailedCount);
        Assert.Null(progress.SkippedCount);
        Assert.Null(progress.CurrentDrive);
        Assert.Null(progress.FailedPathToAdd);
    }

    [Fact]
    public void PropertiesCanBeSet()
    {
        var progress = new BatchOperationProgress
        {
            LogMessage = "Test log",
            StatusText = "Testing...",
            TotalFiles = 10,
            ProcessedCount = 5,
            SuccessCount = 4,
            FailedCount = 1,
            SkippedCount = 0,
            CurrentDrive = "C:",
            FailedPathToAdd = "C:\\failed.iso"
        };

        Assert.Equal("Test log", progress.LogMessage);
        Assert.Equal("Testing...", progress.StatusText);
        Assert.Equal(10, progress.TotalFiles);
        Assert.Equal(5, progress.ProcessedCount);
        Assert.Equal(4, progress.SuccessCount);
        Assert.Equal(1, progress.FailedCount);
        Assert.Equal(0, progress.SkippedCount);
        Assert.Equal("C:", progress.CurrentDrive);
        Assert.Equal("C:\\failed.iso", progress.FailedPathToAdd);
    }

    [Fact]
    public void DefaultInvalidIsoCountIsNull()
    {
        var progress = new BatchOperationProgress();

        Assert.Null(progress.InvalidIsoCount);
    }

    [Fact]
    public void InvalidIsoCountCanBeSet()
    {
        var progress = new BatchOperationProgress { InvalidIsoCount = 3 };

        Assert.Equal(3, progress.InvalidIsoCount);
    }

    [Fact]
    public void AllCountersCanBeSetToZero()
    {
        var progress = new BatchOperationProgress
        {
            TotalFiles = 0,
            ProcessedCount = 0,
            SuccessCount = 0,
            FailedCount = 0,
            SkippedCount = 0,
            InvalidIsoCount = 0
        };

        Assert.Equal(0, progress.TotalFiles);
        Assert.Equal(0, progress.ProcessedCount);
        Assert.Equal(0, progress.SuccessCount);
        Assert.Equal(0, progress.FailedCount);
        Assert.Equal(0, progress.SkippedCount);
        Assert.Equal(0, progress.InvalidIsoCount);
    }

    [Fact]
    public void CountersCanBeSetIndependently()
    {
        var progress = new BatchOperationProgress { TotalFiles = 10 };

        Assert.Equal(10, progress.TotalFiles);
        Assert.Null(progress.ProcessedCount);
        Assert.Null(progress.SuccessCount);
        Assert.Null(progress.FailedCount);
        Assert.Null(progress.SkippedCount);
        Assert.Null(progress.InvalidIsoCount);
    }

    [Fact]
    public void CountersCanBeResetToNull()
    {
        var progress = new BatchOperationProgress
        {
            TotalFiles = 10,
            ProcessedCount = 9,
            InvalidIsoCount = 1
        };

        progress.TotalFiles = null;
        progress.ProcessedCount = null;
        progress.InvalidIsoCount = null;

        Assert.Null(progress.TotalFiles);
        Assert.Null(progress.ProcessedCount);
        Assert.Null(progress.InvalidIsoCount);
    }

    [Fact]
    public void StringPropertiesCanBeResetToNull()
    {
        var progress = new BatchOperationProgress
        {
            LogMessage = "log",
            StatusText = "status",
            CurrentDrive = "D:",
            FailedPathToAdd = "D:\\failed.iso"
        };

        progress.LogMessage = null;
        progress.StatusText = null;
        progress.CurrentDrive = null;
        progress.FailedPathToAdd = null;

        Assert.Null(progress.LogMessage);
        Assert.Null(progress.StatusText);
        Assert.Null(progress.CurrentDrive);
        Assert.Null(progress.FailedPathToAdd);
    }
}