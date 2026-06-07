using System;
using EchoDeck.App.Models;
using EchoDeck.App.Services;
using EchoDeck.App.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;

namespace EchoDeck.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private bool _isCapturingHotkey;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SettingsWindow_SourceInitialized(object sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private const int ResizeBorder = 6;
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCLIENT = 1;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            var pt = new System.Windows.Point((int)(lParam.ToInt64() & 0xFFFF), (int)((lParam.ToInt64() >> 16) & 0xFFFF));
            pt = PointFromScreen(pt);

            var width = ActualWidth;
            var height = ActualHeight;

            var onLeft = pt.X < ResizeBorder;
            var onRight = pt.X >= width - ResizeBorder;
            var onTop = pt.Y < ResizeBorder;
            var onBottom = pt.Y >= height - ResizeBorder;

            if (onLeft && onTop) { handled = true; return new IntPtr(HTTOPLEFT); }
            if (onLeft && onBottom) { handled = true; return new IntPtr(HTBOTTOMLEFT); }
            if (onRight && onTop) { handled = true; return new IntPtr(HTTOPRIGHT); }
            if (onRight && onBottom) { handled = true; return new IntPtr(HTBOTTOMRIGHT); }
            if (onLeft) { handled = true; return new IntPtr(HTLEFT); }
            if (onRight) { handled = true; return new IntPtr(HTRIGHT); }
            if (onTop) { handled = true; return new IntPtr(HTTOP); }
            if (onBottom) { handled = true; return new IntPtr(HTBOTTOM); }

            handled = false;
            return IntPtr.Zero;
        }

        return IntPtr.Zero;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveAsync();
        if (System.Windows.Application.Current is App app)
        {
            app.ApplyTheme(_viewModel.Theme);
            app.ApplyLanguage(_viewModel.Language);
            if (app.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                _viewModel.RefreshDeviceLists();
                mainViewModel.StatusText = "Settings saved";
            }

            // Re-register Stop All hotkey live
            var hotkeyService = app.GetService<HotkeyService>();
            if (hotkeyService is not null && app.MainWindow is Window mainWin)
            {
                var handle = new WindowInteropHelper(mainWin).Handle;
                if (handle != IntPtr.Zero)
                {
                    hotkeyService.Unregister(handle, "Stop All");
                    var stopAllGesture = HotkeyGesture.Parse(_viewModel.StopAllHotkey);
                    if (!string.IsNullOrWhiteSpace(stopAllGesture.Key))
                    {
                        hotkeyService.Register(handle, stopAllGesture, "Stop All");
                    }
                }
            }
        }
        Close();
    }

    private async void ResetSettings_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(this,
            "Reset all settings to defaults? This will overwrite your current settings.json.",
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;
        await _viewModel.ResetSettingsAsync();
    }

    private async void AddWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.AddWatchedFolderAsync();
    }

    private async void WatchedFolderPath_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel) return;
        if (e.Key != System.Windows.Input.Key.Enter ||
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0)
            return;

        e.Handled = true;
        await viewModel.AddWatchedFolderAsync();
    }

    private void OpenWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenSelectedWatchedFolder();
    }

    private void CopyWatchedFolderPath_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.CopySelectedWatchedFolderPath();
    }

    private void WatchedFoldersList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.OpenSelectedWatchedFolder();
    }

    private async void WatchedFoldersList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;

        if (e.Key == System.Windows.Input.Key.C && ctrl)
        {
            e.Handled = true;
            _viewModel.CopySelectedWatchedFolderPath();
            return;
        }

        if (e.Key == System.Windows.Input.Key.O && ctrl)
        {
            e.Handled = true;
            _viewModel.OpenSelectedWatchedFolder();
            return;
        }

        if (e.Key == System.Windows.Input.Key.Delete)
        {
            e.Handled = true;
            await _viewModel.RemoveWatchedFolderAsync();
            return;
        }

        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            _viewModel.OpenSelectedWatchedFolder();
        }
    }

    private void BrowseWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to add to watched folders",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = string.IsNullOrWhiteSpace(_viewModel.WatchedFolderPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : _viewModel.WatchedFolderPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _viewModel.WatchedFolderPath = dialog.SelectedPath;
            _viewModel.WatchedFoldersStatus = $"Selected folder: {dialog.SelectedPath}";
        }
    }

    private async void RemoveWatchedFolder_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RemoveWatchedFolderAsync();
    }

    private async void ScanWatchedFolders_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ScanWatchedFoldersAsync();
    }

    private async void ClearWatchedFolders_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ClearWatchedFoldersAsync();
    }

    private void RemovePerSoundHotkey_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RemoveSelectedPerSoundHotkey();
    }

    private void ClearPerSoundHotkeys_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearAllPerSoundHotkeys();
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.OpenLogsFolder();
    }

    private void ClearAudioCache_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            if (app?.GetService<AudioMetadataCacheService>() is AudioMetadataCacheService cache)
            {
                cache.Clear();
                _viewModel.WatchedFoldersStatus = "File metadata cache cleared.";
            }
        }
        catch
        {
            _viewModel.WatchedFoldersStatus = "Unable to clear file metadata cache.";
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            if (app?.GetService<Infrastructure.AppPaths>() is Infrastructure.AppPaths paths)
            {
                System.Diagnostics.Process.Start("explorer.exe", paths.DataFolder);
                _viewModel.WatchedFoldersStatus = $"Opened data folder: {paths.DataFolder}";
            }
        }
        catch
        {
            _viewModel.WatchedFoldersStatus = "Unable to open data folder.";
        }
    }

    private void ExportLibrary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var libraryService = app?.GetService<LibraryService>();
            if (libraryService is not null)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Library",
                    Filter = "JSON Files|*.json",
                    DefaultExt = "json",
                    FileName = "soundboard-library-export.json"
                };
                if (dialog.ShowDialog(this) == true)
                {
                    libraryService.ExportToJson(dialog.FileName);
                    _viewModel.WatchedFoldersStatus = $"Library exported to: {dialog.FileName}";
                }
            }
        }
        catch
        {
            _viewModel.WatchedFoldersStatus = "Unable to export library.";
        }
    }

    private async void ImportLibrary_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var libraryService = app?.GetService<LibraryService>();
            if (libraryService is not null)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Library",
                    Filter = "JSON Files|*.json",
                    DefaultExt = "json",
                    FileName = "soundboard-library-export.json"
                };
                if (dialog.ShowDialog(this) == true)
                {
                    var count = await libraryService.ImportFromJsonAsync(dialog.FileName);
                    var msg = count > 0
                        ? $"Imported {count} sound(s) from library."
                        : "No new sounds were imported (duplicates skipped).";
                    System.Windows.MessageBox.Show(this, msg, "Import Library",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    _viewModel.WatchedFoldersStatus = msg;
                }
            }
        }
        catch (Exception ex)
        {
            _viewModel.WatchedFoldersStatus = $"Unable to import library: {ex.Message}";
        }
    }

    private async void ExportSoundPack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var libraryService = app?.GetService<LibraryService>();
            var mainVm = (app?.MainWindow as Window)?.DataContext as MainViewModel;
            if (libraryService is null || mainVm is null) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Sound Pack",
                Filter = "ZIP Files|*.zip",
                DefaultExt = "zip",
                FileName = "sound-pack.zip"
            };
            if (dialog.ShowDialog(this) == true)
            {
                var count = await libraryService.ExportSoundPackAsync(dialog.FileName, mainVm.Sounds.ToList());
                _viewModel.WatchedFoldersStatus = count > 0
                    ? $"Exported {count} sound(s) to pack."
                    : "No sounds to export.";
            }
        }
        catch (Exception ex)
        {
            _viewModel.WatchedFoldersStatus = $"Export sound pack failed: {ex.Message}";
        }
    }

    private async void ImportSoundPack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var libraryService = app?.GetService<LibraryService>();
            var mainVm = (app?.MainWindow as Window)?.DataContext as MainViewModel;
            if (libraryService is null || mainVm is null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Sound Pack",
                Filter = "ZIP Files|*.zip",
                DefaultExt = "zip"
            };
            if (dialog.ShowDialog(this) == true)
            {
                var defaultCategory = mainVm.ImportCategory;
                var count = await libraryService.ImportSoundPackAsync(dialog.FileName, defaultCategory);
                var msg = count > 0
                    ? $"Imported {count} sound(s) from pack."
                    : "No new sounds were imported (duplicates skipped).";
                System.Windows.MessageBox.Show(this, msg, "Import Sound Pack",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _viewModel.WatchedFoldersStatus = msg;
                mainVm.RefreshCategories();
                mainVm.SoundsView.Refresh();
            }
        }
        catch (Exception ex)
        {
            _viewModel.WatchedFoldersStatus = $"Import sound pack failed: {ex.Message}";
        }
    }

    private void CaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        _isCapturingHotkey = true;
        _viewModel.HotkeyStatus = "Press a key for Stop All...";
        Focus();
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (_isCapturingHotkey && DataContext is SettingsViewModel viewModel)
        {
            e.Handled = true;
            _isCapturingHotkey = false;

            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;

            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
            {
                viewModel.HotkeyStatus = "Please press a key combination with a non-modifier key.";
                return;
            }

            var modifiers = new List<string>();
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                modifiers.Add("Ctrl");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0)
                modifiers.Add("Alt");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
                modifiers.Add("Shift");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0)
                modifiers.Add("Win");

            modifiers.Add(key.ToString());
            viewModel.StopAllHotkey = string.Join(" + ", modifiers);
            viewModel.HotkeyStatus = $"Stop All hotkey set to {string.Join(" + ", modifiers)}";
            return;
        }

        base.OnKeyDown(e);
    }
}
