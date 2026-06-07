using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly LibraryService _libraryService;
    private readonly CategoryService _categoryService;
    private readonly SettingsService _settingsService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly NormalizationService _normalizationService;
    private readonly ICollectionView _soundsView;
    private string _searchText = string.Empty;
    private bool _isCardView = true;
    private string _libraryFilter = "All";
    private string _selectedCategoryFilter = "All Sounds";
    private SoundItemViewModel? _selectedSound;
    private string _statusText = "Initializing";
    private string _importCategory = "Uncategorized";
    private bool _isSelectionMode;
    private double _bulkVolume = 1.0;
    private bool _suppressBulkVolumeApply;
    private System.Threading.Timer? _bulkVolumeSaveTimer;
    private string _sortBy = "Name";
    private bool _sortAscending = true;

    public MainViewModel(LibraryService libraryService, CategoryService categoryService, SettingsService settingsService, IAudioPlaybackService playbackService, NormalizationService normalizationService)
    {
        _libraryService = libraryService;
        _categoryService = categoryService;
        _settingsService = settingsService;
        _playbackService = playbackService;
        _normalizationService = normalizationService;
        Sounds = _libraryService.Sounds;
        _soundsView = CollectionViewSource.GetDefaultView(Sounds);
        _soundsView.Filter = FilterSound;
        RefreshCategories();
        ApplySort();

        _playbackService.ActivePlaybackChanged += (_, args) =>
        {
            foreach (var sound in Sounds)
            {
                if (string.Equals(sound.Id, args.SoundId, StringComparison.OrdinalIgnoreCase))
                {
                    sound.IsPlaying = args.IsPlaying;
                    if (args.IsPlaying)
                    {
                        sound.PlayCount++;
                        sound.LastPlayedAt = DateTime.UtcNow;
                    }
                }
                else if (args.IsPlaying)
                {
                    sound.IsPlaying = false;
                }
            }
        };
    }

    public ObservableCollection<SoundItemViewModel> Sounds { get; }
    public ObservableCollection<string> ManageableCategories { get; } = new();
    public ObservableCollection<SoundItemViewModel> SelectedForBulkAction { get; } = new();
    public ICollectionView SoundsView => _soundsView;

    public List<string> CategoryNames => _categoryService.Categories.Select(c => c.Name).ToList();

    public string ImportCategory
    {
        get => _importCategory;
        set { _importCategory = value; NotifyPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _soundsView.Refresh();
                NotifyPropertyChanged(nameof(LibraryStatusText));
                NotifyPropertyChanged(nameof(HasVisibleSounds));
                NotifyPropertyChanged(nameof(EmptyStateTitle));
                NotifyPropertyChanged(nameof(EmptyStateSubtitle));
            }
        }
    }

    public bool IsCardView
    {
        get => _isCardView;
        set => SetProperty(ref _isCardView, value);
    }

    public bool IsListView
    {
        get => !_isCardView;
        set => IsCardView = !value;
    }

    public string LibraryFilter
    {
        get => _libraryFilter;
        set
        {
            if (SetProperty(ref _libraryFilter, value))
            {
                _soundsView.Refresh();
                NotifyPropertyChanged(nameof(LibraryStatusText));
                NotifyPropertyChanged(nameof(HasVisibleSounds));
                NotifyPropertyChanged(nameof(EmptyStateTitle));
                NotifyPropertyChanged(nameof(EmptyStateSubtitle));
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                _soundsView.Refresh();
                NotifyPropertyChanged(nameof(LibraryStatusText));
                NotifyPropertyChanged(nameof(HasVisibleSounds));
                NotifyPropertyChanged(nameof(EmptyStateTitle));
                NotifyPropertyChanged(nameof(EmptyStateSubtitle));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public SoundItemViewModel? SelectedSound
    {
        get => _selectedSound;
        set
        {
            if (SetProperty(ref _selectedSound, value))
            {
                StatusText = value is null ? TryGetResource("Status_NothingSelected", "Nothing selected") : $"Selected: {value.Name}";
            }
        }
    }

    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (SetProperty(ref _isSelectionMode, value))
            {
                if (!value)
                {
                    ClearBulkSelection();
                }

                NotifyPropertyChanged(nameof(IsNotSelectionMode));
                NotifyPropertyChanged(nameof(BulkActionSummary));
            }
        }
    }

    public bool IsNotSelectionMode => !IsSelectionMode;

    public int SelectedSoundCount => SelectedForBulkAction.Count;
    public bool HasBulkSelection => SelectedSoundCount > 0;
    public string BulkActionSummary => IsSelectionMode
        ? $"{SelectedSoundCount} selected"
        : string.Empty;

    public double BulkVolume
    {
        get => _bulkVolume;
        set
        {
            var clamped = Math.Clamp(value, 0, 2);
            if (!SetProperty(ref _bulkVolume, clamped))
                return;

            if (_suppressBulkVolumeApply)
                return;

            foreach (var sound in SelectedForBulkAction)
            {
                sound.Volume = clamped;
            }

            QueueBulkVolumeSave();
        }
    }

    public string SortBy
    {
        get => _sortBy;
        set
        {
            if (SetProperty(ref _sortBy, value))
                ApplySort();
        }
    }

    public bool SortAscending
    {
        get => _sortAscending;
        set
        {
            if (SetProperty(ref _sortAscending, value))
                ApplySort();
        }
    }

    public List<string> SortOptions { get; } = new() { "Name", "Category", "Duration", "PlayCount", "CreatedAt" };

    private void ApplySort()
    {
        _soundsView.SortDescriptions.Clear();
        var direction = _sortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending;
        _soundsView.SortDescriptions.Add(new SortDescription(_sortBy, direction));
    }

    public string LibraryStatusText => $"{VisibleSoundCount} sounds · {FavoriteCount} favorites · {LibraryFilter}";
    public bool HasVisibleSounds => VisibleSoundCount > 0;

    public string EmptyStateTitle => Sounds.Count == 0
        ? TryGetResource("Empty_NoSounds", "No sounds in library yet")
        : TryGetResource("Empty_NoMatch", "No sounds match the current filter");

    public string EmptyStateSubtitle => Sounds.Count == 0
        ? TryGetResource("Empty_ImportHint", "Import audio files or drop them into the window to get started.")
        : TryGetResource("Empty_TryDifferent", "Try a different search, category, or library filter.");

    public int VisibleSoundCount => _soundsView.Cast<object>().Count();
    public int FavoriteCount => Sounds.Count(s => s.IsFavorite);

    public async Task AddImportedSoundsAsync(IEnumerable<string> filePaths, string defaultCategory)
    {
        var importResult = _libraryService.ImportPaths(filePaths, defaultCategory);
        var imported = importResult.ImportedItems;

        if (imported.Count == 0)
        {
            StatusText = "No supported files were imported";
            return;
        }

        var tasks = imported.Select(async item =>
        {
            item.Duration = await SoundMetadataReader.TryReadDurationAsync(item.FilePath);
        });
        await Task.WhenAll(tasks);

        if (_settingsService.Current.EnableNormalization)
        {
            var normTasks = imported.Select(item => _normalizationService.NormalizeAsync(item));
            await Task.WhenAll(normTasks);
        }

        await _libraryService.SaveAsync();
        RefreshCategories();
        _soundsView.Refresh();
        NotifyPropertyChanged(nameof(LibraryStatusText));
        NotifyPropertyChanged(nameof(HasVisibleSounds));
        NotifyPropertyChanged(nameof(EmptyStateTitle));
        NotifyPropertyChanged(nameof(EmptyStateSubtitle));

        StatusText = importResult.SkippedDuplicateCount > 0
            ? $"Imported {imported.Count} sound(s), skipped {importResult.SkippedDuplicateCount} duplicate(s)"
            : $"Imported {imported.Count} sound(s)";
    }

    public async Task PersistLibraryAsync() => await _libraryService.SaveAsync();

    public async Task ChangeSoundCategoryAsync(SoundItemViewModel sound, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return;

        if (!CategoryNames.Contains(category, StringComparer.OrdinalIgnoreCase))
            return;

        if (string.Equals(sound.Category, category, StringComparison.OrdinalIgnoreCase))
            return;

        sound.Category = category;
        await _libraryService.SaveAsync();
        _soundsView.Refresh();
        NotifyPropertyChanged(nameof(LibraryStatusText));
        NotifyPropertyChanged(nameof(HasVisibleSounds));
        NotifyPropertyChanged(nameof(EmptyStateTitle));
        NotifyPropertyChanged(nameof(EmptyStateSubtitle));
        StatusText = $"Moved {sound.Name} to {category}";
    }

    public void EnterSelectionMode()
    {
        IsSelectionMode = true;
        StatusText = "Select sounds to manage";
    }

    public void ExitSelectionMode()
    {
        IsSelectionMode = false;
        StatusText = "Selection cancelled";
    }

    public void ToggleBulkSelection(SoundItemViewModel sound)
    {
        if (!IsSelectionMode)
            return;

        if (sound.IsMarkedForBulkAction)
        {
            sound.IsMarkedForBulkAction = false;
            SelectedForBulkAction.Remove(sound);
        }
        else
        {
            sound.IsMarkedForBulkAction = true;
            if (!SelectedForBulkAction.Contains(sound))
            {
                SelectedForBulkAction.Add(sound);
            }
        }

        UpdateBulkSelectionState();
    }

    public void ClearBulkSelection()
    {
        foreach (var sound in SelectedForBulkAction.ToList())
        {
            sound.IsMarkedForBulkAction = false;
        }

        SelectedForBulkAction.Clear();
        UpdateBulkSelectionState();
    }

    public async Task RemoveBulkSelectedSoundsAsync()
    {
        var selected = SelectedForBulkAction.ToList();
        if (selected.Count == 0)
            return;

        foreach (var sound in selected)
        {
            Sounds.Remove(sound);
        }

        ClearBulkSelection();
        IsSelectionMode = false;
        await _libraryService.SaveAsync();
        RefreshCategories();
        _soundsView.Refresh();
        NotifyPropertyChanged(nameof(LibraryStatusText));
        NotifyPropertyChanged(nameof(HasVisibleSounds));
        StatusText = $"Removed {selected.Count} sound(s) from library.";
    }

    public async Task RemoveSelectedSoundAsync()
    {
        if (SelectedSound is null) return;

        var removed = Sounds.Remove(SelectedSound);
        if (!removed) return;

        await _libraryService.SaveAsync();
        RefreshCategories();
        _soundsView.Refresh();
        StatusText = TryGetResource("Status_Removed", "Sound removed from library");
        NotifyPropertyChanged(nameof(HasVisibleSounds));
        NotifyPropertyChanged(nameof(EmptyStateTitle));
        NotifyPropertyChanged(nameof(EmptyStateSubtitle));
    }

    public async Task PlaySelectedSoundAsync()
    {
        if (SelectedSound is null)
        {
            StatusText = TryGetResource("Status_ChooseToPlay", "Choose a sound to play");
            return;
        }

        var message = await _playbackService.PlayAsync(SelectedSound, _settingsService.Current);
        StatusText = message;
        await _libraryService.SaveAsync();
    }

    public async Task PlayRandomSoundAsync()
    {
        var visible = _soundsView.Cast<SoundItemViewModel>().ToList();
        if (visible.Count == 0)
        {
            StatusText = "No sounds to play";
            return;
        }
        var pick = visible[Random.Shared.Next(visible.Count)];
        SelectedSound = pick;
        StatusText = await _playbackService.PlayAsync(pick, _settingsService.Current);
        await _libraryService.SaveAsync();
    }

    public async Task StopAllPlaybackAsync()
    {
        await _playbackService.StopAllAsync();
        StatusText = _playbackService.StatusMessage;
    }

    public void RefreshLibrarySummary()
    {
        NotifyPropertyChanged(nameof(FavoriteCount));
        NotifyPropertyChanged(nameof(LibraryStatusText));
    }

    public List<string> CategoryFilterOptions => new[] { "All Sounds" }.Concat(CategoryNames).ToList();

    public void SetFilter(string category)
    {
        if (string.Equals(category, "All Sounds", StringComparison.OrdinalIgnoreCase))
        {
            _libraryFilter = "All";
            _selectedCategoryFilter = "All Sounds";
        }
        else
        {
            _libraryFilter = "Categories";
            _selectedCategoryFilter = category;
        }
        NotifyPropertyChanged(nameof(LibraryFilter));
        NotifyPropertyChanged(nameof(SelectedCategoryFilter));
        _soundsView.Refresh();
        NotifyPropertyChanged(nameof(LibraryStatusText));
        NotifyPropertyChanged(nameof(HasVisibleSounds));
        NotifyPropertyChanged(nameof(EmptyStateTitle));
        NotifyPropertyChanged(nameof(EmptyStateSubtitle));
        StatusText = _libraryFilter == "All" ? "Showing all sounds" : $"Showing category: {category}";
    }

    public void RefreshCategories()
    {
        ManageableCategories.Clear();
        foreach (var category in _categoryService.Categories)
        {
            ManageableCategories.Add(category.Name);
        }

        if (!ManageableCategories.Contains(SelectedCategoryFilter, StringComparer.OrdinalIgnoreCase) &&
            !string.Equals(SelectedCategoryFilter, "All Sounds", StringComparison.OrdinalIgnoreCase))
        {
            SelectedCategoryFilter = "All Sounds";
        }

        NotifyPropertyChanged(nameof(ManageableCategories));
        NotifyPropertyChanged(nameof(CategoryNames));
        NotifyPropertyChanged(nameof(CategoryFilterOptions));
        NotifyPropertyChanged(nameof(LibraryStatusText));
    }

    private void UpdateBulkSelectionState()
    {
        NotifyPropertyChanged(nameof(SelectedSoundCount));
        NotifyPropertyChanged(nameof(HasBulkSelection));
        NotifyPropertyChanged(nameof(BulkActionSummary));

        _suppressBulkVolumeApply = true;
        try
        {
            BulkVolume = SelectedForBulkAction.Count == 0
                ? 1.0
                : SelectedForBulkAction.Average(sound => sound.Volume);
        }
        finally
        {
            _suppressBulkVolumeApply = false;
        }
    }

    private void QueueBulkVolumeSave()
    {
        if (SelectedForBulkAction.Count == 0)
            return;

        _bulkVolumeSaveTimer ??= new System.Threading.Timer(
            _ => _ = _libraryService.SaveAsync(),
            null,
            System.Threading.Timeout.InfiniteTimeSpan,
            System.Threading.Timeout.InfiniteTimeSpan);
        _bulkVolumeSaveTimer.Change(TimeSpan.FromMilliseconds(300), System.Threading.Timeout.InfiniteTimeSpan);
        StatusText = $"Set volume for {SelectedForBulkAction.Count} sound(s) to {BulkVolume:P0}";
    }

    private static string TryGetResource(string key, string fallback)
    {
        try
        {
            var resource = System.Windows.Application.Current?.TryFindResource(key);
            return resource is string s && !string.IsNullOrWhiteSpace(s) ? s : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private bool FilterSound(object item)
    {
        if (item is not SoundItemViewModel sound) return false;

        var matchesFilter = LibraryFilter switch
        {
            "Favorites" => sound.IsFavorite,
            "Categories" => string.Equals(SelectedCategoryFilter, "All Sounds", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sound.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase),
            _ => true
        };

        if (!matchesFilter) return false;

        if (string.IsNullOrWhiteSpace(SearchText)) return true;

        return sound.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || sound.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || sound.Hotkey.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }
}
