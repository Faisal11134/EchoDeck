using NAudio.Wave;

namespace EchoDeck.App.Services;

public sealed class PlaybackInstance : IDisposable
{
    private bool _disposed;

    public PlaybackInstance(Guid id, string soundId, string soundName, string filePath, string routeName, IWavePlayer outputDevice, AudioFileReader reader, double perSoundFactor)
    {
        Id = id;
        SoundId = soundId;
        SoundName = soundName;
        FilePath = filePath;
        RouteName = routeName;
        OutputDevice = outputDevice;
        Reader = reader;
        PerSoundFactor = perSoundFactor;
    }

    public Guid Id { get; }
    public string SoundId { get; }
    public string SoundName { get; }
    public string FilePath { get; }
    public string RouteName { get; }
    public IWavePlayer OutputDevice { get; }
    public AudioFileReader Reader { get; }
    public double PerSoundFactor { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        OutputDevice.Dispose();
        Reader.Dispose();
    }
}
