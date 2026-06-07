using System.IO;
using System.Text.Json;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.Tests.Services;

public sealed class SettingsServiceTests : IClassFixture<TestFixture>
{
    private readonly TestFixture _fixture;

    public SettingsServiceTests(TestFixture fixture)
    {
        _fixture = fixture;
        foreach (var file in Directory.GetFiles(_fixture.Paths.DataFolder, "*.json"))
            try { File.Delete(file); } catch { }
    }

    [Fact]
    public async Task LoadAsync_NoFile_IsFirstRun()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        Assert.True(service.IsFirstRun);
        Assert.NotNull(service.Current);
    }

    [Fact]
    public async Task LoadAsync_NoFile_CreatesDefaultSettings()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        Assert.True(File.Exists(_fixture.Paths.SettingsFilePath));
    }

    [Fact]
    public async Task LoadAsync_ExistingFile_LoadsCorrectly()
    {
        var settings = new AppSettings
        {
            Theme = "Light",
            MasterVolume = 0.5,
            StopAllHotkey = "F11"
        };
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_fixture.Paths.SettingsFilePath, json);

        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        Assert.False(service.IsFirstRun);
        Assert.Equal("Light", service.Current.Theme);
        Assert.Equal(0.5, service.Current.MasterVolume);
        Assert.Equal("F11", service.Current.StopAllHotkey);
    }

    [Fact]
    public async Task SaveAsync_PersistsChanges()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        service.Current.Theme = "System";
        service.Current.MasterVolume = 0.9;
        await service.SaveAsync();

        var json = await File.ReadAllTextAsync(_fixture.Paths.SettingsFilePath);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(loaded);
        Assert.Equal("System", loaded.Theme);
        Assert.Equal(0.9, loaded.MasterVolume);
    }

    [Fact]
    public async Task SaveAsync_IsAtomic()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        service.Current.Theme = "Light";
        await service.SaveAsync();

        Assert.False(File.Exists(_fixture.Paths.SettingsFilePath + ".tmp"));
    }

    [Fact]
    public async Task ResetAsync_RestoresDefaults()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        service.Current.Theme = "Light";
        service.Current.MasterVolume = 0.1;
        await service.ResetAsync();

        Assert.Equal("Dark", service.Current.Theme);
        Assert.Equal(0.85, service.Current.MasterVolume);
    }

    [Fact]
    public async Task ResetAsync_CreatesBackup()
    {
        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        service.Current.Theme = "Custom";
        await service.SaveAsync();
        await service.ResetAsync();

        var backupFiles = Directory.GetFiles(_fixture.Paths.BackupsFolder, "settings-reset-*.bak");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_RecoversWithWarning()
    {
        await File.WriteAllTextAsync(_fixture.Paths.SettingsFilePath, "not valid json {{{");

        var service = new SettingsService(_fixture.Paths);
        await service.LoadAsync();

        Assert.NotNull(service.LastLoadWarning);
        Assert.NotNull(service.Current);

        var backupFiles = Directory.GetFiles(_fixture.Paths.BackupsFolder, "settings-*.bak");
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public async Task FirstRun_OnlyOnFirstLoad()
    {
        var service1 = new SettingsService(_fixture.Paths);
        await service1.LoadAsync();
        Assert.True(service1.IsFirstRun);

        var service2 = new SettingsService(_fixture.Paths);
        await service2.LoadAsync();
        Assert.False(service2.IsFirstRun);
    }
}
