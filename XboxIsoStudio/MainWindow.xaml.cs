using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Services;
using Serilog;

namespace XboxIsoStudio;

public partial class MainWindow
{
    private const int MaxLogLength = 100000; // Approx 1000-2000 lines depending on length

    private readonly IOrchestratorService _orchestratorService;
    private readonly IDiskMonitorService _diskMonitorService;
    private CancellationTokenSource _cts = new();
    private TaskCompletionSource _operationCompletedTcs = new();
    private readonly IUpdateChecker _updateChecker;
    private readonly ILogger _logger;
    private readonly IMessageBoxService _messageBoxService;
    private readonly IUrlOpener _urlOpener;
    private readonly IScreenshotService _screenshotService;

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

    private int _invalidIsoErrorCount;
    private int _totalProcessedFiles;
    private readonly HashSet<string> _failedFilePaths = new(StringComparer.OrdinalIgnoreCase);

    // Image Explorer State
    private IImageExplorer? _explorer;
    private readonly Lock _explorerLock = new();
    private string _currentInternalPath = "/";

    public MainWindow(IUpdateChecker updateChecker, ILogger logger,
        IMessageBoxService messageBoxService, IUrlOpener urlOpener, IScreenshotService screenshotService,
        IOrchestratorService orchestratorService, IDiskMonitorService diskMonitorService)
    {
        InitializeComponent();

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
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        _ = dispatcher.InvokeAsync(() =>
        {
            if (logViewer.Text.Length > MaxLogLength)
            {
                var text = logViewer.Text;
                // Keep the last ~50% of the log, try to cut at a newline
                var cutIndex = text.IndexOf('\n', text.Length / 2);
                logViewer.Text = cutIndex >= 0 ? text.Substring(cutIndex + 1) : text.Substring(text.Length / 2);
            }

            logViewer.AppendText($"{e.Message}{Environment.NewLine}");
            logViewer.ScrollToEnd();
        });
    }

    private async void Window_LoadedAsync(object sender, RoutedEventArgs e)
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

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        try
        {
            if (_isForceClosing) return;

            if (_isOperationRunning)
            {
                var result = _messageBoxService.Show("An operation is still running. Exit anyway?", "Warning",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // Cancel the operation and prevent immediate window close
                e.Cancel = true;

                // Wait for the operation to complete in the background, then close
                _ = WaitForOperationAndCloseAsync();
                return;
            }

            // No operation running, safe to close immediately
            CleanupResources();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while closing the main window");
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
            await Dispatcher.InvokeAsync(() =>
            {
                _isForceClosing = true;
                CleanupResources();
                Close();
            });

            // If the operation timed out, the dispatcher may still be blocked by a
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
            _logger.Error(ex, "Error while waiting for the current operation to cancel");
        }
    }

    private void CleanupResources()
    {
        try
        {
            lock (_explorerLock)
            {
                _explorer?.Dispose();
            }

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

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void Window_KeyDownAsync(object sender, System.Windows.Input.KeyEventArgs e)
    {
        try
        {
            if (e.Key == System.Windows.Input.Key.F8)
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