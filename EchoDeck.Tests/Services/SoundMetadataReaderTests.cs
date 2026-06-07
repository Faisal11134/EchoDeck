using System.IO;
using EchoDeck.App.Services;

namespace EchoDeck.Tests.Services;

public sealed class SoundMetadataReaderTests
{
    [Fact]
    public async Task TryReadDurationAsync_EmptyPath_ReturnsDefault()
    {
        var result = await SoundMetadataReader.TryReadDurationAsync(string.Empty);
        Assert.Equal("0:00", result);
    }

    [Fact]
    public async Task TryReadDurationAsync_NonExistentFile_ReturnsDefault()
    {
        var result = await SoundMetadataReader.TryReadDurationAsync(@"C:\nonexistent_file_xyz.mp3");
        Assert.Equal("0:00", result);
    }

    [Fact]
    public async Task TryReadDurationAsync_InvalidPath_ReturnsDefault()
    {
        var result = await SoundMetadataReader.TryReadDurationAsync(@"\\invalid\path\file.mp3");
        Assert.Equal("0:00", result);
    }

    [Fact]
    public async Task TryReadDurationAsync_NullPath_ReturnsDefault()
    {
        var result = await SoundMetadataReader.TryReadDurationAsync(null!);
        Assert.Equal("0:00", result);
    }

    [Fact]
    public async Task TryReadDurationAsync_SmallSilentWav_ReturnsDuration()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "SBVM_Test_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            CreateSilentWav(filePath, sampleRate: 44100, durationSeconds: 2);

            var result = await SoundMetadataReader.TryReadDurationAsync(filePath);
            Assert.Equal("0:02", result);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    [Fact]
    public async Task TryReadDurationAsync_30SecondWav_ReturnsDuration()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "SBVM_Test_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            CreateSilentWav(filePath, sampleRate: 22050, durationSeconds: 30);

            var result = await SoundMetadataReader.TryReadDurationAsync(filePath);
            Assert.Equal("0:30", result);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    [Fact]
    public async Task TryReadDurationAsync_1SecondWav_ReturnsDuration()
    {
        var filePath = Path.Combine(Path.GetTempPath(), "SBVM_Test_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            CreateSilentWav(filePath, sampleRate: 8000, durationSeconds: 1);

            var result = await SoundMetadataReader.TryReadDurationAsync(filePath);
            Assert.Equal("0:01", result);
        }
        finally
        {
            try { File.Delete(filePath); } catch { }
        }
    }

    private static void CreateSilentWav(string filePath, int sampleRate, int durationSeconds)
    {
        var numSamples = sampleRate * durationSeconds;
        var bitsPerSample = 16;
        var numChannels = 1;
        var byteRate = sampleRate * numChannels * bitsPerSample / 8;
        var blockAlign = numChannels * bitsPerSample / 8;
        var dataSize = numSamples * blockAlign;
        var fileSize = 36 + dataSize;

        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
        writer.Write(fileSize);
        writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
        writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)numChannels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)blockAlign);
        writer.Write((short)bitsPerSample);
        writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
        writer.Write(dataSize);

        var sampleBytes = new byte[dataSize];
        writer.Write(sampleBytes);
    }
}
