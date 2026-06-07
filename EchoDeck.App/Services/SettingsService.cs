using System.IO;
using System.Text.Json;
using EchoDeck.App.Infrastructure;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public AppSettings Current { get; private set; } = new();
    public string? LastLoadWarning { get; private set; }
    public bool IsFirstRun { get; private set; }

    public SettingsService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task LoadAsync()
    {
        LastLoadWarning = null;

        if (!File.Exists(_paths.SettingsFilePath))
        {
            IsFirstRun = true;
            Current = new AppSettings();
            await SaveAsync();
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_paths.SettingsFilePath);
            Current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            await BackupCorruptedFileAsync(_paths.SettingsFilePath, "settings");
            Current = new AppSettings();
            LastLoadWarning = "Recovered settings.json from corruption.";
        }
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(Current, SerializerOptions);
        var tempPath = _paths.SettingsFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _paths.SettingsFilePath, overwrite: true);
    }

    public async Task ResetAsync()
    {
        if (File.Exists(_paths.SettingsFilePath))
        {
            await BackupCorruptedFileAsync(_paths.SettingsFilePath, "settings-reset");
        }

        Current = new AppSettings();
        await SaveAsync();
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
}
