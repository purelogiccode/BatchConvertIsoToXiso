using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;
using Serilog;

namespace XboxIsoStudio;

/// <summary>
/// Main application window: batch conversion and integrity testing, the on-screen log
/// viewer, and the XISO/ZAR/CHD image explorer. The class is split across several
/// partial files by feature.
/// </summary>
public partial class MainWindow : Window
{
    private const int MaxLogLength = 100000; // Approx 1000-2000 lines depending on length

    private readonly IOrchestratorService _orchestratorService = null!;
    private readonly IDiskMonitorService _diskMonitorService = null!;
    private CancellationTokenSource _cts = new();
    private TaskCompletionSource _operationCompletedTcs = new();
    private readonly IUpdateChecker _updateChecker = null!;
    private readonly ILogger _logger = null!;
    private readonly IMessageBoxService _messageBoxService = null!;
    private readonly IUrlOpener _urlOpener = null!;
    private readonly IScreenshotService _screenshotService = null!;

    // Summary Stats
    private readonly Stopwatch _operationStopwatch = new();
    private readonly DispatcherTimer _processingTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private int _uiTotalFiles;
    private int _uiSuccessCount;
    private int _uiFailedCount;
    private int _uiSkippedCount;
    private bool _isOperationRunning;
    private bool _isForceClosing;
    private bool _isClosingInProgress;

    private int _invalidIsoErrorCount;
    private int _totalProcessedFiles;
    private readonly HashSet<string> _failedFilePaths = new(StringComparer.OrdinalIgnoreCase);

    // Image Explorer State
    private IImageExplorer? _explorer;
    private readonly Lock _explorerLock = new();

    // Serializes CopyOut with explorer disposal: an explorer is only disposed once any
    // background copy-out still using it has finished.
    private readonly SemaphoreSlim _explorerUseLock = new(1, 1);
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

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        UiLogSink.MessageLogged -= OnLogMessage;
    }

    /// <summary>
    /// Called by <see cref="UiLogSink"/> for every log event. Appends the pre-formatted
    /// line to the log viewer on the UI thread, truncating the oldest half when the
    /// viewer grows too large.
    /// </summary>
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
                // Never let a log-viewer failure propagate back into the Serilog pipeline.
                _logger.Debug(ex, "Failed to append a message to the on-screen log viewer");
            }
        });
    }

    /// <summary>
    /// Appends text to the log viewer and keeps the caret at the end so the view
    /// scrolls to the newest line.
    /// </summary>
    private void AppendLogText(string line)
    {
        var logViewer = LogViewer;
        logViewer.Text = (logViewer.Text ?? string.Empty) + line;
        logViewer.CaretIndex = logViewer.Text.Length;
    }

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
                timedOut = true;
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
            if (timedOut || !_isForceClosing)
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

    private void MemoryTimer_Tick(object? sender, EventArgs e)
    {
        var memoryMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        MemoryTextBlock.Text = $"Memory: {memoryMb:F1} MB";
    }

    private void ExitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        // Close the window instead of calling desktop.Shutdown(): Shutdown() forces the
        // window closed even when Window_Closing cancels, which bypasses the
        // "operation still running" confirmation. Close() lets the handler veto.
        Close();
    }

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