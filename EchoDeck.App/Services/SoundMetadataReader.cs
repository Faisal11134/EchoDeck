using NAudio.Wave;

namespace EchoDeck.App.Services;

public static class SoundMetadataReader
{
    public static async Task<string> TryReadDurationAsync(string filePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var reader = new AudioFileReader(filePath);
                return reader.TotalTime.ToString(@"m\:ss");
            });
        }
        catch
        {
            return "0:00";
        }
    }
}
