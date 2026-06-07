using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Interop;
using EchoDeck.App.Models;
using EchoDeck.App.Services;
using EchoDeck.App.ViewModels;

namespace EchoDeck.App.Views;

public partial class CategoryManageDialog : Window
{
    private readonly CategoryService _categoryService;
    private readonly LibraryService _libraryService;
    private readonly MainViewModel _mainViewModel;

    public ObservableCollection<Category> Categories { get; }
    public Category? SelectedItem { get; set; }

    public CategoryManageDialog(CategoryService categoryService, LibraryService libraryService, MainViewModel mainViewModel)
    {
        InitializeComponent();
        _categoryService = categoryService;
        _libraryService = libraryService;
        _mainViewModel = mainViewModel;
        Categories = _categoryService.Categories;
        CategoryListBox.DataContext = this;
    }

    private async void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        var name = NewCategoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NewCategoryTextBox.Focus();
            return;
        }

        if (!_categoryService.AddCategory(name))
        {
            System.Windows.MessageBox.Show(this, "Category already exists.", "Add Category",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await _categoryService.SaveAsync();
        NewCategoryTextBox.Text = string.Empty;
        CategoryListBox.SelectedItem = _categoryService.Categories.FirstOrDefault(c =>
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private void NewCategoryTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            e.Handled = true;
            AddCategory_Click(sender, e);
        }
    }

    private async void RenameCategory_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null || string.Equals(selected.Name, "Uncategorized", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this,
                "Select a category to rename (Uncategorized cannot be renamed).",
                "Rename Category", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new InputDialog("Rename Category", "New name:", selected.Name);
        dialog.Owner = this;
        if (dialog.ShowDialog() != true) return;

        var newName = dialog.InputText.Trim();
        if (string.IsNullOrWhiteSpace(newName)) return;

        var oldName = selected.Name;
        if (!_categoryService.RenameCategory(oldName, newName))
        {
            System.Windows.MessageBox.Show(this, "Category rename failed (name may already exist).",
                "Rename Category", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _libraryService.RenameCategory(oldName, newName);
        await _categoryService.SaveAsync();
        await _libraryService.SaveAsync();
    }

    private async void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null) return;

        if (!_categoryService.MoveCategoryUp(selected.Name)) return;
        await _categoryService.SaveAsync();
        CategoryListBox.SelectedItem = selected;
    }

    private async void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null) return;

        if (!_categoryService.MoveCategoryDown(selected.Name)) return;
        await _categoryService.SaveAsync();
        CategoryListBox.SelectedItem = selected;
    }

    private async void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedItem;
        if (selected is null || string.Equals(selected.Name, "Uncategorized", StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show(this,
                "Select a category to delete (Uncategorized cannot be deleted).",
                "Delete Category", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = System.Windows.MessageBox.Show(this,
            $"Delete category '{selected.Name}'? Sounds will be moved to Uncategorized.",
            "Delete Category", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        var categoryName = selected.Name;
        if (!_categoryService.RemoveCategory(categoryName)) return;

        _libraryService.NormalizeCategories(_categoryService.CategoryNames);
        await _categoryService.SaveAsync();
        await _libraryService.SaveAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CategoryDialog_SourceInitialized(object sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private const int ResizeBorder = 6;
    private const int WM_NCHITTEST = 0x0084;
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
            var w = ActualWidth;
            var h = ActualHeight;
            var l2 = pt.X < ResizeBorder;
            var r2 = pt.X >= w - ResizeBorder;
            var t = pt.Y < ResizeBorder;
            var b = pt.Y >= h - ResizeBorder;
            if (l2 && t) { handled = true; return new IntPtr(HTTOPLEFT); }
            if (l2 && b) { handled = true; return new IntPtr(HTBOTTOMLEFT); }
            if (r2 && t) { handled = true; return new IntPtr(HTTOPRIGHT); }
            if (r2 && b) { handled = true; return new IntPtr(HTBOTTOMRIGHT); }
            if (l2) { handled = true; return new IntPtr(HTLEFT); }
            if (r2) { handled = true; return new IntPtr(HTRIGHT); }
            if (t) { handled = true; return new IntPtr(HTTOP); }
            if (b) { handled = true; return new IntPtr(HTBOTTOM); }
            handled = false;
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
