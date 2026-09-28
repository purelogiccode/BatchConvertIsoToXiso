using System.IO;
using System.Windows;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;

namespace XboxIsoStudio;

public partial class MainWindow
{
    private void BrowseConversionInputButton_Click(object sender, RoutedEventArgs e)
    {
        var inputFolder = SelectFolder("Select the folder containing ISO or archive files");
        if (string.IsNullOrEmpty(inputFolder)) return;

        if (CheckForTempPath.IsSystemTempPath(inputFolder))
        {
            _messageBoxService.ShowError(
                "The system's temporary folder or a subfolder within it cannot be selected as an input folder. Please choose a different location.");
            return;
        }

        ConversionInputFolderTextBox.Text = inputFolder;
        _ = RefreshConversionFileListAsync();
    }

    private void BrowseConversionOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var outputFolder = SelectFolder("Select the output folder for converted files");
        if (string.IsNullOrEmpty(outputFolder)) return;

        if (CheckForTempPath.IsSystemTempPath(outputFolder))
        {
            _messageBoxService.ShowError(
                "The system's temporary folder or a subfolder within it cannot be selected as an output folder. Please choose a different location.");
            return;
        }

        ConversionOutputFolderTextBox.Text = outputFolder;
    }

    private void BrowseTestInputButton_Click(object sender, RoutedEventArgs e)
    {
        var inputFolder = SelectFolder("Select the folder containing ISO, CSO, or ZAR files to test");
        if (string.IsNullOrEmpty(inputFolder)) return;

        if (CheckForTempPath.IsSystemTempPath(inputFolder))
        {
            _messageBoxService.ShowError(
                "The system's temporary folder or a subfolder within it cannot be selected as an input folder for testing. Please choose a different location.");
            return;
        }

        TestInputFolderTextBox.Text = inputFolder;
        _ = RefreshTestFileListAsync();
    }

    private async void StartConversionButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isOperationRunning) return;

            _isOperationRunning = true;
            _operationCompletedTcs = new TaskCompletionSource();
            SetControlsState(false);
            LogViewer.Clear();
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
                _messageBoxService.ShowError("Please select both input and output folders for conversion.");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(inputFolder))
            {
                _messageBoxService.ShowError($"The input folder no longer exists:\n{inputFolder}");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(outputFolder))
            {
                _messageBoxService.ShowError($"The output folder no longer exists:\n{outputFolder}");
                FinalizeUiState();
                return;
            }

            if (!ValidateInputOutputFolders(inputFolder, outputFolder))
            {
                FinalizeUiState();
                return;
            }

            var selectedFiles = GetSelectedConversionFiles();
            if (selectedFiles.Count == 0)
            {
                _messageBoxService.ShowError(
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
                        _invalidIsoErrorCount += p.FailedCount.Value;
                        UpdateSummaryStatsUi();
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
                    : OutputFormat.Cso;

            await _orchestratorService.ConvertFilesAsync(
                selectedFiles, outputFolder,
                DeleteOriginalsCheckBox.IsChecked ?? false,
                SkipSystemUpdateCheckBox.IsChecked ?? false,
                CheckOutputIntegrityCheckBox.IsChecked ?? false,
                outputFormat,
                progress, HandleCloudRetryRequestAsync, _cts.Token);
        }
        catch (OperationCanceledException)
        {
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
            FinalizeUiState();
            await LogOperationSummaryAsync("Conversion");
            await RefreshConversionFileListAsync();
        }
    }

    private async void StartTestButton_ClickAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isOperationRunning) return;

            _isOperationRunning = true;
            _operationCompletedTcs = new TaskCompletionSource();
            SetControlsState(false);
            LogViewer.Clear();
            ResetSummaryStats();

            // Immediate visual feedback while the background thread scans the filesystem
            ProgressBar.IsIndeterminate = true;
            ProgressTextBlock.Text = "Preparing integrity test...";

            UpdateStatus("Cleaning up temporary files...");
            await PreOperationCleanupAsync();

            var inputFolder = TestInputFolderTextBox.Text;
            if (string.IsNullOrEmpty(inputFolder))
            {
                _messageBoxService.ShowError("Please select the input folder for testing.");
                FinalizeUiState();
                return;
            }

            if (!Directory.Exists(inputFolder))
            {
                _messageBoxService.ShowError($"The input folder no longer exists:\n{inputFolder}");
                FinalizeUiState();
                return;
            }

            var selectedFiles = GetSelectedTestFiles();
            if (selectedFiles.Count == 0)
            {
                _messageBoxService.ShowError(
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
                        _invalidIsoErrorCount += p.FailedCount.Value;
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
            UpdateStatus("Starting batch image test...");

            await _orchestratorService.TestFilesAsync(
                inputFolder, selectedFiles,
                MoveSuccessFilesCheckBox.IsChecked == true,
                MoveFailedFilesCheckBox.IsChecked == true,
                PerformDeepScanCheckBox.IsChecked ?? false,
                progress, HandleCloudRetryRequestAsync, _cts.Token);
        }
        catch (OperationCanceledException)
        {
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
            FinalizeUiState();
            await LogOperationSummaryAsync("Test");
            await RefreshTestFileListAsync();
        }
    }
}