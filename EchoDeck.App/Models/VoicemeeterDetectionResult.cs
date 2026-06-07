namespace EchoDeck.App.Models;

public sealed class VoicemeeterDetectionResult
{
    public bool IsDetected { get; set; }
    public string? Edition { get; set; }
    public List<AudioDeviceInfo> Outputs { get; set; } = new();
    public string? Message { get; set; }
}
