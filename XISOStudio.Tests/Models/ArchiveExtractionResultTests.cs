using XISOStudio.Models;
using Xunit;

namespace XISOStudio.Tests.Models;

/// <summary>Tests construction, the <c>Failed</c> singleton, and value equality of <c>ArchiveExtractionResult</c>.</summary>
public class ArchiveExtractionResultTests
{
    [Fact]
    public void ConstructorSetsProperties()
    {
        var result = new ArchiveExtractionResult(true, ["extra.iso"]);

        Assert.True(result.Success);
        Assert.Equal(["extra.iso"], result.SkippedEntries);
    }

    [Fact]
    public void SuccessfulResultWithNoSkippedEntries()
    {
        var result = new ArchiveExtractionResult(true, []);

        Assert.True(result.Success);
        Assert.Empty(result.SkippedEntries);
    }

    [Fact]
    public void FailedStaticReportsFailureWithoutSkippedEntries()
    {
        Assert.False(ArchiveExtractionResult.Failed.Success);
        Assert.NotNull(ArchiveExtractionResult.Failed.SkippedEntries);
        Assert.Empty(ArchiveExtractionResult.Failed.SkippedEntries);
    }

    [Fact]
    public void FailedIsASingleton()
    {
        Assert.Same(ArchiveExtractionResult.Failed, ArchiveExtractionResult.Failed);
    }

    [Fact]
    public void EqualityWithSameSkippedEntriesReferenceIsEqual()
    {
        IReadOnlyList<string> skipped = ["a.iso"];
        var first = new ArchiveExtractionResult(false, skipped);
        var second = new ArchiveExtractionResult(false, skipped);

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void EqualityWithDifferentSuccessIsNotEqual()
    {
        IReadOnlyList<string> skipped = [];
        var success = new ArchiveExtractionResult(true, skipped);
        var failure = new ArchiveExtractionResult(false, skipped);

        Assert.NotEqual(success, failure);
    }

    [Fact]
    public void EqualityComparesSkippedEntriesByReference()
    {
        var first = new ArchiveExtractionResult(false, ["a.iso"]);
        var second = new ArchiveExtractionResult(false, ["a.iso"]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void WithExpressionChangesSuccess()
    {
        var failed = new ArchiveExtractionResult(false, []);

        var succeeded = failed with { Success = true };

        Assert.True(succeeded.Success);
        Assert.False(failed.Success);
    }

    [Fact]
    public void WithExpressionChangesSkippedEntries()
    {
        var result = new ArchiveExtractionResult(true, []);
        IReadOnlyList<string> skipped = ["ignored.iso", "image2.iso"];

        var updated = result with { SkippedEntries = skipped };

        Assert.Same(skipped, updated.SkippedEntries);
        Assert.Empty(result.SkippedEntries);
    }

    [Fact]
    public void FailedStaticCanBeComparedToConstructedFailure()
    {
        var constructed = ArchiveExtractionResult.Failed with { Success = false };

        Assert.Equal(ArchiveExtractionResult.Failed, constructed);
    }
}