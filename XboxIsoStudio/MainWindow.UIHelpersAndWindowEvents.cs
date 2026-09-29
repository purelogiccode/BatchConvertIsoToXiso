using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Serilog;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;

namespace XboxIsoStudio;

[SuppressMessage("ReSharper", "UnusedMember.Local",
    Justification =
        "XAML event handlers are resolved by the Avalonia markup compiler, which ReSharper does not link across partial class files.")]
[SuppressMessage("ReSharper", "UnusedParameter.Local",
    Justification = "Parameters are required by XAML event handler signatures (sender, event args).")]
public partial class MainWindow
{
    private void FinalizeUiState()
    {
        _processingTimer.Stop();
        _memoryTimer.Stop();
        _diskMonitorService.StopMonitoring();
        _isPerformanceCounterStopped = false;
        StopPerformanceCounter();
        ProgressBar.IsIndeterminate = false;

        var finalElapsedTime = _operationStopwatch.Elapsed;
        ProcessingTimeValue.Text = finalElapsedTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private async Task<CloudRetryResult> HandleCloudRetryRequestAsync(string fileName)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var result = await _messageBoxService.ShowAsync(
                $"The file '{fileName}' is stored in the cloud and needs to be downloaded.\n\n" +
                "• Click 'Yes' to Retry.\n" +
                "• Click 'No' to Skip.\n" +
                "• Click 'Cancel' to stop the batch.",
                "Cloud File Required",
                UiMessageBoxButton.YesNoCancel,
                UiMessageBoxImage.Information);

            return result switch
            {
                UiMessageBoxResult.Yes => CloudRetryResult.Retry,
                UiMessageBoxResult.No => CloudRetryResult.Skip,
                _ => CloudRetryResult.Cancel
            };
        });
    }

    private async Task PreOperationCleanupAsync()
    {
        try
        {
            _logger.Information("Performing pre-operation cleanup of temporary folders...");
            await TempFolderCleanupHelper.CleanupBatchConvertTempFoldersAsync(_logger);
            _logger.Information("Pre-operation cleanup completed.");
        }
        catch (Exception ex)
        {
            // Cleanup is best-effort; never block the operation because of it.
            _logger.Warning(ex, "Pre-operation cleanup of temporary folders failed");
        }
    }

    private void CancelButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException ex)
        {
            // CTS already disposed during shutdown — ignore
            _logger.Debug(ex, "Cancellation token source already disposed during shutdown");
        }

        _logger.Information("Cancellation requested. Finishing current file...");
    }

    private async void AboutMenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var aboutWindow = new AboutWindow(_urlOpener, _messageBoxService, _logger);
            await aboutWindow.ShowDialog(this);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method AboutMenuItem_Click");
        }
    }

    private void DonateButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _urlOpener.OpenUrl("https://www.purelogiccode.com/donate");
    }

    private async Task<string?> SelectFolderAsync(string description)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = description,
                AllowMultiple = false
            });

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Folder picker failed");
            return null;
        }
    }

    private async Task<bool> ValidateInputOutputFoldersAsync(string inputFolder, string outputFolder)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalizedInput = Path.GetFullPath(inputFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedOutput = Path.GetFullPath(outputFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (normalizedInput.Equals(normalizedOutput, comparison))
        {
            await _messageBoxService.ShowErrorAsync("Input and output folders must be different.");
            return false;
        }

        // Check if output folder is a subfolder of input folder
        if (normalizedOutput.StartsWith(normalizedInput + Path.DirectorySeparatorChar, comparison))
        {
            await _messageBoxService.ShowErrorAsync(
                "Output folder cannot be a subfolder of the input folder. This would cause recursive processing issues.");
            return false;
        }

        return true;
    }

    private void UpdateSummaryStatsUi()
    {
        TotalFilesValue.Text = _uiTotalFiles.ToString(CultureInfo.InvariantCulture);
        SuccessValue.Text = _uiSuccessCount.ToString(CultureInfo.InvariantCulture);
        FailedValue.Text = _uiFailedCount.ToString(CultureInfo.InvariantCulture);
        SkippedValue.Text = _uiSkippedCount.ToString(CultureInfo.InvariantCulture);
    }

    private void UpdateProgressUi(int current, int total)
    {
        // Don't update determinate text if we haven't received a total yet
        if (total <= 0) return;

        ProgressBar.Maximum = total;
        ProgressBar.Value = current;

        if (ProgressBar.IsVisible && !ProgressBar.IsIndeterminate)
        {
            var percentage = (double)current / total * 100;
            ProgressTextBlock.Text = $"{current} of {total} ({percentage:F0}%)";
        }
    }

    /// <summary>
    ///     Restores the normal UI state as soon as the batch is over and shows the summary.
    ///     The controls are re-enabled before the summary dialog is shown so that a dialog
    ///     failure can never leave the window permanently disabled.
    /// </summary>
    private async Task FinishOperationAsync(string operationType, bool operationStarted, bool operationCanceled)
    {
        FinalizeUiState();

        _isOperationRunning = false;
        SetControlsState(true);

        try
        {
            await LogOperationSummaryAsync(operationType, operationStarted, operationCanceled);
        }
        finally
        {
            // Always release the shutdown wait, even if the summary failed.
            _operationCompletedTcs.TrySetResult();
        }
    }

    private async Task LogOperationSummaryAsync(string operationType, bool operationStarted, bool operationCanceled)
    {
        try
        {
            _logger.Information("");
            _logger.Information("--- Batch {OperationType} {Outcome}. ---", operationType.ToLowerInvariant(),
                operationCanceled ? "canceled" : "completed");
            _logger.Information("Total files processed: {TotalFiles}", _uiTotalFiles);
            _logger.Information("Successfully {Action}: {SuccessCount} files",
                ConvertToPastTense.GetPastTense(operationType), _uiSuccessCount);
            _logger.Information("Skipped: {SkippedCount} files", _uiSkippedCount);

            if (_uiFailedCount > 0)
            {
                _logger.Information("Failed to {OperationType}: {FailedCount} files",
                    operationType.ToLowerInvariant(), _uiFailedCount);
                _logger.Information("List of files that failed (original names):");
                foreach (var originalPath in _failedFilePaths)
                {
                    _logger.Information("- {FileName}", Path.GetFileName(originalPath));
                }

                _logger.Information("");
            }

            // Validation failed before the batch started: the error dialog was already shown,
            // so there is no "completed" summary to display.
            if (!operationStarted) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (_isForceClosing) return;

                var summaryText = $"Total files processed: {_uiTotalFiles}\n" +
                                  $"Successfully {ConvertToPastTense.GetPastTense(operationType)}: {_uiSuccessCount} files\n" +
                                  $"Skipped: {_uiSkippedCount} files\n" +
                                  $"Failed: {_uiFailedCount} files";

                if (operationCanceled)
                {
                    await _messageBoxService.ShowAsync(
                        $"Batch {operationType.ToLowerInvariant()} was canceled.\n\n{summaryText}",
                        $"{operationType} Canceled", UiMessageBoxButton.Ok, UiMessageBoxImage.Warning);
                    return;
                }

                if (_totalProcessedFiles > 5 && (double)_invalidIsoErrorCount / _totalProcessedFiles > 0.5)
                {
                    await _messageBoxService.ShowWarningAsync(
                        $"Many files ({_invalidIsoErrorCount} out of {_totalProcessedFiles}) were not valid Xbox ISOs. " +
                        "Please ensure you are selecting the correct ISO files from Xbox or Xbox 360 games.",
                        "High Rate of Invalid ISOs Detected");
                }

                await _messageBoxService.ShowAsync($"Batch {operationType.ToLowerInvariant()} completed.\n\n" +
                                                   summaryText,
                    $"{operationType} Complete", UiMessageBoxButton.Ok,
                    _uiFailedCount > 0 ? UiMessageBoxImage.Warning : UiMessageBoxImage.Information);
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while logging the batch operation summary");
        }
    }

    private void ProcessingTimer_Tick(object? sender, EventArgs e)
    {
        var elapsedTime = _operationStopwatch.Elapsed;
        ProcessingTimeValue.Text = elapsedTime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

        // Update read speed
        ReadSpeedValue?.Text = _diskMonitorService.GetCurrentReadSpeedFormatted();

        ReadSpeedDriveIndicator?.Text = _diskMonitorService.CurrentDriveLetter != null
            ? $"({_diskMonitorService.CurrentDriveLetter})"
            : "";

        // Update write speed
        WriteSpeedValue?.Text = _diskMonitorService.GetCurrentWriteSpeedFormatted();

        WriteSpeedDriveIndicator?.Text = _diskMonitorService.CurrentDriveLetter != null
            ? $"({_diskMonitorService.CurrentDriveLetter})"
            : "";

        // Show status message in status bar if disk speed is unavailable
        var statusMessage = _diskMonitorService.StatusMessage;
        if (!string.IsNullOrEmpty(statusMessage) && StatusTextBlock != null &&
            !statusMessage.Equals(StatusTextBlock.Text, StringComparison.Ordinal))
        {
            StatusTextBlock.Text = statusMessage;
        }
    }

    private void SetControlsState(bool enabled)
    {
        // Disable/Enable the navigation buttons in the header
        BtnNavConvert.IsEnabled = enabled;
        BtnNavTest.IsEnabled = enabled;
        BtnNavExplorer.IsEnabled = enabled;

        // Disable/Enable the entire settings area
        ControlsBorder.IsEnabled = enabled;

        // Toggle visibility of progress and cancel
        ProgressAreaGrid.IsVisible = !enabled;
        ProgressBar.IsVisible = !enabled;
        CancelButton.IsVisible = !enabled;

        if (enabled)
        {
            UpdateStatus("Ready.");
        }
    }

    private void ResetSummaryStats()
    {
        _uiTotalFiles = _uiSuccessCount = _uiFailedCount = _uiSkippedCount = 0;
        _invalidIsoErrorCount = 0;
        _totalProcessedFiles = 0;
        _failedFilePaths.Clear();

        UpdateSummaryStatsUi();

        // Reset Progress Bar to a clean state
        ProgressBar.Value = 0;
        ProgressBar.Maximum = 1;
        ProgressBar.IsIndeterminate = false;
        ProgressTextBlock.Text = "";
    }

    private void NavConvert_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConvertView.IsVisible = true;
        TestView.IsVisible = false;
        ExplorerHeaderView.IsVisible = false;

        ShowLogPanel();
        StatsPanel.IsVisible = true;

        UpdateNavigationButtonStyles(BtnNavConvert);
    }

    private void NavTest_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConvertView.IsVisible = false;
        TestView.IsVisible = true;
        ExplorerHeaderView.IsVisible = false;

        ShowLogPanel();
        StatsPanel.IsVisible = true;

        UpdateNavigationButtonStyles(BtnNavTest);
    }

    private void NavExplorer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConvertView.IsVisible = false;
        TestView.IsVisible = false;
        ExplorerHeaderView.IsVisible = true;

        HideLogPanel();
        StatsPanel.IsVisible = false;

        UpdateNavigationButtonStyles(BtnNavExplorer);
    }

    private GridLength _savedLogColumnWidth = new(1, GridUnitType.Star);
    private GridLength _savedSplitterColumnWidth = new(10);

    /// <summary>The splitter column of the main content grid (see MainWindow.axaml).</summary>
    private ColumnDefinition SplitterColumn => ContentGrid.ColumnDefinitions[1];

    /// <summary>The log column of the main content grid (see MainWindow.axaml).</summary>
    private ColumnDefinition LogColumn => ContentGrid.ColumnDefinitions[2];

    /// <summary>
    ///     Gives the explorer the full window width by collapsing the log column.
    /// </summary>
    private void HideLogPanel()
    {
        if (LogColumn.Width.Value > 0)
        {
            _savedLogColumnWidth = LogColumn.Width;
            _savedSplitterColumnWidth = SplitterColumn.Width;
        }

        LogColumn.MinWidth = 0;
        LogColumn.Width = new GridLength(0);
        SplitterColumn.Width = new GridLength(0);
        LogBorder.IsVisible = false;
    }

    /// <summary>
    ///     Restores the log column and splitter to their previous widths.
    /// </summary>
    private void ShowLogPanel()
    {
        LogColumn.MinWidth = 300;
        LogColumn.Width = _savedLogColumnWidth;
        SplitterColumn.Width = _savedSplitterColumnWidth;
        LogBorder.IsVisible = true;
    }

    private void UpdateNavigationButtonStyles(Button selectedButton)
    {
        // Reset all navigation buttons to default style
        BtnNavConvert.Theme = (ControlTheme?)this.FindResource("MenuButtonStyle");
        BtnNavTest.Theme = (ControlTheme?)this.FindResource("MenuButtonStyle");
        BtnNavExplorer.Theme = (ControlTheme?)this.FindResource("MenuButtonStyle");

        // Apply selected style to the active button
        selectedButton.Theme = (ControlTheme?)this.FindResource("SelectedMenuButtonStyle");
    }

    private bool _isPerformanceCounterStopped;

    private void StopPerformanceCounter()
    {
        if (_isPerformanceCounterStopped) return;

        _isPerformanceCounterStopped = true;

        _diskMonitorService.StopMonitoring();
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            ReadSpeedValue?.Text = "N/A";

            ReadSpeedDriveIndicator?.Text = "";

            WriteSpeedValue?.Text = "N/A";

            WriteSpeedDriveIndicator?.Text = "";
        });
    }

    private void UpdateStatus(string status)
    {
        StatusTextBlock?.Text = status;
    }

    private void SetCurrentOperationDrive(string? driveLetter)
    {
        _diskMonitorService.StartMonitoring(driveLetter);
    }
}