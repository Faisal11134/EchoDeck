using System.Collections.Concurrent;
using System.IO;

namespace EchoDeck.App.Services;

public sealed class AudioMetadataCacheService
{
    private readonly ConcurrentDictionary<string, CachedAudioInfo> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CachedAudioInfo GetOrCreate(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var lastWriteTimeUtc = File.GetLastWriteTimeUtc(normalizedPath);
        var length = new FileInfo(normalizedPath).Length;

        return _cache.AddOrUpdate(
            normalizedPath,
            _ => new CachedAudioInfo(normalizedPath, lastWriteTimeUtc, length),
            (_, existing) => existing.IsStale(lastWriteTimeUtc, length)
                ? new CachedAudioInfo(normalizedPath, lastWriteTimeUtc, length)
                : existing);
    }

    public bool TryGet(string filePath, out CachedAudioInfo info)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        return _cache.TryGetValue(normalizedPath, out info!);
    }

    public void Invalidate(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        _cache.TryRemove(normalizedPath, out _);
    }

    public void Clear() => _cache.Clear();
}

public sealed record CachedAudioInfo(string FilePath, DateTime LastWriteTimeUtc, long Length)
{
    public bool IsStale(DateTime lastWriteTimeUtc, long length)
        => LastWriteTimeUtc != lastWriteTimeUtc || Length != length;
}