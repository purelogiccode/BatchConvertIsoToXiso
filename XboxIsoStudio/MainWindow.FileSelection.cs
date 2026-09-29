using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    private readonly ObservableCollection<FileItem> _conversionFiles = new();
    private readonly ObservableCollection<FileItem> _testFiles = new();

    // A scan generation per file list: a refresh only mutates its list while it is still
    // the newest scan for that list, so an older, slower scan cannot clear or repopulate
    // the list after a newer one (e.g. when the user changes the source folder quickly).
    private readonly Lock _fileListRefreshLock = new();
    private readonly Dictionary<ObservableCollection<FileItem>, int> _fileListRefreshGeneration = new();

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
        int generation;
        lock (_fileListRefreshLock)
        {
            generation = _fileListRefreshGeneration.TryGetValue(target, out var current) ? current + 1 : 1;
            _fileListRefreshGeneration[target] = generation;
        }

        try
        {
            if (string.IsNullOrEmpty(inputFolder) || !Directory.Exists(inputFolder))
            {
                if (IsFileListRefreshCurrent(target, generation)) target.Clear();
                return;
            }

            var files = await Task.Run(() =>
            {
                var enumerationOptions = new EnumerationOptions
                {
                    RecurseSubdirectories = searchSubfolders,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.System | FileAttributes.Hidden
                };

                return Directory.GetFiles(inputFolder, "*.*", enumerationOptions)
                    .Where(filter)
                    .Select(file => new FileItem
                    {
                        FileName = Path.GetRelativePath(inputFolder, file),
                        FullPath = file,
                        FileSize = GetFileSizeSafe(file)
                    })
                    .ToList();
            });

            if (!IsFileListRefreshCurrent(target, generation)) return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsFileListRefreshCurrent(target, generation)) target.Clear();
            });

            const int chunkSize = 100;
            for (var i = 0; i < files.Count; i += chunkSize)
            {
                if (!IsFileListRefreshCurrent(target, generation)) return;

                var chunk = files.Skip(i).Take(chunkSize).ToList();
                await Dispatcher.UIThread.InvokeAsync(
                    () =>
                    {
                        // Re-check on the UI thread: a newer scan may have started after the
                        // check above but before this callback ran.
                        if (!IsFileListRefreshCurrent(target, generation)) return;
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

    /// <summary>
    ///     Returns true when <paramref name="generation" /> is still the newest scan for
    ///     <paramref name="target" />; results from an older, slower scan must be discarded.
    /// </summary>
    private bool IsFileListRefreshCurrent(ObservableCollection<FileItem> target, int generation)
    {
        lock (_fileListRefreshLock)
        {
            return _fileListRefreshGeneration.TryGetValue(target, out var current) && current == generation;
        }
    }

    /// <summary>
    ///     Returns the file length, or 0 when the file vanished, is locked, or is otherwise
    ///     unreadable: one such file must not abort the whole folder scan.
    /// </summary>
    private long GetFileSizeSafe(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.Debug(ex, "Could not read the size of {FilePath}; treating it as 0", path);
            return 0;
        }
    }
}