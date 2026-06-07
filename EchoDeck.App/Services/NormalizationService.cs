using System.IO;
using NAudio.Wave;
using EchoDeck.App.ViewModels;

namespace EchoDeck.App.Services;

public sealed class NormalizationService
{
    private const double TargetRmsLevel = 0.2;

    public async Task<double> AnalyzeGainAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return 1.0;

        return await Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(filePath);
                var buffer = new float[4096];
                var sumSquares = 0.0;
                var totalSamples = 0;
                var bytesRead = 0;

                while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (var i = 0; i < bytesRead; i++)
                    {
                        sumSquares += buffer[i] * buffer[i];
                        totalSamples++;
                    }
                }

                if (totalSamples == 0)
                    return 1.0;

                var rms = Math.Sqrt(sumSquares / totalSamples);
                if (rms < 0.001)
                    return 1.0;

                var gain = TargetRmsLevel / rms;
                return Math.Clamp(gain, 0.1, 3.0);
            }
            catch
            {
                return 1.0;
            }
        });
    }

    public async Task NormalizeAsync(SoundItemViewModel sound)
    {
        var gain = await AnalyzeGainAsync(sound.FilePath);
        sound.NormalizationGain = gain;
        sound.Normalized = true;
    }

    public void ResetNormalization(SoundItemViewModel sound)
    {
        sound.NormalizationGain = 1.0;
        sound.Normalized = false;
    }
}