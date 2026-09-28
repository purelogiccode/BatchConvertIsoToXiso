using XboxIsoStudio.Models;

namespace XboxIsoStudio.Interfaces;

public interface IOrchestratorService
{
    Task ConvertAsync(
        string inputFolder,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        OutputFormat outputFormat,
        bool searchSubfolders,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token);

    Task ConvertFilesAsync(
        IReadOnlyList<string> files,
        string outputFolder,
        bool deleteOriginals,
        bool skipSystemUpdate,
        bool checkIntegrity,
        OutputFormat outputFormat,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token);

    Task TestAsync(
        string inputFolder,
        bool moveSuccessful,
        bool moveFailed,
        bool searchSubfolders,
        bool performDeepScan,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token);

    Task TestFilesAsync(
        string inputFolder,
        IReadOnlyList<string> files,
        bool moveSuccessful,
        bool moveFailed,
        bool performDeepScan,
        IProgress<BatchOperationProgress> progress,
        Func<string, Task<CloudRetryResult>> onCloudRetryRequired,
        CancellationToken token);
}