namespace EchoDeck.App.Models;

public sealed class AudioDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AudioDeviceKind Kind { get; set; }
    public bool IsDefault { get; set; }
    public bool IsVoicemeeter { get; set; }
    public bool IsAvailable { get; set; } = true;

    public override string ToString() => Name;
}
