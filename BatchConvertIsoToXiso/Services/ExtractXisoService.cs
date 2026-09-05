using System.Diagnostics;
using System.IO;
using BatchConvertIsoToXiso.Interfaces;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// Service for converting ISO files to XISO format using extract-xiso.exe
/// </summary>
public class ExtractXisoService : IExtractXisoService
{
    private readonly ILogger _logger;
    private readonly IBugReportService _bugReportService;
    private readonly IDiskMonitorService _diskMonitorService;
    private readonly string _extractXisoPath;

    public ExtractXisoService(ILogger logger, IBugReportService bugReportService,
        IDiskMonitorService diskMonitorService)
    {
        _logger = logger;
        _bugReportService = bugReportService;
        _diskMonitorService = diskMonitorService;
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _extractXisoPath = Path.Combine(appDir, "extract-xiso.exe");
    }

    public async Task<bool> ConvertIsoToXisoAsync(string inputFile, string outputFolder, bool skipSystemUpdate,
        CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        _logger.LogMessage($"Converting '{fileName}' using extract-xiso.exe...");

        // Check if extract-xiso.exe exists
        if (!File.Exists(_extractXisoPath))
        {
            _logger.LogMessage($"[ERROR] extract-xiso.exe not found at: {_extractXisoPath}");
            return false;
        }

        // Create a temporary working directory for simplified filenames
        // extract-xiso has issues with spaces and special characters in paths
        var inputFileSize = new FileInfo(inputFile).Length;

        // Pre-check the output drive before doing any work. A direct File.Move of the
        // converted file to a full output drive fails late with a confusing error after
        // minutes of conversion work.
        var outputCheck = CheckOutputDriveAsync(inputFile, inputFileSize, outputFolder);
        if (outputCheck != null)
        {
            _logger.LogMessage($"[ERROR] {outputCheck}");
            return false;
        }

        var tempWorkDir = ResolveTempDirectory(inputFileSize, "BatchConvertIsoToXiso_Work");

        try
        {
            Directory.CreateDirectory(tempWorkDir);

            // Create simplified filenames without spaces/special characters
            // extract-xiso rewrites the file in-place (-r flag), so the output filename
            // matches the input filename. Using "input.iso" as both input and expected output.
            const string simpleInputName = "input.iso";
            var tempInputFile = Path.Combine(tempWorkDir, simpleInputName);

            // Copy input file to temp location with simple name
            _logger.LogMessage("  Preparing file for conversion...");
            await CopyFileWithProgressAsync(inputFile, tempInputFile, token);

            // Create output filename with .iso extension
            var outputFileName = Path.GetFileNameWithoutExtension(inputFile) + ".iso";

            var outputPath = Path.Combine(outputFolder, outputFileName);

            // Ensure output directory exists
            Directory.CreateDirectory(outputFolder);

            // Record start time before starting the process to detect newly created/modified files
            // Subtract 2 seconds to account for file system timestamp granularity (FAT32 has 2-second resolution)
            var conversionStartTime = DateTime.UtcNow.AddSeconds(-2);

            // Build arguments for extract-xiso using simplified paths
            // -r = rewrite (convert to XISO)
            // -d = output directory (temp work dir for simplicity)
            // -s = skip $SystemUpdate folder
            var arguments = $"-r -d \"{tempWorkDir}\"{(skipSystemUpdate ? " -s" : "")} \"{tempInputFile}\"";

            // Use manual disposal instead of 'using' to avoid capturing a disposed variable in the cancellation callback
            Process? process = null;
            CancellationTokenRegistration cancellationRegistration = default;
            try
            {
                process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = _extractXisoPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogMessage($"  [extract-xiso] {e.Data}");
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger.LogMessage($"  [extract-xiso] ERROR: {e.Data}");
                    }
                };

                process.Start();
                SetProcessPrioritySafe(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Register cancellation handler before awaiting
                cancellationRegistration = token.Register(state =>
                {
                    var p = (Process?)state;
                    if (p != null)
                    {
                        // Offload to background thread to avoid blocking UI thread
                        // Use CancellationToken.None because 'token' is already cancelled at this point
                        _ = Task.Run(() => ProcessTerminatorHelper.TerminateProcess(p, "extract-xiso", _logger),
                            CancellationToken.None);
                    }
                }, process);

                await process.WaitForExitAsync(token);

                if (process.ExitCode == 0)
                {
                    // extract-xiso rewrites in-place (-r flag), so the output is the same file as the input
                    if (File.Exists(tempInputFile))
                    {
                        // Move the converted file to the final destination
                        _logger.LogMessage("  Moving converted file to output folder...");
                        await MoveFileWithFallbackAsync(tempInputFile, outputPath, token);
                        _logger.LogMessage($"Successfully converted '{fileName}' to XISO format.");
                        return true;
                    }

                    // Check for alternative output filename in temp directory
                    // Only consider files created/modified after this conversion started to avoid
                    // matching stale files from previous runs
                    var possibleOutputs = Directory.GetFiles(tempWorkDir, "*.iso")
                        .Where(f =>
                        {
                            try
                            {
                                var fileInfo = new FileInfo(f);
                                // File must be created or modified after this conversion started
                                return fileInfo.LastWriteTimeUtc >= conversionStartTime ||
                                       fileInfo.CreationTimeUtc >= conversionStartTime;
                            }
                            catch
                            {
                                return false;
                            }
                        })
                        .ToList();

                    if (possibleOutputs.Count > 0)
                    {
                        // Move the converted file to the final destination
                        _logger.LogMessage("  Moving converted file to output folder...");
                        await MoveFileWithFallbackAsync(possibleOutputs[0], outputPath, token);
                        _logger.LogMessage(
                            $"Successfully converted '{fileName}' to XISO format (found: {Path.GetFileName(possibleOutputs[0])}).");
                        return true;
                    }

                    _logger.LogMessage($"[WARNING] Conversion completed but output file not found for '{fileName}'.");
                    return false;
                }

                _logger.LogMessage($"[ERROR] extract-xiso.exe exited with code {process.ExitCode} for '{fileName}'.");
                return false;
            }
            finally
            {
                // Dispose registration before process to avoid accessing disposed process in callback
                cancellationRegistration.Dispose();
                process?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogMessage($"Conversion of '{fileName}' was canceled.");
            throw;
        }
        catch (Exception ex) when (PathHelper.IsDiskSpaceError(ex))
        {
            _logger.LogMessage($"[ERROR] Not enough disk space to convert '{fileName}': {ex.Message}");
            throw;
        }
        catch (Exception ex) when (PathHelper.IsNetworkError(ex))
        {
            _logger.LogMessage($"[ERROR] Network error while converting '{fileName}': {ex.Message}\n\n" +
                               "Please try:\n" +
                               "1. Check that the network drive is still connected and accessible\n" +
                               "2. Copy the file to a local drive before processing\n" +
                               "3. Check your network connection stability");
            throw;
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogMessage($"[ERROR] Drive or path not found for '{fileName}': {ex.Message}\n\n" +
                               "Please check that the drive is connected and the path exists.");
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogMessage($"[ERROR] Access denied while converting '{fileName}'.\n\n" +
                               "The output folder may be write-protected or require administrator rights.\n" +
                               "Please choose a different output folder or run the application as administrator.");
            return false;
        }
        catch (IOException ex) when (ex.Message.Contains("parameter is incorrect", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogMessage($"[ERROR] Failed to move the converted file for '{fileName}': {ex.Message}\n\n" +
                               "The output drive may not support files of this size or format (e.g. FAT32 is limited to 4 GB per file).\n" +
                               "Please use an NTFS or exFAT formatted output drive.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogMessage($"[ERROR] Failed to convert '{fileName}': {ex.Message}");
            _ = _bugReportService.SendBugReportAsync($"Failed to convert '{fileName}'", ex);
            return false;
        }
        finally
        {
            // Clean up temp working directory
            if (Directory.Exists(tempWorkDir))
            {
                try
                {
                    Directory.Delete(tempWorkDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    /// <summary>
    /// Safely sets the process priority. The process may exit between Start() and the
    /// priority assignment, which throws InvalidOperationException ("Cannot process
    /// request because the process has exited"). This race is benign and can be ignored.
    /// </summary>
    private static void SetProcessPrioritySafe(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
        }
        catch (InvalidOperationException)
        {
            // Process exited before the priority could be set; nothing to do
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Not allowed to change priority (e.g. restricted token); non-fatal
        }
    }

    /// <summary>
    /// Validates that the output drive can hold the converted file before the conversion
    /// starts. Returns an error message when the output drive is full or uses a file system
    /// that cannot store files of this size (FAT32 4 GB limit); otherwise returns null.
    /// </summary>
    private string? CheckOutputDriveAsync(string inputFile, long inputFileSize, string outputFolder)
    {
        try
        {
            var availableSpace = _diskMonitorService.GetAvailableFreeSpace(outputFolder);
            var requiredWithBuffer = inputFileSize + Math.Max(inputFileSize / 10, 200L * 1024 * 1024);

            if (availableSpace > 0 && availableSpace < requiredWithBuffer)
            {
                return $"Not enough disk space on the output drive for '{Path.GetFileName(inputFile)}'. " +
                       $"Required: {Formatter.FormatBytes(inputFileSize)}, Available: {Formatter.FormatBytes(availableSpace)}. " +
                       "Please free up disk space or select a different output folder.";
            }

            var fullPath = Path.GetFullPath(outputFolder);
            if (PathHelper.IsUncPath(fullPath)) return null;

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root)) return null;

            var drive = new DriveInfo(root);
            if (drive.IsReady &&
                drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) &&
                inputFileSize > 4L * 1024 * 1024 * 1024 - 1)
            {
                return $"The output drive '{drive.Name}' uses FAT32, which cannot store files larger than 4 GB. " +
                       $"'{Path.GetFileName(inputFile)}' is {Formatter.FormatBytes(inputFileSize)}. " +
                       "Please use an NTFS or exFAT formatted output drive.";
            }
        }
        catch
        {
            // Pre-check failures are non-fatal; the conversion will surface real errors if they occur
        }

        return null;
    }

    /// <summary>
    /// Copies a file with progress reporting and retry logic for transient IO errors
    /// </summary>
    private static async Task CopyFileWithProgressAsync(string sourcePath, string destPath, CancellationToken token)
    {
        const int bufferSize = 81920; // 80KB buffer
        const int maxRetries = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read, bufferSize, FileOptions.Asynchronous);
                await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize, FileOptions.Asynchronous);

                var buffer = new byte[bufferSize];
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer, token)) > 0)
                {
                    await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                }

                return; // Success
            }
            catch (IOException) when (attempt < maxRetries)
            {
                // Clean up partial dest file before retry
                try
                {
                    if (File.Exists(destPath)) File.Delete(destPath);
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(attempt * 2000, token);
            }
        }
    }

    /// <summary>
    /// Moves a file to the destination, falling back to copy+delete when a direct move fails.
    /// A direct File.Move across different volumes (or when overwriting an existing file on some
    /// file systems) can throw IOException "The parameter is incorrect", so we retry by copying
    /// the bytes and deleting the source.
    /// </summary>
    private static async Task MoveFileWithFallbackAsync(string sourcePath, string destPath, CancellationToken token)
    {
        try
        {
            if (File.Exists(destPath)) File.Delete(destPath);
            File.Move(sourcePath, destPath);
            return;
        }
        catch (IOException)
        {
            // Fall back to copy + delete (handles cross-volume moves and overwrite quirks)
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to copy + delete
        }

        await CopyFileWithProgressAsync(sourcePath, destPath, token);

        try
        {
            File.Delete(sourcePath);
        }
        catch
        {
            // Source cleanup failure is non-fatal; temp directory is deleted afterwards anyway
        }
    }

    private string ResolveTempDirectory(long requiredSize, string tempSubfolder)
    {
        return PathHelper.ResolveTempDirectory(requiredSize, tempSubfolder, _diskMonitorService);
    }
}