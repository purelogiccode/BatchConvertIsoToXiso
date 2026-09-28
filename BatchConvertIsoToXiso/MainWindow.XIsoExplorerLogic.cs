using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BatchConvertIsoToXiso.Models;
using BatchConvertIsoToXiso.Services;
using Microsoft.Win32;
using XISOSharp;

namespace BatchConvertIsoToXiso;

public partial class MainWindow
{
    // Drag-drop state tracking
    private Point _dragStartPoint;
    private bool _isDragging;

    private void BrowseExplorerFile_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Xbox ISO files (*.iso)|*.iso|All files (*.*)|*.*",
            Title = "Select an Xbox ISO to explore"
        };

        if (openFileDialog.ShowDialog() != true) return;

        ExplorerFilePathTextBox.Text = openFileDialog.FileName;
        InitializeExplorer(openFileDialog.FileName);
    }

    private void InitializeExplorer(string isoPath)
    {
        try
        {
            lock (_explorerLock)
            {
                _explorer?.Dispose();
                _explorer = new XisoExplorer(isoPath, new XisoExplorerOptions { KeepOpen = true });
            }

            LoadDirectory("/");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read XISO: {IsoPath}", isoPath);
            _messageBoxService.ShowError($"Failed to read XISO: {ex.Message}");
        }
    }

    private void LoadDirectory(string internalPath)
    {
        XisoExplorer explorer;
        lock (_explorerLock)
        {
            if (_explorer == null) return;
            explorer = _explorer;
        }

        try
        {
            var entries = explorer.ListChildren(internalPath);
            var uiItems = entries.Select(static e => new XisoExplorerItem
                {
                    Name = e.Name,
                    IsDirectory = e.IsDirectory,
                    SizeFormatted = e.IsDirectory ? "" : Formatter.FormatBytes(e.Size),
                    Node = e
                }).OrderByDescending(static i => i.IsDirectory)
                .ThenBy(static i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ExplorerListView.ItemsSource = uiItems;
            _currentInternalPath = XisoExplorer.Normalize(internalPath);
            UpdateExplorerUiState();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading directory: {InternalPath}", internalPath);
            _messageBoxService.ShowError($"Error loading directory: {ex.Message}");
        }
    }

    private void UpdateExplorerUiState()
    {
        ExplorerUpButton.IsEnabled = !string.Equals(_currentInternalPath, "/", StringComparison.Ordinal);
        ExplorerPathTextBlock.Text = _currentInternalPath;
    }

    private async void ExplorerListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (ExplorerListView.SelectedItem is not XisoExplorerItem item) return;

            if (item.IsDirectory)
            {
                LoadDirectory(item.Node.FullPath);
            }
            else
            {
                // Open the file with the default application
                await OpenFileFromIso(item.Node, item.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method ExplorerListView_MouseDoubleClick");
        }
    }

    private async Task OpenFileFromIso(ExplorerNode node, string fileName)
    {
        await Task.Run(async () =>
        {
            try
            {
                var tempFolder = ResolveExplorerTempDirectory(node.Size, "XisoExplorer");
                Directory.CreateDirectory(tempFolder);
                var tempPath = Path.Combine(tempFolder, fileName);

                // Extract file to temp location
                XisoExplorer explorer;
                lock (_explorerLock)
                {
                    if (_explorer == null) return;
                    explorer = _explorer;
                }

                explorer.CopyOut(node.FullPath, tempPath);

                // Open with default application on UI thread
                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to open extracted file: {TempPath}", tempPath);
                        _messageBoxService.ShowError($"Failed to open file: {ex.Message}");
                    }
                });

                // Schedule delayed cleanup of temp file
                _ = Task.Run(async () =>
                {
                    await Task.Delay(30_000);
                    try
                    {
                        if (File.Exists(tempPath)) File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        /* in use */
                        _logger.Debug(cleanupEx, "Could not delete extracted temp file: {TempPath}", tempPath);
                    }

                    try
                    {
                        var dir = Path.GetDirectoryName(tempPath);
                        if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
                    }
                    catch (Exception cleanupEx)
                    {
                        /* ignore cleanup failures */
                        _logger.Debug(cleanupEx, "Could not delete extracted temp folder for: {TempPath}", tempPath);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract and open file from ISO: {FileName}", fileName);
                await Dispatcher.InvokeAsync(() =>
                {
                    _messageBoxService.ShowError($"Failed to extract and open file: {ex.Message}");
                });
            }
        }, _cts.Token);
    }

    private void ExplorerListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private async void ExplorerListView_MouseMoveAsync(object sender, MouseEventArgs e)
    {
        try
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint - currentPosition;

            // Check if mouse has moved enough to start a drag operation
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            // Get selected file items (not directories)
            var selectedItems = ExplorerListView.SelectedItems
                .Cast<XisoExplorerItem>()
                .Where(static i => !i.IsDirectory)
                .ToList();

            if (selectedItems.Count == 0) return;

            try
            {
                _isDragging = true;
                // Extract files to temp folder for drag operation
                var totalSize = selectedItems.Sum(static i => i.Node.Size);
                var tempFolder = ResolveExplorerTempDirectory(totalSize, "XisoExplorer_DragDrop");
                Directory.CreateDirectory(tempFolder);

                var tempFiles = new List<string>();

                // Perform extraction asynchronously to avoid UI freeze
                await Task.Run(() =>
                {
                    XisoExplorer explorer;
                    lock (_explorerLock)
                    {
                        if (_explorer == null) return;
                        explorer = _explorer;
                    }

                    foreach (var item in selectedItems)
                    {
                        var tempPath = Path.Combine(tempFolder, item.Name);
                        explorer.CopyOut(item.Node.FullPath, tempPath);
                        tempFiles.Add(tempPath);
                    }
                });

                // Start drag operation back on the UI thread
                var data = new DataObject(DataFormats.FileDrop, tempFiles.ToArray());
                DragDrop.DoDragDrop(ExplorerListView, data, DragDropEffects.Copy);

                // Cleanup temp files after drag operation completes
                try
                {
                    Directory.Delete(tempFolder, true);
                }
                catch (Exception cleanupEx)
                {
                    // Ignore cleanup errors
                    _logger.Debug(cleanupEx, "Could not delete drag-and-drop temp folder: {TempFolder}", tempFolder);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to prepare files for drag operation");
                _messageBoxService.ShowError($"Failed to prepare files for drag operation: {ex.Message}");
            }
            finally
            {
                _isDragging = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Drag operation failed");
            _messageBoxService.ShowError($"Drag operation failed: {ex.Message}");
        }
    }

    private void ExplorerUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(_currentInternalPath, "/", StringComparison.Ordinal)) return;

        LoadDirectory(GetParentPath(_currentInternalPath));
    }

    private static string GetParentPath(string internalPath)
    {
        var normalized = XisoExplorer.Normalize(internalPath);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }

    private void ExplorerListView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not ListView listView) return;

        var remainingWidth = listView.ActualWidth - ExplorerSizeColumn.Width - ExplorerTypeColumn.Width - 10;
        if (remainingWidth > 100)
        {
            ExplorerNameColumn.Width = remainingWidth;
        }
    }

    private string ResolveExplorerTempDirectory(long requiredSize, string tempSubfolder)
    {
        var defaultTempPath = Path.GetTempPath();
        var defaultTempDriveRoot = Path.GetPathRoot(defaultTempPath);
        var requiredWithBuffer = requiredSize + Math.Max(requiredSize / 10, 200L * 1024 * 1024);

        if (defaultTempDriveRoot != null)
        {
            try
            {
                var defaultDrive = new DriveInfo(defaultTempDriveRoot);
                if (defaultDrive.IsReady && defaultDrive.AvailableFreeSpace >= requiredWithBuffer)
                    return Path.Combine(defaultTempPath, tempSubfolder, Guid.NewGuid().ToString());
            }
            catch (Exception ex)
            {
                // Ignore and fall through to alternative search
                _logger.Debug(ex, "Could not inspect default temp drive: {TempDriveRoot}", defaultTempDriveRoot);
            }
        }

        var altDrive = _diskMonitorService.FindDriveWithFreeSpace(requiredSize, defaultTempDriveRoot);
        if (altDrive != null)
            return Path.Combine(altDrive, tempSubfolder, Guid.NewGuid().ToString());

        // Fall back to default even if space is low — let the operation attempt and fail with a clear error
        return Path.Combine(defaultTempPath, tempSubfolder, Guid.NewGuid().ToString());
    }
}