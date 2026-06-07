using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using EchoDeck.App.Infrastructure;

namespace EchoDeck.App.Services;

public sealed class WatchedFoldersService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public ObservableCollection<string> WatchedFolders { get; } = new();
    public string StatusMessage { get; private set; } = "Watched folders are ready.";

    public WatchedFoldersService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task LoadAsync()
    {
        WatchedFolders.Clear();

        if (!File.Exists(_paths.WatchedFoldersFilePath))
        {
            await SaveAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_paths.WatchedFoldersFilePath);
            var items = JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? [];
            foreach (var folder in items
                         .Select(NormalizeFolderPath)
                         .Where(folder => !string.IsNullOrWhiteSpace(folder))
                         .Select(folder => folder!)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                WatchedFolders.Add(folder);
            }

            StatusMessage = $"Loaded {WatchedFolders.Count} watched folder(s).";
        }
        catch
        {
            WatchedFolders.Clear();
            StatusMessage = "Watched folders file was unreadable. Resetting to empty list.";
            await SaveAsync();
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(WatchedFolders, SerializerOptions);
        var tempPath = _paths.WatchedFoldersFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _paths.WatchedFoldersFilePath, overwrite: true);
    }

    public Task InitializeAsync()
    {
        StatusMessage = WatchedFolders.Count > 0
            ? $"Loaded {WatchedFolders.Count} watched folder(s)."
            : "Watched folders are ready.";
        return Task.CompletedTask;
    }

    public bool AddWatchedFolder(string path, out string? storedPath)
    {
        storedPath = null;
        var normalized = NormalizeFolderPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            StatusMessage = "Watched folder path cannot be empty.";
            return false;
        }

        if (!Directory.Exists(normalized))
        {
            StatusMessage = "Watched folder does not exist.";
            return false;
        }

        if (WatchedFolders.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = $"Watched folder already exists: {normalized}";
            return false;
        }

        WatchedFolders.Add(normalized);
        storedPath = normalized;
        StatusMessage = $"Added watched folder: {normalized}";
        return true;
    }

    public bool RemoveWatchedFolder(string path)
    {
        var normalized = NormalizeFolderPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            StatusMessage = "Watched folder path cannot be empty.";
            return false;
        }

        var existing = WatchedFolders.FirstOrDefault(folder => string.Equals(folder, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            StatusMessage = "Watched folder not found.";
            return false;
        }

        WatchedFolders.Remove(existing);
        StatusMessage = $"Removed watched folder: {existing}";
        return true;
    }

    private static string? NormalizeFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        }
        catch
        {
            return path.Trim();
        }
    }

    public void ClearWatchedFolders()
    {
        WatchedFolders.Clear();
        StatusMessage = "Cleared watched folders.";
    }
}
