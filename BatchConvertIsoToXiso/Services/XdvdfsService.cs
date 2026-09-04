using System.Diagnostics;
using System.IO;
using BatchConvertIsoToXiso.Interfaces;

namespace BatchConvertIsoToXiso.Services;

/// <summary>
/// Service for converting ISO files to XISO format using xdvdfs.exe
/// </summary>
public class XdvdfsService : IXdvdfsService
{
    private readonly ILogger _logger;
    private readonly IBugReportService _bugReportService;
    private readonly string _xdvdfsPath;

    public XdvdfsService(ILogger logger, IBugReportService bugReportService)
    {
        _logger = logger;
        _bugReportService = bugReportService;
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _xdvdfsPath = Path.Combine(appDir, "xdvdfs.exe");
    }

    public async Task<bool> ConvertIsoToXisoAsync(string inputFile, string outputFolder, CancellationToken token)
    {
        var fileName = Path.GetFileName(inputFile);
        _logger.LogMessage($"Converting '{fileName}' using xdvdfs.exe...");
        _logger.LogMessage("[WARNING] The 'Skip $SystemUpdate' feature is NOT supported by the xdvdfs tool.");

        // Check if xdvdfs.exe exists
        if (!File.Exists(_xdvdfsPath))
        {
            _logger.LogMessage($"[ERROR] xdvdfs.exe not found at: {_xdvdfsPath}");
            return false;
        }

        // Create output filename with .iso extension
        var outputFileName = Path.GetFileNameWithoutExtension(inputFile) + ".iso";
        var outputPath = Path.Combine(outputFolder, outputFileName);

        // Ensure output directory exists
        Directory.CreateDirectory(outputFolder);

        // Repacking an Image
        // Images can be repacked from an existing ISO image:
        // xdvdfs pack <input-image> [optional output path]
        // This will create an iso that matches 1-to-1 with the input image.

        var arguments = $"pack \"{inputFile}\" \"{outputPath}\"";

        // Use manual disposal instead of 'using' to avoid capturing a disposed variable in the cancellation callback
        Process? process = null;
        CancellationTokenRegistration cancellationRegistration = default;

        // Record start time to detect newly created files when recovering the output
        // Subtract 2 seconds to account for file system timestamp granularity (FAT32 has 2-second resolution)
        var conversionStartTime = DateTime.UtcNow.AddSeconds(-2);

        try
        {
            process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = _xdvdfsPath,
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
                    _logger.LogMessage($"  [xdvdfs] {e.Data}");
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogMessage($"  [xdvdfs] ERROR: {e.Data}");
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
                    _ = Task.Run(() => ProcessTerminatorHelper.TerminateProcess(p, "xdvdfs", _logger), CancellationToken.None);
                }
            }, process);

            await process.WaitForExitAsync(token);

            if (process.ExitCode == 0)
            {
                // Give the file system a moment (antivirus/indexing can briefly hold new files)
                var found = await WaitForOutputFileAsync(outputPath, token);

                if (!found)
                {
                    // Fallback: xdvdfs may have written the output elsewhere (e.g. working directory
                    // or next to the input file). Search for a matching file created during this
                    // conversion and move it to the expected output location.
                    found = await TryRecoverOutputFileAsync(outputPath, outputFileName, inputFile, conversionStartTime, token);
                }

                if (found)
                {
                    _logger.LogMessage($"Successfully converted '{fileName}' to XISO format using xdvdfs.");
                    return true;
                }

                _logger.LogMessage($"[WARNING] xdvdfs completed but output file not found for '{fileName}'.");
                _logger.LogMessage("[HINT] If you are using an antivirus, it may have quarantined the newly created file. Please add an exclusion for the output folder and try again.");
                _ = _bugReportService.SendBugReportAsync($"xdvdfs completed but output file not found for '{fileName}'");
                return false;
            }

            _logger.LogMessage($"[ERROR] xdvdfs.exe exited with code {process.ExitCode} for '{fileName}'. The ISO file may be invalid, corrupt, or not a supported Xbox ISO format.");
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogMessage($"Conversion of '{fileName}' was canceled. Cleaning up partially saved file...");
            try
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw;
        }
        finally
        {
            // Dispose registration before process to avoid accessing disposed process in callback
            cancellationRegistration.Dispose();
            process?.Dispose();
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
    /// Waits briefly for the output file to appear. Antivirus scanners and file
    /// indexing can delay file visibility for a short period after the tool exits.
    /// </summary>
    private static async Task<bool> WaitForOutputFileAsync(string outputPath, CancellationToken token)
    {
        const int maxAttempts = 3;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (File.Exists(outputPath)) return true;

            if (attempt < maxAttempts - 1)
            {
                await Task.Delay(1000, token);
            }
        }

        return File.Exists(outputPath);
    }

    /// <summary>
    /// Searches likely output locations (working directory, input file directory) for a
    /// file created during this conversion and moves it to the expected output path.
    /// </summary>
    private async Task<bool> TryRecoverOutputFileAsync(string outputPath, string outputFileName, string inputFile, DateTime conversionStartTime, CancellationToken token)
    {
        var searchDirectories = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.GetDirectoryName(inputFile)
        }.Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in searchDirectories)
        {
            try
            {
                var candidate = Path.Combine(directory!, outputFileName);

                // Exact name match first (xdvdfs may have used the default naming scheme)
                if (File.Exists(candidate) &&
                    !string.Equals(candidate, outputPath, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(candidate, inputFile, StringComparison.OrdinalIgnoreCase))
                {
                    return await MoveRecoveredFileAsync(candidate, outputPath, token);
                }

                // Otherwise look for any .iso created/modified during this conversion
                var recentFiles = Directory.GetFiles(directory!, "*.iso")
                    .Where(f =>
                    {
                        try
                        {
                            var fileInfo = new FileInfo(f);
                            return !string.Equals(f, inputFile, StringComparison.OrdinalIgnoreCase) &&
                                   (fileInfo.LastWriteTimeUtc >= conversionStartTime || fileInfo.CreationTimeUtc >= conversionStartTime);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .ToList();

                if (recentFiles.Count > 0)
                {
                    return await MoveRecoveredFileAsync(recentFiles[0], outputPath, token);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"  [xdvdfs] Output recovery search failed in '{directory}': {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Moves a recovered output file to the expected destination, falling back to copy+delete
    /// when a direct move fails (e.g. cross-volume moves).
    /// </summary>
    private async Task<bool> MoveRecoveredFileAsync(string sourcePath, string destPath, CancellationToken token)
    {
        _logger.LogMessage($"  [xdvdfs] Output file found at '{sourcePath}'. Moving to expected location...");

        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(sourcePath, destPath);
            }, token);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Fall back to copy + delete (handles cross-volume moves and file locks)
        }

        try
        {
            await Task.Run(async () =>
            {
                await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
                await using var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);

                await sourceStream.CopyToAsync(destStream, token);
            }, token);

            File.Delete(sourcePath);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogMessage($"  [xdvdfs] Failed to move recovered output file: {ex.Message}");

            try
            {
                if (File.Exists(destPath)) File.Delete(destPath);
            }
            catch
            {
                // Ignore cleanup errors
            }

            return false;
        }
    }
}