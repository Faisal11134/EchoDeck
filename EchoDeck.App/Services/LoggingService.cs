using System.IO;
using EchoDeck.App.Infrastructure;

namespace EchoDeck.App.Services;

public sealed class LoggingService
{
    private const long MaxLogFileSizeBytes = 512 * 1024;
    private const int MaxLogFilesToKeep = 10;
    private readonly string _logFolderPath;
    public string LogFolderPath => _logFolderPath;

    public LoggingService(AppPaths paths)
    {
        _logFolderPath = paths.LogsFolder;
        Directory.CreateDirectory(_logFolderPath);
    }

    public Task LogInformation(string message) => AppendAsync("INFO", "app", message);
    public Task LogWarning(string message) => AppendAsync("WARN", "app", message);
    public Task LogError(Exception exception, string message) => AppendAsync("ERROR", "errors", $"{message}{Environment.NewLine}{exception}");

    public Task LogAudio(string message) => AppendAsync("INFO", "audio", message);
    public Task LogHotkey(string message) => AppendAsync("INFO", "hotkeys", message);
    public Task LogVoicemeeter(string message) => AppendAsync("INFO", "voicemeeter", message);
    public Task LogStorage(string message) => AppendAsync("INFO", "storage", message);

    private async Task AppendAsync(string level, string category, string message)
    {
        await RotateLogIfNeededAsync(category);
        var logFilePath = GetLogPath(category);
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {level} {message}";
        await File.AppendAllTextAsync(logFilePath, line + Environment.NewLine);
    }

    private async Task RotateLogIfNeededAsync(string category)
    {
        var logFilePath = GetLogPath(category);
        Directory.CreateDirectory(_logFolderPath);

        if (File.Exists(logFilePath))
        {
            var info = new FileInfo(logFilePath);
            if (info.Length >= MaxLogFileSizeBytes)
            {
                var archiveName = $"{category}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log";
                var archivePath = Path.Combine(_logFolderPath, archiveName);
                await Task.Run(() => File.Move(logFilePath, archivePath, overwrite: true));
            }
        }

        var logFiles = Directory.GetFiles(_logFolderPath, $"{category}*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToList();

        foreach (var file in logFiles.Skip(MaxLogFilesToKeep))
        {
            try { await Task.Run(() => file.Delete()); } catch { }
        }
    }

    private string GetLogPath(string category) =>
        Path.Combine(_logFolderPath, $"{category}.log");
}