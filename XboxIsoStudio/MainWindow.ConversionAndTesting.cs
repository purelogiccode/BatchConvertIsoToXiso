using System.Diagnostics.CodeAnalysis;
using Avalonia.Interactivity;
using Serilog;
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
    private async void BrowseConversionInputButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var inputFolder = await SelectFolderAsync("Select the folder containing ISO or archive files");
            if (string.IsNullOrEmpty(inputFolder)) return;

            if (CheckForTempPath.IsSystemTempPath(inputFolder))
            {
                await _messageBoxService.ShowErrorAsync(
                    "The system's temporary folder or a subfolder within it cannot be selected as an input folder. Please choose a different location.");
                return;
            }

            ConversionInputFolderTextBox.Text = inputFolder;
            _ = RefreshConversionFileListAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method BrowseConversionInputButton_Click");
        }
    }

    private async void BrowseConversionOutputButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var outputFolder = await SelectFolderAsync("Select the output folder for converted files");
            if (string.IsNullOrEmpty(outputFolder)) return;

            if (CheckForTempPath.IsSystemTempPath(outputFolder))
            {
                await _messageBoxService.ShowErrorAsync(
                    "The system's temporary folder or a subfolder within it cannot be selected as an output folder. Please choose a different location.");
                return;
            }

            ConversionOutputFolderTextBox.Text = outputFolder;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method BrowseConversionOutputButton_Click");
        }
    }

    private async void BrowseTestInputButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var inputFolder =
                await SelectFolderAsync("Select the folder containing ISO, CSO, ZAR, or CHD files to test");
            if (string.IsNullOrEmpty(inputFolder)) return;

            if (CheckForTempPath.IsSystemTempPath(inputFolder))
            {
                await _messageBoxService.ShowErrorAsync(
                    "The system's temporary folder or a subfolder within it cannot be selected as an input folder for testing. Please choose a different location.");
                return;
            }

            TestInputFolderTextBox.Text = inputFolder;
            _ = RefreshTestFileListAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method BrowseTestInputButton_Click");
        }
    }

    private async void StartConversionButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        // Guard outside the try: the finally block tears the operation down, so an early
        // return from inside it would finish the operation that is already running.
        if (_isOperationRunning) return;

        var operationStarted = false;
        var operationCanceled = false;

        try
        {
            _isOperationRunning = true;
            _operationCompletedTcs = new TaskCompletionSource();
            SetControlsState(false);
            LogViewer.Text = string.Empty;
            ResetSummaryStats();

            // Immediate visual feedback while the background thread scans the filesystem
            ProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = "Preparing conversion...";

            UpdateStatus("Cleaning up temporary files...");
            await PreOperationCleanupAsync();

            var inputFolder = ConversionInputFolderTextBox.Text;
            var outputFolder = ConversionOutputFolderTextBox.Text;

            if (string.IsNullOrEmpty(inputFolder) || string.IsNullOrEmpty(outputFolder))
            {
                await _messageBoxService.ShowErrorAsync("Please select both input and output folders for conversion.");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(inputFolder))
            {
                await _messageBoxService.ShowErrorAsync($"The input folder no longer exists:\n{inputFolder}");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(outputFolder))
            {
                await _messageBoxService.ShowErrorAsync($"The output folder no longer exists:\n{outputFolder}");
                FinalizeUiState();
                return;
            }

            if (!await ValidateInputOutputFoldersAsync(inputFolder, outputFolder))
            {
                FinalizeUiState();
                return;
            }

            var selectedFiles = GetSelectedConversionFiles();
            if (selectedFiles.Count == 0)
            {
                await _messageBoxService.ShowErrorAsync(
                    "No files selected for conversion. Select a source folder and tick at least one file in the list.");
                FinalizeUiState();
                return;
            }

            var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            try
            {
                oldCts.Dispose();
            }
            catch (Exception ex)
            {
                /* Already disposed by CleanupResources */
                _logger.Debug(ex, "Cancellation token source was already disposed");
            }

            var progress = new Progress<BatchOperationProgress>(p =>
            {
                try
                {
                    if (p.LogMessage != null) _logger.Information("{Message:l}", p.LogMessage);
                    if (p.StatusText != null) UpdateStatus(p.StatusText);

                    if (p.TotalFiles.HasValue)
                    {
                        _uiTotalFiles = p.TotalFiles.Value;
                        UpdateSummaryStatsUi();
                    }

                    if (p.ProcessedCount.HasValue)
                    {
                        UpdateProgressUi(p.ProcessedCount.Value, _uiTotalFiles);
                    }

                    if (p.SuccessCount.HasValue)
                    {
                        _uiSuccessCount += p.SuccessCount.Value;
                        _totalProcessedFiles += p.SuccessCount.Value;
                        UpdateSummaryStatsUi();
                    }

                    if (p.FailedCount.HasValue)
                    {
                        _uiFailedCount += p.FailedCount.Value;
                        _totalProcessedFiles += p.FailedCount.Value;
                        UpdateSummaryStatsUi();
                    }

                    // Only genuinely invalid images count toward the "not valid Xbox ISOs" warning.
                    if (p.InvalidIsoCount.HasValue)
                    {
                        _invalidIsoErrorCount += p.InvalidIsoCount.Value;
                    }

                    if (p.SkippedCount.HasValue)
                    {
                        _uiSkippedCount += p.SkippedCount.Value;
                        _totalProcessedFiles += p.SkippedCount.Value;
                        UpdateSummaryStatsUi();
                    }

                    if (p.CurrentDrive != null) SetCurrentOperationDrive(p.CurrentDrive);
                    if (p.FailedPathToAdd != null) _failedFilePaths.Add(p.FailedPathToAdd);

                    if (p.TotalFiles.HasValue || p.ProcessedCount.HasValue)
                    {
                        ProgressBar.IsIndeterminate = false;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // Ignore cancellation exceptions during UI updates
                    _logger.Debug(ex, "Ignoring cancellation exception during progress UI update");
                }
            });

            _operationStopwatch.Restart();
            _processingTimer.Start();
            _memoryTimer.Start();
            UpdateStatus("Starting batch conversion...");

            var outputFormat = OutputFormatXisoRadio.IsChecked == true
                ? OutputFormat.Xiso
                : OutputFormatZarRadio.IsChecked == true
                    ? OutputFormat.Zar
                    : OutputFormatCsoRadio.IsChecked == true
                        ? OutputFormat.Cso
                        : OutputFormat.Chd;

            operationStarted = true;
            await _orchestratorService.ConvertFilesAsync(
                selectedFiles, outputFolder,
                DeleteOriginalsCheckBox.IsChecked ?? false,
                SkipSystemUpdateCheckBox.IsChecked ?? false,
                CheckOutputIntegrityCheckBox.IsChecked ?? false,
                outputFormat,
                progress, HandleCloudRetryRequestAsync, _cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            operationCanceled = true;
            _logger.Information(ex, "Batch conversion canceled by the user");
            UpdateStatus("Operation canceled.");
        }
        catch (Exception ex)
        {
            // Environmental errors (disk space, network, disconnected drives) are not
            // application bugs — the user already gets a clear message from the orchestrator.
            if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) ||
                PathHelper.IsDeviceIoError(ex))
            {
                _logger.Information(ex, "Batch conversion stopped due to an environmental error");
            }
            else
            {
                _logger.Error(ex, "Critical error during batch conversion");
            }
        }
        finally
        {
            await FinishOperationAsync("Conversion", operationStarted, operationCanceled);
            await RefreshConversionFileListAsync();
        }
    }

    private async void StartTestButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        // Guard outside the try: the finally block tears the operation down, so an early
        // return from inside it would finish the operation that is already running.
        if (_isOperationRunning) return;

        var operationStarted = false;
        var operationCanceled = false;

        try
        {
            _isOperationRunning = true;
            _operationCompletedTcs = new TaskCompletionSource();
            SetControlsState(false);
            LogViewer.Text = string.Empty;
            ResetSummaryStats();

            // Immediate visual feedback while the background thread scans the filesystem
            ProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = "Preparing integrity test...";

            UpdateStatus("Cleaning up temporary files...");
            await PreOperationCleanupAsync();

            var inputFolder = TestInputFolderTextBox.Text;
            if (string.IsNullOrEmpty(inputFolder))
            {
                await _messageBoxService.ShowErrorAsync("Please select the input folder for testing.");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(inputFolder))
            {
                await _messageBoxService.ShowErrorAsync($"The input folder no longer exists:\n{inputFolder}");
                FinalizeUiState();
                return;
            }

            var selectedFiles = GetSelectedTestFiles();
            if (selectedFiles.Count == 0)
            {
                await _messageBoxService.ShowErrorAsync(
                    "No files selected for testing. Select an image folder and tick at least one file in the list.");
                FinalizeUiState();
                return;
            }

            var oldCts = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
            try
            {
                oldCts.Dispose();
            }
            catch (Exception ex)
            {
                /* Already disposed by CleanupResources */
                _logger.Debug(ex, "Cancellation token source was already disposed");
            }

            var progress = new Progress<BatchOperationProgress>(p =>
            {
                try
                {
                    if (p.LogMessage != null) _logger.Information("{Message:l}", p.LogMessage);
                    if (p.StatusText != null) UpdateStatus(p.StatusText);

                    if (p.TotalFiles.HasValue)
                    {
                        _uiTotalFiles = p.TotalFiles.Value;
                        UpdateSummaryStatsUi();
                    }

                    if (p.ProcessedCount.HasValue)
                    {
                        UpdateProgressUi(p.ProcessedCount.Value, _uiTotalFiles);
                    }

                    if (p.SuccessCount.HasValue)
                    {
                        _uiSuccessCount += p.SuccessCount.Value;
                        _totalProcessedFiles += p.SuccessCount.Value;
                        UpdateSummaryStatsUi();
                    }

                    if (p.FailedCount.HasValue)
                    {
                        _uiFailedCount += p.FailedCount.Value;
                        _totalProcessedFiles += p.FailedCount.Value;
                        UpdateSummaryStatsUi();
                    }

                    // Only genuinely invalid images count toward the "not valid Xbox ISOs" warning.
                    if (p.InvalidIsoCount.HasValue)
                    {
                        _invalidIsoErrorCount += p.InvalidIsoCount.Value;
                    }

                    if (p.CurrentDrive != null) SetCurrentOperationDrive(p.CurrentDrive);
                    if (p.FailedPathToAdd != null) _failedFilePaths.Add(p.FailedPathToAdd);

                    if (p.TotalFiles.HasValue || p.ProcessedCount.HasValue)
                    {
                        ProgressBar.IsIndeterminate = false;
                    }
                }
                catch (OperationCanceledException ex)
                {
                    // Ignore cancellation exceptions during UI updates
                    _logger.Debug(ex, "Ignoring cancellation exception during progress UI update");
                }
            });

            _operationStopwatch.Restart();
            _processingTimer.Start();
            _memoryTimer.Start();
            UpdateStatus("Starting batch image test...");

            operationStarted = true;
            await _orchestratorService.TestFilesAsync(
                inputFolder, selectedFiles,
                MoveSuccessFilesCheckBox.IsChecked == true,
                MoveFailedFilesCheckBox.IsChecked == true,
                PerformDeepScanCheckBox.IsChecked ?? false,
                progress, HandleCloudRetryRequestAsync, _cts.Token);
        }
        catch (OperationCanceledException ex)
        {
            operationCanceled = true;
            _logger.Information(ex, "Batch integrity test canceled by the user");
            UpdateStatus("Operation canceled.");
        }
        catch (Exception ex)
        {
            // Environmental errors (disk space, network, disconnected drives) are not
            // application bugs — the user already gets a clear message from the orchestrator.
            if (PathHelper.IsDiskSpaceError(ex) || PathHelper.IsNetworkError(ex) ||
                PathHelper.IsDeviceIoError(ex))
            {
                _logger.Information(ex, "Batch test stopped due to an environmental error");
            }
            else
            {
                _logger.Error(ex, "Critical error during batch test");
            }
        }
        finally
        {
            await FinishOperationAsync("Test", operationStarted, operationCanceled);
            await RefreshTestFileListAsync();
        }
    }
}