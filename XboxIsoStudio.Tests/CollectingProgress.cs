using XboxIsoStudio.Models;

namespace XboxIsoStudio.Tests;

/// <summary>Collects progress reports synchronously (unlike <see cref="Progress{T}" />).</summary>
internal sealed class CollectingProgress : IProgress<BatchOperationProgress>
{
    private readonly Lock _lock = new();
    private readonly List<BatchOperationProgress> _reports = [];

    public IReadOnlyList<BatchOperationProgress> Reports
    {
        get
        {
            lock (_lock)
            {
                return [.. _reports];
            }
        }
    }

    public void Report(BatchOperationProgress value)
    {
        lock (_lock)
        {
            _reports.Add(value);
        }
    }
}
