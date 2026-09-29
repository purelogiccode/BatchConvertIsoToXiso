using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XISOStudio.Interfaces;
using XISOStudio.Models;
using XISOStudio.Services;
using Serilog;

namespace XISOStudio;

/// <summary>
/// Main application window: batch conversion and integrity testing, the on-screen log
/// viewer, and the XISO/ZAR/CHD image explorer. The class is split across several
/// partial files by feature.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Maximum number of characters kept in the on-screen log viewer before the oldest half is dropped.</summary>
    private const int MaxLogLength = 100000; // Approx 1000-2000 lines depending on length

    /// <summary>Coordinates batch conversion and integrity testing.</summary>
    private readonly IOrchestratorService _orchestratorService = null!;
    /// <summary>Reports disk read/write speed and free space while an operation runs.</summary>
    private readonly IDiskMonitorService _diskMonitorService = null!;
    /// <summary>Cancels the current batch operation.</summary>
    private CancellationTokenSource _cts = new();
    /// <summary>Signals completion of the current batch operation to the shutdown flow.</summary>
    private TaskCompletionSource _operationCompletedTcs = new();
    /// <summary>Checks GitHub for a newer release at startup.</summary>
    private readonly IUpdateChecker _updateChecker = null!;
    /// <summary>Logger scoped to this window.</summary>
    private readonly ILogger _logger = null!;
    /// <summary>Shows modal dialogs such as confirmations and errors.</summary>
    private readonly IMessageBoxService _messageBoxService = null!;
    /// <summary>Opens links in the default browser.</summary>
    private readonly IUrlOpener _urlOpener = null!;
    /// <summary>Captures the active window for the F8 screenshot shortcut.</summary>
    private readonly IScreenshotService _screenshotService = null!;

    // Summary Stats
    /// <summary>Measures the elapsed time of the current operation.</summary>
    private readonly Stopwatch _operationStopwatch = new();
    /// <summary>Updates the elapsed-time and disk-speed display every second.</summary>
    private readonly DispatcherTimer _processingTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    /// <summary>Updates the managed-memory display every two seconds.</summary>
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    /// <summary>Total number of files reported for the current operation.</summary>
    private int _uiTotalFiles;
    /// <summary>Number of files processed successfully in the current operation.</summary>
    private int _uiSuccessCount;
    /// <summary>Number of files that failed in the current operation.</summary>
    private int _uiFailedCount;
    /// <summary>Number of files skipped in the current operation.</summary>
    private int _uiSkippedCount;
    /// <summary>Indicates whether a batch operation is currently running.</summary>
    private bool _isOperationRunning;
    /// <summary>Indicates that the window is closing without further confirmation.</summary>
    private bool _isForceClosing;
    /// <summary>Indicates that a close negotiation is already in progress.</summary>
    private bool _isClosingInProgress;

    /// <summary>Number of processed files that were not valid Xbox ISOs.</summary>
    private int _invalidIsoErrorCount;
    /// <summary>Total number of files processed in the current operation.</summary>
    private int _totalProcessedFiles;
    /// <summary>Paths of the files that failed in the current operation.</summary>
    private readonly HashSet<string> _failedFilePaths = new(StringComparer.OrdinalIgnoreCase);

    // Image Explorer State
    /// <summary>Explorer for the image currently open in the explorer view.</summary>
    private IImageExplorer? _explorer;
    /// <summary>Guards access to <see cref="_explorer"/>.</summary>
    private readonly Lock _explorerLock = new();

    // Serializes CopyOut with explorer disposal: an explorer is only disposed once any
    // background copy-out still using it has finished.
    /// <summary>Serializes explorer use with explorer retirement.</summary>
    private readonly SemaphoreSlim _explorerUseLock = new(1, 1);
    /// <summary>Directory currently shown in the explorer view.</summary>
    private string _currentInternalPath = "/";

    /// <summary>Set once the constructor finished so XAML-driven events can be ignored during load.</summary>
    private readonly bool _isUiInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class for the XAML designer;
    /// use the dependency-injection constructor at runtime.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class with all of its
    /// runtime dependencies.
    /// </summary>
    /// <param name="updateChecker">Checks GitHub for a newer release.</param>
    /// <param name="logger">Serilog logger for the window and its services.</param>
    /// <param name="messageBoxService">Service used to show modal dialogs.</param>
    /// <param name="urlOpener">Service used to open links in the default browser.</param>
    /// <param name="screenshotService">Service that captures the active window (F8).</param>
    /// <param name="orchestratorService">Batch conversion and integrity-test orchestrator.</param>
    /// <param name="diskMonitorService">Live disk read/write speed monitor.</param>
    public MainWindow(IUpdateChecker updateChecker, ILogger logger,
        IMessageBoxService messageBoxService, IUrlOpener urlOpener, IScreenshotService screenshotService,
        IOrchestratorService orchestratorService, IDiskMonitorService diskMonitorService)
        : this()
    {
        _updateChecker = updateChecker;
        _logger = logger.ForContext<MainWindow>();
        _messageBoxService = messageBoxService;
        _urlOpener = urlOpener;
        _screenshotService = screenshotService;
        _orchestratorService = orchestratorService;
        _diskMonitorService = diskMonitorService;

        InitializeFileLists();

        // Display every Serilog event in the on-screen log viewer.
        UiLogSink.MessageLogged += OnLogMessage;
        Closed += MainWindow_Closed;

        _processingTimer.Tick += ProcessingTimer_Tick;
        _memoryTimer.Tick += MemoryTimer_Tick;

        ResetSummaryStats();
        DisplayInstructions.Initialize(_logger);
        DisplayInstructions.DisplayInitialInstructions();

        _isUiInitialized = true;
    }

    /// <summary>Detaches the log-viewer subscription when the window closes.</summary>
    /// <param name="sender">The window that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        UiLogSink.MessageLogged -= OnLogMessage;
    }

    /// <summary>
    /// Called by <see cref="UiLogSink"/> for every log event. Appends the pre-formatted
    /// line to the log viewer on the UI thread, truncating the oldest half when the
    /// viewer grows too large.
    /// </summary>
    /// <param name="sender">The log sink that raised the event.</param>
    /// <param name="e">The pre-formatted log message.</param>
    private void OnLogMessage(object? sender, UiLogSink.LogMessageEventArgs e)
    {
        var logViewer = LogViewer;

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                var text = logViewer.Text ?? string.Empty;
                if (text.Length > MaxLogLength)
                {
                    // Keep the last ~50% of the log, try to cut at a newline
                    var cutIndex = text.IndexOf('\n', text.Length / 2);
                    text = cutIndex >= 0 ? text.Substring(cutIndex + 1) : text.Substring(text.Length / 2);
                    logViewer.Text = text;
                }

                AppendLogText($"{e.Message}{Environment.NewLine}");
            }
            catch (Exception ex)
            {
                // Never log this through Serilog: the UI sink is part of the same pipeline,
                // so a persistent viewer failure would feed back into this method forever.
                Serilog.Debugging.SelfLog.WriteLine(
                    "Failed to append a message to the on-screen log viewer: {0}", ex);
            }
        });
    }

    /// <summary>
    /// Appends text to the log viewer and keeps the caret at the end so the view
    /// scrolls to the newest line.
    /// </summary>
    /// <param name="line">The text to append, including any trailing newline.</param>
    private void AppendLogText(string line)
    {
        var logViewer = LogViewer;
        logViewer.Text = (logViewer.Text ?? string.Empty) + line;
        logViewer.CaretIndex = logViewer.Text.Length;
    }

    /// <summary>Sets the initial navigation style and starts the update check once the window is loaded.</summary>
    /// <param name="sender">The window that raised the event.</param>
    /// <param name="e">The event data.</param>
    private async void Window_LoadedAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Set initial navigation button style
            UpdateNavigationButtonStyles(BtnNavConvert);

            try
            {
                await CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking for updates");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error setting initial navigation button style");
        }
    }

    /// <summary>Negotiates the close asynchronously instead of closing synchronously.</summary>
    /// <param name="sender">The window that raised the event.</param>
    /// <param name="e">The closing event data whose cancellation flag is set.</param>
    private void Window_Closing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            if (_isForceClosing) return;

            // A close is already being negotiated (for example the Exit button was clicked
            // while the confirmation dialog is open): keep vetoing without starting a second flow.
            if (_isClosingInProgress)
            {
                e.Cancel = true;
                return;
            }

            // Never close synchronously: when an operation is running this handler needs
            // an asynchronous confirmation, and Avalonia does not pump a nested loop.
            e.Cancel = true;
            _isClosingInProgress = true;
            _ = HandleClosingAsync();
        }
        catch (Exception ex)
        {
            _isClosingInProgress = false;
            _logger.Error(ex, "Error while closing the main window");
        }
    }

    /// <summary>Asks for confirmation when an operation is running, then closes the window.</summary>
    private async Task HandleClosingAsync()
    {
        try
        {
            if (_isOperationRunning)
            {
                var result = await _messageBoxService.ShowAsync("An operation is still running. Exit anyway?",
                    "Warning",
                    UiMessageBoxButton.YesNo, UiMessageBoxImage.Warning);
                if (result != UiMessageBoxResult.Yes)
                {
                    // The user chose to keep working: allow a future close attempt.
                    _isClosingInProgress = false;
                    return;
                }

                await WaitForOperationAndCloseAsync();
                return;
            }

            // No operation running, safe to close immediately
            CleanupResources();
            _isForceClosing = true;
            Close();
        }
        catch (Exception ex)
        {
            _isClosingInProgress = false;
            _logger.Error(ex, "Error while handling window close");
        }
    }

    /// <summary>Cancels the running operation and waits briefly for it before closing the window.</summary>
    private async Task WaitForOperationAndCloseAsync()
    {
        try
        {
            _logger.Information("Waiting for current operation to cancel before exiting...");

            // Signal the operation to stop
            _cts.Cancel();

            // Wait up to 10 seconds for the operation to complete
            var completedTask = await Task.WhenAny(_operationCompletedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));

            var timedOut = completedTask != _operationCompletedTcs.Task;
            if (timedOut)
            {
                _logger.Warning("Operation did not complete within timeout. Closing anyway.");
            }
            else
            {
                _logger.Information("Operation completed. Closing application...");
            }

            // Now perform cleanup and close on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isForceClosing = true;
                CleanupResources();
                Close();
            });

            // If the operation timed out, the UI thread may still be blocked by a
            // queued message box or another modal dialog. Force a process-level exit
            // after a delay as a last resort — this skips normal cleanup but prevents
            // a permanently hung process.
            if (timedOut)
            {
                ThreadPool.QueueUserWorkItem(static _ =>
                {
                    Thread.Sleep(5000);
                    Environment.Exit(0);
                });
            }
        }
        catch (Exception ex)
        {
            // The window did not close: allow a future close attempt.
            _isClosingInProgress = false;
            _logger.Error(ex, "Error while waiting for the current operation to cancel");
        }
    }

    /// <summary>Stops the timers and disk monitoring and disposes the explorer and cancellation token source.</summary>
    private void CleanupResources()
    {
        try
        {
            IImageExplorer? explorer;
            lock (_explorerLock)
            {
                explorer = _explorer;
                _explorer = null;
            }

            // Deferred: a background copy-out may still be reading from the explorer.
            RetireExplorer(explorer);

            _processingTimer.Stop();
            _memoryTimer.Stop();
            StopPerformanceCounter();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during resource cleanup");
        }

        try
        {
            _cts.Cancel();
            _cts.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore disposal errors during shutdown
            _logger.Debug(ex, "Ignoring cancellation token disposal error during shutdown");
        }
    }

    /// <summary>Updates the memory usage display.</summary>
    /// <param name="sender">The timer that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void MemoryTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var memoryMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            MemoryTextBlock.Text = $"Memory: {memoryMb:F1} MB";
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error while updating the memory timer UI");
        }
    }

    /// <summary>Closes the window so the normal closing negotiation runs.</summary>
    /// <param name="sender">The menu item that raised the event.</param>
    /// <param name="e">The event data.</param>
    private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        // Close the window instead of calling desktop.Shutdown(): Shutdown() forces the
        // window closed even when Window_Closing cancels, which bypasses the
        // "operation still running" confirmation. Close() lets the handler veto.
        Close();
    }

    /// <summary>Handles the F8 screenshot shortcut.</summary>
    /// <param name="sender">The window that raised the event.</param>
    /// <param name="e">The key event data.</param>
    private async void Window_KeyDownAsync(object? sender, KeyEventArgs e)
    {
        try
        {
            if (e.Key == Key.F8)
            {
                e.Handled = true;
                var filePath = await _screenshotService.CaptureActiveWindowAsync();
                if (filePath is not null)
                {
                    _logger.Information("Screenshot captured: {FilePath}", filePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method Window_KeyDownAsync");
        }
    }
}