namespace EchoDeck.App.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public string Language { get; set; } = "Arabic";
    public string Theme { get; set; } = "Dark";
    public double MasterVolume { get; set; } = 0.85;
    public bool EnableNormalization { get; set; } = true;
    public bool AllowOverlap { get; set; } = false;
    public string StopAllHotkey { get; set; } = "F12";
    public string StartupView { get; set; } = "Library";
    public bool MinimizeToTray { get; set; } = true;
    public string CloseButtonBehavior { get; set; } = "MinimizeToTray";
    public string? PreferredVoicemeeterOutputDeviceId { get; set; }
    public bool VirtualMicEnabled { get; set; }
    public string? SelectedInputDeviceId { get; set; }
    public double VirtualMicVolume { get; set; } = 0.85;
    public double MonitorVolume { get; set; } = 1.0;
    public double OutputVolume { get; set; } = 1.0;
    public bool AutoReconnectVoicemeeter { get; set; } = true;
    public string DefaultCategoryId { get; set; } = "Uncategorized";
    public string? SelectedWatchedFolder { get; set; }
    public string HotkeyConflictBehavior { get; set; } = "Warn";
    public Dictionary<string, string> PerSoundHotkeys { get; set; } = new();
}
