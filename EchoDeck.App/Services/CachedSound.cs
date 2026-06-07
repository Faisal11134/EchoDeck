using NAudio.Wave;

namespace EchoDeck.App.Services;

public sealed class CachedSound
{
    public float[] Samples { get; }
    public WaveFormat WaveFormat { get; }

    public CachedSound(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        WaveFormat = reader.WaveFormat;
        var sampleCount = (int)(reader.Length / 4);
        Samples = new float[sampleCount];
        var totalRead = 0;
        while (totalRead < sampleCount)
        {
            totalRead += reader.Read(Samples, totalRead, sampleCount - totalRead);
        }
    }
}
