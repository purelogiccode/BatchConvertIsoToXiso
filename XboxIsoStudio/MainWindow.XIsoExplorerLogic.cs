using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Serilog;
using XboxIsoStudio.Interfaces;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;

namespace XboxIsoStudio;

public partial class MainWindow
{
    // Drag-drop state tracking
    private Point _dragStartPoint;
    private PointerPressedEventArgs? _dragPointerArgs;
    private bool _isDragging;

    private const double MinimumDragDistance = 4;

    private async void BrowseExplorerFile_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an Xbox image to explore",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Xbox images")
                        { Patterns = Options },
                    new FilePickerFileType("Xbox ISO") { Patterns = OptionsArray },
                    new FilePickerFileType("Compressed ISO") { Patterns = OptionsArray0 },
                    new FilePickerFileType("ZAR archive") { Patterns = OptionsArray1 },
                    new FilePickerFileType("CHD image") { Patterns = OptionsArray2 },
                    new FilePickerFileType("All files") { Patterns = OptionsArray3 }
                }
            });

            if (files.Count == 0) return;

            var selectedPath = files[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(selectedPath)) return;

            ExplorerFilePathTextBox.Text = selectedPath;
            InitializeExplorer(selectedPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in method BrowseExplorerFile_Click");
        }
    }

    private void InitializeExplorer(string imagePath)
    {
        try
        {
            lock (_explorerLock)
            {
                _explorer?.Dispose();
                _explorer = ImageExplorerFactory.Open(imagePath);
            }

            LoadDirectory("/");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read image: {ImagePath}", imagePath);
            _ = _messageBoxService.ShowErrorAsync($"Failed to read image: {ex.Message}");
        }
    }

    private void LoadDirectory(string internalPath)
    {
        IImageExplorer explorer;
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
                    Entry = e
                }).OrderByDescending(static i => i.IsDirectory)
                .ThenBy(static i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ExplorerDataGrid.ItemsSource = uiItems;
            _currentInternalPath = ImagePaths.Normalize(internalPath);
            UpdateExplorerUiState();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading directory: {InternalPath}", internalPath);
            _ = _messageBoxService.ShowErrorAsync($"Error loading directory: {ex.Message}");
        }
    }

    private void UpdateExplorerUiState()
    {
        ExplorerUpButton.IsEnabled = !string.Equals(_currentInternalPath, "/", StringComparison.Ordinal);
        ExplorerPathTextBlock.Text = _currentInternalPath;
    }

    private async void ExplorerDataGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        try
        {
            if (ExplorerDataGrid.SelectedItem is not XisoExplorerItem item) return;

            if (item.IsDirectory)
            {
                LoadDirectory(item.Entry.FullPath);
            }
            else
            {
                // Open the file with the default application
                await OpenFileFromImage(item.Entry, item.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in method ExplorerDataGrid_DoubleTapped");
        }
    }

    private async Task OpenFileFromImage(ImageEntry entry, string fileName)
    {
        await Task.Run(async () =>
        {
            try
            {
                var tempFolder = ResolveExplorerTempDirectory(entry.Size, "ImageExplorer");
                Directory.CreateDirectory(tempFolder);
                var tempPath = Path.Combine(tempFolder, fileName);

                // Extract file to temp location
                IImageExplorer explorer;
                lock (_explorerLock)
                {
                    if (_explorer == null) return;
                    explorer = _explorer;
                }

                explorer.CopyOut(entry.FullPath, tempPath);

                // Open with default application on UI thread
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to open extracted file: {TempPath}", tempPath);
                        _ = _messageBoxService.ShowErrorAsync($"Failed to open file: {ex.Message}");
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
                _logger.Error(ex, "Failed to extract and open file from image: {FileName}", fileName);
                await Dispatcher.UIThread.InvokeAsync(() =>
                    _ = _messageBoxService.ShowErrorAsync($"Failed to extract and open file: {ex.Message}"));
            }
        }, _cts.Token);
    }

    private void ExplorerDataGrid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _dragPointerArgs = e;
    }

    private async void ExplorerDataGrid_PointerMovedAsync(object? sender, PointerEventArgs e)
    {
        try
        {
            if (_isDragging || _dragPointerArgs is null) return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            var currentPosition = e.GetPosition(null);
            var diff = _dragStartPoint - currentPosition;

            // Check if the pointer has moved enough to start a drag operation
            if (Math.Abs(diff.X) < MinimumDragDistance && Math.Abs(diff.Y) < MinimumDragDistance) return;

            // Get selected file items (not directories)
            var selectedItems = ExplorerDataGrid.SelectedItems?
                .Cast<XisoExplorerItem>()
                .Where(static i => !i.IsDirectory)
                .ToList();

            if (selectedItems is not { Count: > 0 }) return;

            try
            {
                _isDragging = true;
                // Extract files to temp folder for drag operation
                var totalSize = selectedItems.Sum(static i => i.Entry.Size);
                var tempFolder = ResolveExplorerTempDirectory(totalSize, "ImageExplorer_DragDrop");
                Directory.CreateDirectory(tempFolder);

                var tempFiles = new List<string>();

                // Perform extraction asynchronously to avoid UI freeze
                await Task.Run(() =>
                {
                    IImageExplorer explorer;
                    lock (_explorerLock)
                    {
                        if (_explorer == null) return;
                        explorer = _explorer;
                    }

                    foreach (var item in selectedItems)
                    {
                        var tempPath = Path.Combine(tempFolder, item.Name);
                        explorer.CopyOut(item.Entry.FullPath, tempPath);
                        tempFiles.Add(tempPath);
                    }
                });

                // Start drag operation with the file drop list
                var topLevel = GetTopLevel(this);
                if (topLevel is not null && tempFiles.Count > 0)
                {
                    var data = new DataTransfer();
                    var addedCount = 0;
                    foreach (var tempFile in tempFiles)
                    {
                        var storageItem = await topLevel.StorageProvider.TryGetFileFromPathAsync(tempFile);
                        if (storageItem is not null)
                        {
                            data.Add(DataTransferItem.CreateFile(storageItem));
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        await DragDrop.DoDragDropAsync(_dragPointerArgs, data, DragDropEffects.Copy);
                    }
                }

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
                _ = _messageBoxService.ShowErrorAsync($"Failed to prepare files for drag operation: {ex.Message}");
            }
            finally
            {
                _isDragging = false;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Drag operation failed");
            _ = _messageBoxService.ShowErrorAsync($"Drag operation failed: {ex.Message}");
        }
    }

    private static readonly string[] OptionsArray3 = new[] { "*" };
    private static readonly string[] OptionsArray2 = new[] { "*.chd" };
    private static readonly string[] OptionsArray1 = new[] { "*.zar" };
    private static readonly string[] OptionsArray0 = new[] { "*.cso" };
    private static readonly string[] OptionsArray = new[] { "*.iso" };
    private static readonly string[] Options = new[] { "*.iso", "*.cso", "*.zar", "*.chd" };

    private void ExplorerUpButton_Click(object? sender, RoutedEventArgs e)
    {
        if (string.Equals(_currentInternalPath, "/", StringComparison.Ordinal)) return;

        LoadDirectory(ImagePaths.GetParent(_currentInternalPath));
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