using System.IO;
using System.Text.Json;

namespace EchoDeck.App.Infrastructure;

public sealed class JsonFileStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public JsonFileStore(string filePath)
    {
        _filePath = filePath;
    }

    public bool FileExists() => File.Exists(_filePath);

    public async Task<T?> LoadAsync<T>() where T : new()
    {
        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    public async Task<T> LoadOrCreateAsync<T>() where T : new()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = new T();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            return await LoadAsync<T>() ?? new T();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            await BackupCorruptedFileAsync();
            var defaults = new T();
            await SaveAsync(defaults);
            return defaults;
        }
    }

    public async Task SaveAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, SerializerOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private async Task BackupCorruptedFileAsync()
    {
        try
        {
            var backupDir = Path.Combine(
                Path.GetDirectoryName(_filePath) ?? ".",
                "backups");
            Directory.CreateDirectory(backupDir);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var backupPath = Path.Combine(backupDir,
                $"{Path.GetFileNameWithoutExtension(_filePath)}_{timestamp}.json.bak");
            await Task.Run(() => File.Copy(_filePath, backupPath, overwrite: true));
        }
        catch
        {
        }
    }
}
