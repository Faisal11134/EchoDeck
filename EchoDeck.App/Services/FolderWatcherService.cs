using System.Collections.Concurrent;
using System.IO;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class FolderWatcherService : IDisposable
{
    private readonly WatchedFoldersService _watchedFoldersService;
    private readonly LibraryService _libraryService;
    private readonly SettingsService _settingsService;
    private readonly LoggingService _loggingService;
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncRoot = new();
    private System.Threading.Timer? _debounceTimer;
    private bool _initialized;

    public FolderWatcherService(
        WatchedFoldersService watchedFoldersService,
        LibraryService libraryService,
        SettingsService settingsService,
        LoggingService loggingService)
    {
        _watchedFoldersService = watchedFoldersService;
        _libraryService = libraryService;
        _settingsService = settingsService;
        _loggingService = loggingService;
    }

    public string StatusMessage { get; private set; } = "Folder watching is ready.";

    public Task InitializeAsync()
    {
        lock (_syncRoot)
        {
            if (_initialized)
            {
                RefreshWatchers();
                return Task.CompletedTask;
            }

            _debounceTimer = new System.Threading.Timer(_ => _ = ProcessPendingFilesAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _watchedFoldersService.WatchedFolders.CollectionChanged += (_, _) => RefreshWatchers();
            _initialized = true;
        }

        RefreshWatchers();
        return Task.CompletedTask;
    }

    public void RefreshWatchers()
    {
        lock (_syncRoot)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            _watchers.Clear();

            var watchedFolders = _watchedFoldersService.WatchedFolders
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var folder in watchedFolders)
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                watcher.Created += OnFolderChanged;
                watcher.Changed += OnFolderChanged;
                watcher.Renamed += OnFolderRenamed;
                watcher.EnableRaisingEvents = true;
                _watchers[folder] = watcher;
            }

            StatusMessage = watchedFolders.Count > 0
                ? $"Watching {watchedFolders.Count} folder(s)."
                : "Watching disabled until at least one folder is added.";
        }
    }

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath))
        {
            return;
        }

        _pendingFiles[e.FullPath] = 1;
        _debounceTimer?.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private void OnFolderRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath))
        {
            return;
        }

        _pendingFiles[e.FullPath] = 1;
        _debounceTimer?.Change(TimeSpan.FromMilliseconds(500), Timeout.InfiniteTimeSpan);
    }

    private async Task ProcessPendingFilesAsync()
    {
        if (_pendingFiles.IsEmpty)
        {
            return;
        }

        var files = _pendingFiles.Keys.ToList();
        _pendingFiles.Clear();

        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null && !dispatcher.CheckAccess())
            {
                var importedTask = await dispatcher.InvokeAsync(() => ProcessImportsAsync(files));
                await importedTask;
            }
            else
            {
                await ProcessImportsAsync(files);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Watched folder import failed: {ex.Message}";
            await _loggingService.LogError(ex, "Watched folder import failed.");
        }
    }

    private async Task ProcessImportsAsync(List<string> files)
    {
        var imported = await ImportFilesAsync(files);
        StatusMessage = imported > 0
            ? $"Imported {imported} file(s) from watched folders."
            : "Watched folders processed: no new supported files.";
        await _loggingService.LogInformation(StatusMessage);
    }

    private async Task<int> ImportFilesAsync(IEnumerable<string> filePaths)
    {
        var imported = 0;
        var category = string.IsNullOrWhiteSpace(_settingsService.Current.DefaultCategoryId)
            ? "Uncategorized"
            : _settingsService.Current.DefaultCategoryId;

        var result = _libraryService.ImportPaths(filePaths, category);
        var tasks = result.ImportedItems.Select(async item =>
        {
            item.Duration = await SoundMetadataReader.TryReadDurationAsync(item.FilePath);
        });
        await Task.WhenAll(tasks);
        imported = result.ImportedItems.Count;

        if (imported == 0)
        {
            return 0;
        }

        await _libraryService.SaveAsync();
        return imported;
    }

    private static bool IsSupportedFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wma", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".aac", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".flac", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _debounceTimer?.Dispose();
            foreach (var watcher in _watchers.Values)
            {
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }
}
