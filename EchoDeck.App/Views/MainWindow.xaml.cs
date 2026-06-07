using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using EchoDeck.App.Models;
using EchoDeck.App.Services;
using EchoDeck.App.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;

namespace EchoDeck.App.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HotkeyService _hotkeyService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly SettingsService _settingsService;
    private readonly LibraryService _libraryService;
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _viewModel = viewModel;
        DataContext = viewModel;

        _hotkeyService = serviceProvider.GetRequiredService<HotkeyService>();
        _playbackService = serviceProvider.GetRequiredService<IAudioPlaybackService>();
        _settingsService = serviceProvider.GetRequiredService<SettingsService>();
        _libraryService = serviceProvider.GetRequiredService<LibraryService>();

        viewModel.StatusText = "Ready";
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeButton_Click(sender, e);
            return;
        }
        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
        SearchBox.Focus();
    }

    private void Window_SourceInitialized(object sender, EventArgs e)
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

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowContainer.CornerRadius = new CornerRadius(0);
            WindowContainer.Margin = new Thickness(0);
        }
        else
        {
            WindowContainer.CornerRadius = new CornerRadius(8);
            WindowContainer.Margin = new Thickness(0);
        }
    }

    private async void ImportSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import sound files",
                Filter = "Audio Files|*.mp3;*.wav;*.wma;*.aac;*.m4a;*.ogg;*.flac|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var category = string.IsNullOrWhiteSpace(_viewModel.ImportCategory) ? _settingsService.Current.DefaultCategoryId : _viewModel.ImportCategory;
                await _viewModel.AddImportedSoundsAsync(dialog.FileNames, category);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Import failed: {ex.Message}";
        }
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder to import sounds from",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var category = string.IsNullOrWhiteSpace(_viewModel.ImportCategory) ? _settingsService.Current.DefaultCategoryId : _viewModel.ImportCategory;
                await _viewModel.AddImportedSoundsAsync([dialog.SelectedPath], category);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Import failed: {ex.Message}";
        }
    }

    private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedItems = CardList.SelectedItems.Cast<object>()
                .Concat(SoundListView.SelectedItems.Cast<object>())
                .OfType<SoundItemViewModel>()
                .Distinct()
                .ToList();

            if (selectedItems.Count == 0)
                return;

            var msg = selectedItems.Count == 1
                ? $"Remove '{selectedItems[0].Name}' from library? Files will stay on disk."
                : $"Remove {selectedItems.Count} sound(s) from library? Files will stay on disk.";

            var result = System.Windows.MessageBox.Show(this, msg, "Remove from Library", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.No)
                return;

            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            foreach (var sound in selectedItems)
            {
                _hotkeyService.Unregister(handle, sound.Id);
                _settingsService.Current.PerSoundHotkeys.Remove(sound.Id);
                _viewModel.Sounds.Remove(sound);
            }

            await _settingsService.SaveAsync();
            await _libraryService.SaveAsync();
            _viewModel.RefreshCategories();
            _viewModel.SoundsView.Refresh();
            _viewModel.StatusText = $"Removed {selectedItems.Count} sound(s) from library.";
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Delete failed: {ex.Message}";
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                return;

            if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] droppedPaths || droppedPaths.Length == 0)
                return;

            var category = string.IsNullOrWhiteSpace(_viewModel.ImportCategory) ? _settingsService.Current.DefaultCategoryId : _viewModel.ImportCategory;
            await _viewModel.AddImportedSoundsAsync(droppedPaths, category);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Import failed: {ex.Message}";
        }
    }

    private async void StopAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.StopAllPlaybackAsync();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Stop all failed: {ex.Message}";
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox { SelectedItem: string category })
        {
            _viewModel.SetFilter(category);
        }
    }

    private async void RandomSound_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.PlayRandomSoundAsync();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Random play failed: {ex.Message}";
        }
    }

    private void AllSounds_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LibraryFilter = "All";
        _viewModel.SelectedCategoryFilter = "All Sounds";
        _viewModel.StatusText = "Showing all sounds";
    }

    private void Favorites_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LibraryFilter = "Favorites";
        _viewModel.StatusText = "Showing favorites";
    }

    private void CategoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Content: string category })
        {
            _viewModel.SetFilter(category);
        }
    }

    private void ManageCategories_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CategoryManageDialog(
            _serviceProvider.GetRequiredService<CategoryService>(),
            _serviceProvider.GetRequiredService<LibraryService>(),
            _serviceProvider.GetRequiredService<MainViewModel>());
        dialog.Owner = this;
        dialog.ShowDialog();
        _viewModel.RefreshCategories();
    }

    private void SelectMode_Click(object sender, RoutedEventArgs e)
    {
        CardList.UnselectAll();
        SoundListView.UnselectAll();
        _viewModel.EnterSelectionMode();
    }

    private void CancelSelection_Click(object sender, RoutedEventArgs e)
    {
        CardList.UnselectAll();
        SoundListView.UnselectAll();
        _viewModel.ExitSelectionMode();
    }

    private async void BulkDelete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = _viewModel.SelectedForBulkAction.ToList();
            if (selected.Count == 0)
                return;

            var message = $"Remove {selected.Count} sound(s) from library? Files will stay on disk.";
            var result = System.Windows.MessageBox.Show(this, message, "Remove from Library", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            foreach (var sound in selected)
            {
                _hotkeyService.Unregister(handle, sound.Id);
                _settingsService.Current.PerSoundHotkeys.Remove(sound.Id);
            }

            await _settingsService.SaveAsync();
            await _viewModel.RemoveBulkSelectedSoundsAsync();
            CardList.UnselectAll();
            SoundListView.UnselectAll();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Bulk delete failed: {ex.Message}";
        }
    }

    private void SoundItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(sender as UIElement);

        if (!_viewModel.IsSelectionMode)
            return;

        if (FindAncestorDataContext<SoundItemViewModel>(e.OriginalSource as DependencyObject) is { } sound)
        {
            _viewModel.ToggleBulkSelection(sound);
            e.Handled = true;
        }
    }

    private async void SoundCategory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox { DataContext: SoundItemViewModel sound, SelectedItem: string category } combo)
            return;

        if (!combo.IsDropDownOpen && !combo.IsKeyboardFocusWithin)
            return;

        try
        {
            await _viewModel.ChangeSoundCategoryAsync(sound, category);
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Category update failed: {ex.Message}";
        }
    }

    private async void SoundList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            if (_viewModel.SelectedSound is not null)
            {
                await _viewModel.PlaySelectedSoundAsync();
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Playback failed: {ex.Message}";
        }
    }

    private async void ContextMenu_Play_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
            {
                _viewModel.SelectedSound = sound;
                await _viewModel.PlaySelectedSoundAsync();
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Playback failed: {ex.Message}";
        }
    }

    private async void ContextMenu_ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
            {
                sound.IsFavorite = !sound.IsFavorite;
                _viewModel.RefreshLibrarySummary();
                await _viewModel.PersistLibraryAsync();
                _viewModel.StatusText = sound.IsFavorite
                    ? $"Marked favorite: {sound.Name}"
                    : $"Removed favorite: {sound.Name}";
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Failed to toggle favorite: {ex.Message}";
        }
    }

    private async void ContextMenu_ChangeCategory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
            return;

        var combo = new System.Windows.Controls.ComboBox
        {
            ItemsSource = _viewModel.CategoryNames,
            SelectedItem = sound.Category,
            MinHeight = 34,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "Apply",
            MinWidth = 82,
            MinHeight = 34,
            Margin = new Thickness(0, 14, 8, 0),
            IsDefault = true
        };
        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            MinWidth = 82,
            MinHeight = 34,
            Margin = new Thickness(0, 14, 0, 0),
            IsCancel = true
        };

        var panel = new System.Windows.Controls.StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new System.Windows.Controls.TextBlock
                {
                    Text = $"Choose a category for {sound.Name}",
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                combo,
                new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Children = { okButton, cancelButton }
                }
            }
        };

        var dialog = new Window
        {
            Title = "Change Category",
            Width = 360,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Content = panel,
            Background = Background,
            Foreground = Foreground
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        if (dialog.ShowDialog() == true && combo.SelectedItem is string category)
        {
            await _viewModel.ChangeSoundCategoryAsync(sound, category);
        }
    }

    private void ContextMenu_AssignHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
            return;

        var win = new Window
        {
            Title = "Assign Hotkey",
            Width = 360,
            Height = 180,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            Owner = this,
            WindowStyle = System.Windows.WindowStyle.ToolWindow,
            ResizeMode = System.Windows.ResizeMode.NoResize,
            Content = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(20),
                Children =
                {
                    new System.Windows.Controls.TextBlock
                    {
                        Text = "Press a key combination for this sound...",
                        Margin = new System.Windows.Thickness(0, 0, 0, 16),
                        FontWeight = System.Windows.FontWeights.SemiBold
                    },
                    new System.Windows.Controls.TextBox
                    {
                        Name = "HotkeyTextBox",
                        IsReadOnly = true,
                        Height = 34,
                        Text = sound.Hotkey
                    },
                    new System.Windows.Controls.TextBlock
                    {
                        Text = "Supported: F1-F24, Ctrl, Alt, Shift + Key",
                        Margin = new System.Windows.Thickness(0, 8, 0, 0),
                        Opacity = 0.7
                    }
                }
            }
        };

        var textBox = (System.Windows.Controls.TextBox)((System.Windows.Controls.StackPanel)win.Content).Children[1];
        textBox.Focus();

        System.Windows.Input.KeyEventHandler onKeyDown = null!;
        onKeyDown = (_, args) =>
        {
            var modifiers = new List<string>();
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
                modifiers.Add("Ctrl");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0)
                modifiers.Add("Alt");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
                modifiers.Add("Shift");
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0)
                modifiers.Add("Win");

            var key = args.Key == System.Windows.Input.Key.System ? args.SystemKey : args.Key;
            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
                return;

            modifiers.Add(key.ToString());
            textBox.Text = string.Join(" + ", modifiers);
            args.Handled = true;
            win.DialogResult = true;
        };

        win.KeyDown += onKeyDown;
        var assigned = win.ShowDialog() == true;
        win.KeyDown -= onKeyDown;

        if (!assigned) return;

        var combo = textBox.Text;
        if (string.IsNullOrWhiteSpace(combo) || string.Equals(combo, sound.Hotkey, StringComparison.Ordinal))
            return;

        var oldHotkey = sound.Hotkey;
        sound.Hotkey = combo;

        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var gesture = HotkeyGesture.Parse(combo);

        if (!string.IsNullOrWhiteSpace(oldHotkey))
        {
            _hotkeyService.Unregister(handle, sound.Id);
        }

        if (_hotkeyService.Register(handle, gesture, sound.Id))
        {
            _settingsService.Current.PerSoundHotkeys[sound.Id] = combo;
            _ = _settingsService.SaveAsync();
            _viewModel.StatusText = $"Hotkey {combo} assigned to {sound.Name}";
        }
        else
        {
            sound.Hotkey = oldHotkey;
            _viewModel.StatusText = $"Hotkey {combo} could not be registered (conflict or invalid)";
        }

        _ = _viewModel.PersistLibraryAsync();
    }

    private void ContextMenu_OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
        {
            var path = sound.FilePath;
            if (!string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            }
            else
            {
                _viewModel.StatusText = "File not found on disk";
            }
        }
    }

    private async void ContextMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is System.Windows.Controls.MenuItem { DataContext: SoundItemViewModel sound })
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _hotkeyService.Unregister(handle, sound.Id);
                _settingsService.Current.PerSoundHotkeys.Remove(sound.Id);
                await _settingsService.SaveAsync();

                _viewModel.SelectedSound = sound;
                await _viewModel.RemoveSelectedSoundAsync();
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = $"Remove failed: {ex.Message}";
        }
    }

    private System.Windows.Point _dragStartPoint;

    private void CardList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var pos = e.GetPosition(sender as UIElement);
        if (Math.Abs(pos.X - _dragStartPoint.X) < 4 && Math.Abs(pos.Y - _dragStartPoint.Y) < 4) return;
        if (sender is System.Windows.Controls.ListBox lb && lb.SelectedItem is SoundItemViewModel dragged)
        {
            System.Windows.DragDrop.DoDragDrop(lb, dragged, System.Windows.DragDropEffects.Move);
        }
    }

    private void CardList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SoundItemViewModel)) is not SoundItemViewModel dropped) return;
        var target = FindAncestorDataContext<SoundItemViewModel>(e.OriginalSource as DependencyObject);
        if (target is null || ReferenceEquals(target, dropped)) return;
        MoveSound(dropped, target);
    }

    private void SoundListView_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        var pos = e.GetPosition(sender as UIElement);
        if (Math.Abs(pos.X - _dragStartPoint.X) < 4 && Math.Abs(pos.Y - _dragStartPoint.Y) < 4) return;
        if (sender is System.Windows.Controls.ListView lv && lv.SelectedItem is SoundItemViewModel dragged)
        {
            System.Windows.DragDrop.DoDragDrop(lv, dragged, System.Windows.DragDropEffects.Move);
        }
    }

    private void SoundListView_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(SoundItemViewModel)) is not SoundItemViewModel dropped) return;
        var target = FindAncestorDataContext<SoundItemViewModel>(e.OriginalSource as DependencyObject);
        if (target is null || ReferenceEquals(target, dropped)) return;
        MoveSound(dropped, target);
    }

    private async void MoveSound(SoundItemViewModel dropped, SoundItemViewModel target)
    {
        var idx = _viewModel.Sounds.IndexOf(target);
        _viewModel.Sounds.Remove(dropped);
        idx = _viewModel.Sounds.IndexOf(target);
        if (idx >= 0)
            _viewModel.Sounds.Insert(idx + 1, dropped);
        else
            _viewModel.Sounds.Add(dropped);
        await _viewModel.PersistLibraryAsync();
        _viewModel.StatusText = $"Reordered: {dropped.Name}";
    }

    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.GridViewColumnHeader header || header.Column is null)
            return;

        var binding = header.Column.DisplayMemberBinding;
        var sortBy = binding is System.Windows.Data.Binding b ? b.Path.Path : null;

        if (string.IsNullOrWhiteSpace(sortBy))
            return;

        var view = _viewModel.SoundsView;
        var sortDesc = view.SortDescriptions.FirstOrDefault();

        if (sortDesc.PropertyName == sortBy)
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortBy,
                sortDesc.Direction == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending));
        }
        else
        {
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(sortBy, ListSortDirection.Ascending));
        }
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? source) where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T typed })
                return typed;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
