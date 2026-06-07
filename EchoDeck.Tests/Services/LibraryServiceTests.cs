using System.IO;
using System.Text.Json;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.Tests.Services;

public sealed class LibraryServiceTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public LibraryServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
        foreach (var file in Directory.GetFiles(_fixture.Paths.DataFolder, "*.json"))
            try { File.Delete(file); } catch { }
    }

    [Fact]
    public async Task LoadAsync_NoFile_CreatesDefaultSounds()
    {
        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.NotEmpty(service.Sounds);
        Assert.Contains(service.Sounds, s => s.Name == "Airhorn");
        Assert.Contains(service.Sounds, s => s.Name == "Victory Stinger");
        Assert.Contains(service.Sounds, s => s.Name == "Beep");
    }

    [Fact]
    public async Task LoadAsync_FlatArrayFormat_LoadsCorrectly()
    {
        var items = new List<SoundLibraryItem>
        {
            new() { Id = "test1", Name = "Sound1", FilePath = @"C:\test\sound1.mp3" },
            new() { Id = "test2", Name = "Sound2", FilePath = @"C:\test\sound2.wav" }
        };
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_fixture.Paths.LibraryFilePath, json);

        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Equal(2, service.Sounds.Count);
        Assert.Contains(service.Sounds, s => s.Name == "Sound1");
        Assert.Contains(service.Sounds, s => s.Name == "Sound2");
    }

    [Fact]
    public async Task LoadAsync_WrappedFormat_LoadsCorrectly()
    {
        var doc = new LibraryDocument
        {
            SchemaVersion = 1,
            Sounds = new List<SoundLibraryItem>
            {
                new() { Id = "test1", Name = "Wrapped1", FilePath = @"C:\test\w1.mp3" }
            }
        };
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_fixture.Paths.LibraryFilePath, json);

        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.Single(service.Sounds);
        Assert.Equal("Wrapped1", service.Sounds[0].Name);
    }

    [Fact]
    public async Task SaveAsync_WritesWrappedFormat()
    {
        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        var originalCount = service.Sounds.Count;
        service.AddFromFile(@"C:\test\new.mp3", "NewSound", "General", "0:30");
        await service.SaveAsync();

        var savedJson = await File.ReadAllTextAsync(_fixture.Paths.LibraryFilePath);
        Assert.StartsWith("{", savedJson.TrimStart());

        var doc = JsonSerializer.Deserialize<LibraryDocument>(savedJson);
        Assert.NotNull(doc);
        Assert.Equal(1, doc.SchemaVersion);
        Assert.Equal(originalCount + 1, doc.Sounds.Count);
    }

    [Fact]
    public void AddFromFile_DuplicatePath_ReturnsExistingSound()
    {
        var service = new LibraryService(_fixture.Paths);

        var first = service.AddFromFile(@"C:\test\dup.mp3", "Original", "General", "0:10");
        var second = service.AddFromFile(@"C:\test\dup.mp3", "Duplicate", "General", "0:20");

        Assert.Same(first, second);
        Assert.Equal("Original", first.Name);
        Assert.Single(service.Sounds);
    }

    [Fact]
    public void AddFromFile_NonDuplicate_AddsSound()
    {
        var service = new LibraryService(_fixture.Paths);

        var s1 = service.AddFromFile(@"C:\test\a.mp3", "A", "Cat1", "0:10");
        var s2 = service.AddFromFile(@"C:\test\b.mp3", "B", "Cat2", "0:20");

        Assert.NotSame(s1, s2);
        Assert.Equal(2, service.Sounds.Count);
    }

    [Fact]
    public void AddFromFile_SetsProperties()
    {
        var service = new LibraryService(_fixture.Paths);

        var sound = service.AddFromFile(@"C:\test\prop.mp3", "PropTest", "MyCategory", "1:30");

        Assert.NotEmpty(sound.Id);
        Assert.Equal("PropTest", sound.Name);
        Assert.Equal(@"C:\test\prop.mp3", sound.FilePath);
        Assert.Equal("MyCategory", sound.Category);
        Assert.Equal("1:30", sound.Duration);
        Assert.Empty(sound.Hotkey);
        Assert.False(sound.IsFavorite);
    }

    [Fact]
    public void AddFromPaths_Distinct_ImportsAll()
    {
        var service = new LibraryService(_fixture.Paths);
        var files = CreateTempAudioFiles("a.mp3", "b.wav", "c.ogg");

        var imported = service.AddFromPaths(files, "Music").ToList();

        Assert.Equal(3, imported.Count);
        Assert.Equal(3, service.Sounds.Count);
    }

    [Fact]
    public void AddFromPaths_Duplicates_Skipped()
    {
        var service = new LibraryService(_fixture.Paths);
        var file = CreateTempAudioFile("dup.mp3");
        var paths = new[] { file, file, CreateTempAudioFile("unique.mp3") };

        var imported = service.AddFromPaths(paths, "General").ToList();

        Assert.Equal(2, imported.Count);
        Assert.Equal(2, service.Sounds.Count);
    }

    [Fact]
    public void ImportPaths_ReturnsCorrectCounts()
    {
        var service = new LibraryService(_fixture.Paths);
        var files = CreateTempAudioFiles("a.mp3", "b.wav", "c.aac");

        var result = service.ImportPaths(files, "Default");

        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(0, result.SkippedDuplicateCount);
        Assert.Equal(3, result.ImportedItems.Count);
    }

    [Fact]
    public void ImportPaths_WithDuplicates_ReportsSkips()
    {
        var service = new LibraryService(_fixture.Paths);

        var fileA = CreateTempAudioFile("a.mp3");
        service.AddFromFile(fileA, "A", "Cat", "0:10");
        var paths = new[] { fileA, CreateTempAudioFile("b.mp3") };
        var result = service.ImportPaths(paths, "Cat");

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedDuplicateCount);
    }

    private string CreateTempAudioFile(string name)
    {
        var path = Path.Combine(_fixture.TempRoot, name);
        File.WriteAllText(path, "dummy content");
        return path;
    }

    private string[] CreateTempAudioFiles(params string[] names)
    {
        return names.Select(CreateTempAudioFile).ToArray();
    }

    [Fact]
    public void NormalizeCategories_ReassignsUnknown()
    {
        var service = new LibraryService(_fixture.Paths);

        service.AddFromFile(@"C:\test\a.mp3", "A", "ValidCat", "0:10");
        service.AddFromFile(@"C:\test\b.mp3", "B", "InvalidCat", "0:10");
        service.AddFromFile(@"C:\test\c.mp3", "C", "Uncategorized", "0:10");

        service.NormalizeCategories(new[] { "ValidCat", "Uncategorized" });

        Assert.Equal("ValidCat", service.Sounds[0].Category);
        Assert.Equal("Uncategorized", service.Sounds[1].Category);
        Assert.Equal("Uncategorized", service.Sounds[2].Category);
    }

    [Fact]
    public void RenameCategory_UpdatesAllSounds()
    {
        var service = new LibraryService(_fixture.Paths);

        service.AddFromFile(@"C:\test\a.mp3", "A", "OldCat", "0:10");
        service.AddFromFile(@"C:\test\b.mp3", "B", "OldCat", "0:10");
        service.AddFromFile(@"C:\test\c.mp3", "C", "OtherCat", "0:10");

        service.RenameCategory("OldCat", "NewCat");

        Assert.Equal(2, service.Sounds.Count(s => s.Category == "NewCat"));
        Assert.Equal(1, service.Sounds.Count(s => s.Category == "OtherCat"));
    }

    [Fact]
    public async Task BackupCorruptedFileAsync_CreatesBackup()
    {
        await File.WriteAllTextAsync(_fixture.Paths.LibraryFilePath, "corrupted json {{{");

        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        Assert.NotNull(service.LastLoadWarning);
        var backupFiles = Directory.GetFiles(_fixture.Paths.BackupsFolder, "library-*.bak");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public async Task SeedDefaults_HasCorrectProperties()
    {
        var service = new LibraryService(_fixture.Paths);
        await service.LoadAsync();

        var airhorn = service.Sounds.First(s => s.Name == "Airhorn");
        Assert.True(airhorn.IsMissingFile);
        Assert.True(airhorn.IsFavorite);
        Assert.Equal("F8", airhorn.Hotkey);
        Assert.Equal("Memes", airhorn.Category);
        Assert.Equal(1.0, airhorn.Volume);
    }

    [Fact]
    public void ToViewModel_ToModel_RoundTrip()
    {
        var service = new LibraryService(_fixture.Paths);

        var sound = service.AddFromFile(@"C:\test\roundtrip.mp3", "RoundTrip", "Test", "1:00");
        sound.Volume = 0.75;
        sound.IsFavorite = true;
        sound.PlayCount = 5;
        sound.Normalized = true;
        sound.NormalizationGain = 2.0;
        sound.Hotkey = "Ctrl + F1";

        var item = ToModelViaSave(sound);
        Assert.Equal(sound.Id, item.Id);
        Assert.Equal(sound.Name, item.Name);
        Assert.Equal(sound.FilePath, item.FilePath);
        Assert.Equal(sound.Volume, item.Volume);
        Assert.Equal(sound.IsFavorite, item.IsFavorite);
        Assert.Equal(sound.PlayCount, item.PlayCount);
        Assert.Equal(sound.Normalized, item.Normalized);
        Assert.Equal(sound.NormalizationGain, item.NormalizationGain);
        Assert.Equal(sound.Hotkey, item.Hotkey);
    }

    private static SoundLibraryItem ToModelViaSave(EchoDeck.App.ViewModels.SoundItemViewModel vm)
    {
        return new SoundLibraryItem
        {
            Id = vm.Id,
            Name = vm.Name,
            FilePath = vm.FilePath,
            Category = vm.Category,
            Hotkey = vm.Hotkey,
            Duration = vm.Duration,
            Volume = vm.Volume,
            IsFavorite = vm.IsFavorite,
            IsMissingFile = vm.IsMissingFile,
            Normalized = vm.Normalized,
            NormalizationGain = vm.NormalizationGain,
            PlayCount = vm.PlayCount,
            CreatedAt = vm.CreatedAt,
            LastPlayedAt = vm.LastPlayedAt
        };
    }
}
