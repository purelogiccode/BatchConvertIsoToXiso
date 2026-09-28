using System.Collections.ObjectModel;
using System.IO;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XboxIsoStudio.Models;
using XboxIsoStudio.Services;

namespace XboxIsoStudio;

public partial class MainWindow
{
    private readonly ObservableCollection<FileItem> _conversionFiles = new();
    private readonly ObservableCollection<FileItem> _testFiles = new();

    /// <summary>
    ///     Binds the selectable file lists to their DataGrids. Called once from the constructor.
    /// </summary>
    private void InitializeFileLists()
    {
        ConversionFilesDataGrid.ItemsSource = _conversionFiles;
        TestFilesDataGrid.ItemsSource = _testFiles;
    }

    private void SearchSubfoldersCheckBox_Changed(object? sender, RoutedEventArgs e)
    {
        if (!_isUiInitialized) return;

        if (ReferenceEquals(sender, SearchSubfoldersConversionCheckBox))
        {
            _ = RefreshConversionFileListAsync();
        }
        else if (ReferenceEquals(sender, SearchSubfoldersTestCheckBox))
        {
            _ = RefreshTestFileListAsync();
        }
    }

    private void SelectAllConversion_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var file in _conversionFiles) file.IsSelected = true;
    }

    private void DeselectAllConversion_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var file in _conversionFiles) file.IsSelected = false;
    }

    private void SelectAllTest_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var file in _testFiles) file.IsSelected = true;
    }

    private void DeselectAllTest_Click(object? sender, RoutedEventArgs e)
    {
        foreach (var file in _testFiles) file.IsSelected = false;
    }

    private IReadOnlyList<string> GetSelectedConversionFiles()
    {
        return _conversionFiles.Where(static f => f.IsSelected).Select(static f => f.FullPath).ToList();
    }

    private IReadOnlyList<string> GetSelectedTestFiles()
    {
        return _testFiles.Where(static f => f.IsSelected).Select(static f => f.FullPath).ToList();
    }

    private Task RefreshConversionFileListAsync()
    {
        return LoadFileListAsync(ConversionInputFolderTextBox.Text,
            SearchSubfoldersConversionCheckBox.IsChecked == true, _conversionFiles, SupportedFiles.IsConvertible,
            "conversion");
    }

    private Task RefreshTestFileListAsync()
    {
        return LoadFileListAsync(TestInputFolderTextBox.Text, SearchSubfoldersTestCheckBox.IsChecked == true,
            _testFiles, SupportedFiles.IsTestable, "testing");
    }

    /// <summary>
    ///     Scans the input folder and repopulates the given list with every supported file,
    ///     adding items in small chunks so the UI stays responsive with thousands of files.
    /// </summary>
    private async Task LoadFileListAsync(string? inputFolder, bool searchSubfolders,
        ObservableCollection<FileItem> target, Func<string, bool> filter, string purpose)
    {
        try
        {
            if (string.IsNullOrEmpty(inputFolder) || !Directory.Exists(inputFolder))
            {
                target.Clear();
                return;
            }

            var files = await Task.Run(() =>
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = searchSubfolders,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                };

                return Directory.GetFiles(inputFolder, "*.*", options)
                    .Where(filter)
                    .Select(file => new FileItem
                    {
                        FileName = Path.GetRelativePath(inputFolder, file),
                        FullPath = file,
                        FileSize = new FileInfo(file).Length
                    })
                    .ToList();
            });

            await Dispatcher.UIThread.InvokeAsync(target.Clear);

            const int chunkSize = 100;
            for (var i = 0; i < files.Count; i += chunkSize)
            {
                var chunk = files.Skip(i).Take(chunkSize).ToList();
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        foreach (var item in chunk) target.Add(item);
                    },
                    DispatcherPriority.Background);
            }

            _logger.Information("Loaded {FileCount} file(s) for {Purpose} from {InputFolder}", files.Count, purpose,
                inputFolder);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Could not load the file list for {Purpose} from {InputFolder}", purpose, inputFolder);
        }
    }
}
