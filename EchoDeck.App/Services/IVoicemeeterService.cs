using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public interface IVoicemeeterService
{
    VoicemeeterState State { get; }
    IReadOnlyList<AudioDeviceInfo> AvailableVoicemeeterOutputs { get; }

    Task<VoicemeeterDetectionResult> DetectAsync();
    Task<bool> ConnectAsync();
    Task DisconnectAsync();
    Task<bool> ReconnectAsync();
    AudioDeviceInfo? GetPreferredOutput(AppSettings settings);
    void SetMonitorVolume(double volume);
    void SetOutputVolume(double volume);
}
