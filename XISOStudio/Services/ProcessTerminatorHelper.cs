using System.ComponentModel;
using System.Diagnostics;
using Serilog;

namespace XISOStudio.Services;

/// <summary>
/// Terminates processes gracefully where possible, falling back to a forced kill of the entire
/// process tree.
/// </summary>
public static class ProcessTerminatorHelper
{
    /// <summary>
    /// Robustly terminates a process with graceful shutdown fallback
    /// </summary>
    public static void TerminateProcess(Process process, string processName, ILogger logger)
    {
        try
        {
            if (process.HasExited)
            {
                logger.Information("Process {ProcessName} has already exited.", processName);
                return;
            }
        }
        catch (InvalidOperationException ex)
        {
            logger.Debug(ex,
                "Process {ProcessName} is not associated with a running process or has been disposed.", processName);
            return;
        }
        catch (Win32Exception ex)
        {
            // HasExited throws Win32Exception (not InvalidOperationException) when the
            // process handle cannot be opened, e.g. for an elevated process.
            logger.Debug(ex, "Process {ProcessName} cannot be accessed.", processName);
            return;
        }

        try
        {
            logger.Information("Attempting graceful termination of {ProcessName}...", processName);

            // Try graceful shutdown first
            if (process.CloseMainWindow())
            {
                if (process.WaitForExit(3000))
                {
                    logger.Information("Process {ProcessName} exited gracefully.", processName);
                    return;
                }
            }

            logger.Information("Graceful termination failed for {ProcessName}, forcing kill...", processName);

            // Force kill with entire process tree
            process.Kill(true);

            // Wait for process to fully exit and release handles
            if (process.WaitForExit(5000))
            {
                logger.Information("Process {ProcessName} was killed successfully.", processName);
                return;
            }

            logger.Warning("Process {ProcessName} did not exit within timeout after kill.", processName);
        }
        catch (InvalidOperationException ex)
        {
            logger.Debug(ex, "Process {ProcessName} already exited during termination.", processName);
        }
        catch (Win32Exception ex)
        {
            logger.Warning(ex, "Access denied terminating {ProcessName}", processName);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Unexpected error terminating {ProcessName}", processName);
        }
    }
}