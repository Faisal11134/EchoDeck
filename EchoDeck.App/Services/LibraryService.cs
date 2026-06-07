using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using EchoDeck.App.Infrastructure;
using EchoDeck.App.Models;
using EchoDeck.App.ViewModels;

namespace EchoDeck.App.Services;

public sealed class LibraryService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public ObservableCollection<SoundItemViewModel> Sounds { get; } = new();
    public string? LastLoadWarning { get; private set; }

    public LibraryService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task LoadAsync()
    {
        LastLoadWarning = null;
        Sounds.Clear();

        if (!File.Exists(_paths.LibraryFilePath))
        {
            SeedDefaults();
            await SaveAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_paths.LibraryFilePath);
            List<SoundLibraryItem> items;

            // Support both wrapped { schemaVersion, sounds } and flat array formats
            if (json.TrimStart().StartsWith("["))
            {
                items = JsonSerializer.Deserialize<List<SoundLibraryItem>>(json, SerializerOptions) ?? [];
            }
            else
            {
                var doc = JsonSerializer.Deserialize<LibraryDocument>(json, SerializerOptions);
                items = doc?.Sounds ?? [];
            }

            foreach (var item in items)
            {
                Sounds.Add(ToViewModel(item));
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            await BackupCorruptedFileAsync(_paths.LibraryFilePath, "library");
            SeedDefaults();
            await SaveAsync();
            LastLoadWarning = "Recovered library.json from corruption.";
        }
    }

    public async Task SaveAsync()
    {
        var items = Sounds.Select(ToModel).ToList();
        var doc = new LibraryDocument { SchemaVersion = 1, Sounds = items };
        var json = JsonSerializer.Serialize(doc, SerializerOptions);
        var tempPath = _paths.LibraryFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _paths.LibraryFilePath, overwrite: true);
    }

    public SoundItemViewModel AddFromFile(string filePath, string name, string category, string duration)
    {
        if (Sounds.Any(sound => string.Equals(sound.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
        {
            return Sounds.First(sound => string.Equals(sound.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        var item = new SoundItemViewModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            FilePath = filePath,
            Category = category,
            Duration = duration,
            Hotkey = string.Empty,
            IsFavorite = false,
            IsMissingFile = !File.Exists(filePath)
        };

        Sounds.Add(item);
        return item;
    }

    public IEnumerable<SoundItemViewModel> AddFromPaths(IEnumerable<string> paths, string defaultCategory)
    {
        var imported = new List<SoundItemViewModel>();

        foreach (var filePath in ExpandImportPaths(paths).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Sounds.Any(sound => string.Equals(sound.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(filePath);
            imported.Add(AddFromFile(filePath, name, defaultCategory, "0:00"));
        }

        return imported;
    }

    public ImportResult ImportPaths(IEnumerable<string> paths, string defaultCategory)
    {
        var imported = 0;
        var skipped = 0;
        var createdItems = new List<SoundItemViewModel>();

        foreach (var filePath in ExpandImportPaths(paths).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Sounds.Any(sound => string.Equals(sound.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(filePath);
            createdItems.Add(AddFromFile(filePath, name, defaultCategory, "0:00"));
            imported++;
        }

        return new ImportResult(createdItems, imported, skipped);
    }

    public void NormalizeCategories(IEnumerable<string> allowedCategories)
    {
        var allowed = new HashSet<string>(allowedCategories.Where(IsUsableCategory), StringComparer.OrdinalIgnoreCase);
        foreach (var sound in Sounds)
        {
            if (!allowed.Contains(sound.Category))
            {
                sound.Category = "Uncategorized";
            }
        }
    }

    public void RenameCategory(string oldName, string newName)
    {
        foreach (var sound in Sounds.Where(sound => string.Equals(sound.Category, oldName, StringComparison.OrdinalIgnoreCase)))
        {
            sound.Category = newName;
        }
    }

    private static bool IsUsableCategory(string? category)
        => !string.IsNullOrWhiteSpace(category) && !string.Equals(category, "Uncategorized", StringComparison.OrdinalIgnoreCase);

    private static SoundItemViewModel ToViewModel(SoundLibraryItem item) =>
        new()
        {
            Id = item.Id,
            Name = item.Name,
            FilePath = item.FilePath,
            Category = item.Category,
            Hotkey = item.Hotkey,
            Duration = item.Duration,
            Volume = item.Volume,
            IsFavorite = item.IsFavorite,
            IsMissingFile = !string.IsNullOrWhiteSpace(item.FilePath) && !File.Exists(item.FilePath),
            Normalized = item.Normalized,
            NormalizationGain = item.NormalizationGain,
            PlayCount = item.PlayCount,
            CreatedAt = item.CreatedAt,
            LastPlayedAt = item.LastPlayedAt
        };

    private static SoundLibraryItem ToModel(SoundItemViewModel item) =>
        new()
        {
            Id = item.Id,
            Name = item.Name,
            FilePath = item.FilePath,
            Category = item.Category,
            Hotkey = item.Hotkey,
            Duration = item.Duration,
            Volume = item.Volume,
            IsFavorite = item.IsFavorite,
            IsMissingFile = item.IsMissingFile,
            Normalized = item.Normalized,
            NormalizationGain = item.NormalizationGain,
            PlayCount = item.PlayCount,
            CreatedAt = item.CreatedAt,
            LastPlayedAt = item.LastPlayedAt
            };

    public async Task<int> ImportFromJsonAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return 0;

        var json = await File.ReadAllTextAsync(filePath);
        LibraryDocument? doc;

        try
        {
            if (json.TrimStart().StartsWith("["))
            {
                var items = JsonSerializer.Deserialize<List<SoundLibraryItem>>(json, SerializerOptions);
                if (items is null) return 0;
                doc = new LibraryDocument { SchemaVersion = 1, Sounds = items };
            }
            else
            {
                doc = JsonSerializer.Deserialize<LibraryDocument>(json, SerializerOptions);
            }
        }
        catch
        {
            return 0;
        }

        if (doc?.Sounds is null || doc.Sounds.Count == 0)
            return 0;

        var imported = 0;
        foreach (var item in doc.Sounds)
        {
            if (Sounds.Any(s => string.Equals(s.Id, item.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            Sounds.Add(ToViewModel(item));
            imported++;
        }

        await SaveAsync();
        return imported;
    }

    public void ExportToJson(string outputPath)
    {
        var items = Sounds.Select(ToModel).ToList();
        var doc = new LibraryDocument { SchemaVersion = 1, Sounds = items };
        var json = JsonSerializer.Serialize(doc, SerializerOptions);
        File.WriteAllText(outputPath, json);
    }

    public async Task<int> ExportSoundPackAsync(string zipPath, IReadOnlyList<SoundItemViewModel> items)
    {
        if (items.Count == 0) return 0;

        var tempDir = Path.Combine(Path.GetTempPath(), "echodeck_pack_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var meta = new List<SoundLibraryItem>();
            foreach (var item in items)
            {
                var model = ToModel(item);
                var src = item.FilePath;
                if (!string.IsNullOrWhiteSpace(src) && File.Exists(src))
                {
                    var dst = Path.Combine(tempDir, Path.GetFileName(src));
                    File.Copy(src, dst, true);
                    model.FilePath = Path.GetFileName(src);
                }
                meta.Add(model);
            }

            var doc = new LibraryDocument { SchemaVersion = 1, Sounds = meta };
            var json = JsonSerializer.Serialize(doc, SerializerOptions);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "library.json"), json);

            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath);
            return items.Count;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    public async Task<int> ImportSoundPackAsync(string zipPath, string defaultCategory)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "echodeck_import_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(zipPath, tempDir);

            var jsonPath = Path.Combine(tempDir, "library.json");
            if (!File.Exists(jsonPath)) return 0;

            var json = await File.ReadAllTextAsync(jsonPath);
            var doc = JsonSerializer.Deserialize<LibraryDocument>(json);
            if (doc?.Sounds is null || doc.Sounds.Count == 0) return 0;

            var imported = 0;
            foreach (var sound in doc.Sounds)
            {
                if (Sounds.Any(s => string.Equals(s.Name, sound.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var audioPath = string.IsNullOrWhiteSpace(sound.FilePath)
                    ? string.Empty
                    : Path.Combine(tempDir, sound.FilePath);

                if (!string.IsNullOrWhiteSpace(audioPath) && !File.Exists(audioPath))
                    audioPath = string.Empty;

                var audioCopy = string.Empty;
                if (!string.IsNullOrWhiteSpace(audioPath))
                {
                    var fileName = Path.GetFileName(audioPath);
                    audioCopy = Path.Combine(_paths.DataFolder, fileName);
                    if (!File.Exists(audioCopy))
                    {
                        var dir = Path.GetDirectoryName(audioCopy);
                        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.Copy(audioPath, audioCopy);
                    }
                }

                var vm = new SoundItemViewModel
                {
                    Name = sound.Name,
                    FilePath = audioCopy,
                    Category = string.IsNullOrWhiteSpace(sound.Category) ? defaultCategory : sound.Category,
                    Volume = Math.Clamp(sound.Volume, 0, 2),
                    IsFavorite = sound.IsFavorite,
                    Duration = sound.Duration ?? "0:00",
                    CreatedAt = DateTime.UtcNow
                };
                Sounds.Add(vm);
                imported++;
            }

            if (imported > 0) await SaveAsync();
            return imported;
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private void SeedDefaults()
    {
        Sounds.Add(new SoundItemViewModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Airhorn",
            FilePath = string.Empty,
            Category = "Memes",
            Hotkey = "F8",
            Duration = "0:02",
            Volume = 1.0,
            IsFavorite = true,
            IsMissingFile = true,
            CreatedAt = DateTime.UtcNow
        });

        Sounds.Add(new SoundItemViewModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Victory Stinger",
            FilePath = string.Empty,
            Category = "FX",
            Hotkey = "F9",
            Duration = "0:04",
            Volume = 1.0,
            IsFavorite = false,
            IsMissingFile = true,
            CreatedAt = DateTime.UtcNow
        });

        Sounds.Add(new SoundItemViewModel
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Beep",
            FilePath = string.Empty,
            Category = "Alerts",
            Hotkey = "F10",
            Duration = "0:01",
            Volume = 1.0,
            IsFavorite = false,
            IsMissingFile = true,
            CreatedAt = DateTime.UtcNow
        });
    }

    private async Task BackupCorruptedFileAsync(string sourcePath, string stem)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        var backupPath = Path.Combine(_paths.BackupsFolder, $"{stem}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.bak");
        var bytes = await File.ReadAllBytesAsync(sourcePath);
        await File.WriteAllBytesAsync(backupPath, bytes);
    }

    private static IEnumerable<string> ExpandImportPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                if (IsSupportedAudioFile(path))
                {
                    yield return path;
                }

                continue;
            }

            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    if (IsSupportedAudioFile(file))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static bool IsSupportedAudioFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".wma", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".aac", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".flac", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record ImportResult(IReadOnlyList<SoundItemViewModel> ImportedItems, int ImportedCount, int SkippedDuplicateCount);

internal sealed class LibraryDocument
{
    public int SchemaVersion { get; set; } = 1;
    public List<SoundLibraryItem> Sounds { get; set; } = new();
}
