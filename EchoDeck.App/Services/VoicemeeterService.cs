using NAudio.CoreAudioApi;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class VoicemeeterService : IVoicemeeterService, IDisposable
{
    private readonly List<AudioDeviceInfo> _availableOutputs = [];
    private readonly LoggingService _loggingService;
    private System.Threading.Timer? _reconnectTimer;
    private bool _disposed;
    private const int InitialRetryMs = 2000;
    private const int NormalRetryMs = 5000;

    public VoicemeeterService(LoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public VoicemeeterState State { get; private set; } = VoicemeeterState.Unknown;
    public IReadOnlyList<AudioDeviceInfo> AvailableVoicemeeterOutputs => _availableOutputs;

    public Task<VoicemeeterDetectionResult> DetectAsync()
    {
        _availableOutputs.Clear();

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                using var endpoint = ep;
                if (endpoint.FriendlyName.Contains("Voicemeeter", StringComparison.OrdinalIgnoreCase))
                {
                    _availableOutputs.Add(new AudioDeviceInfo
                    {
                        Id = endpoint.ID,
                        Name = endpoint.FriendlyName,
                        Kind = AudioDeviceKind.Output,
                        IsDefault = false,
                        IsVoicemeeter = true,
                        IsAvailable = true
                    });
                }
            }
        }
        catch
        {
        }

        var isDetected = _availableOutputs.Count > 0;
        State = isDetected ? VoicemeeterState.Detected : VoicemeeterState.NotDetected;

        return Task.FromResult(new VoicemeeterDetectionResult
        {
            IsDetected = isDetected,
            Edition = _availableOutputs.Any(device => device.Name.Contains("Potato", StringComparison.OrdinalIgnoreCase))
                ? "Potato"
                : _availableOutputs.Any(device => device.Name.Contains("Banana", StringComparison.OrdinalIgnoreCase))
                    ? "Banana"
                    : isDetected ? "Standard" : null,
            Outputs = _availableOutputs.ToList(),
            Message = isDetected
                ? $"Voicemeeter detected: {string.Join(", ", _availableOutputs.Select(device => device.Name))}"
                : "Voicemeeter not detected."
        });
    }

    public async Task<bool> ConnectAsync()
    {
        var result = await DetectAsync();
        if (result.IsDetected)
        {
            State = VoicemeeterState.Connected;
            await _loggingService.LogVoicemeeter($"Voicemeeter connected. Edition: {result.Edition}");
            StopReconnect();
            return true;
        }

        State = VoicemeeterState.NotDetected;
        StartReconnect(InitialRetryMs);
        return false;
    }

    public Task DisconnectAsync()
    {
        State = VoicemeeterState.Disconnected;
        StopReconnect();
        return Task.CompletedTask;
    }

    public async Task<bool> ReconnectAsync()
    {
        State = VoicemeeterState.Reconnecting;
        await _loggingService.LogVoicemeeter("Voicemeeter reconnecting...");

        var result = await DetectAsync();
        if (result.IsDetected)
        {
            State = VoicemeeterState.Connected;
            await _loggingService.LogVoicemeeter($"Voicemeeter reconnected. Edition: {result.Edition}");
            StopReconnect();
            return true;
        }

        State = VoicemeeterState.Disconnected;
        StartReconnect(NormalRetryMs);
        return false;
    }

    public AudioDeviceInfo? GetPreferredOutput(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PreferredVoicemeeterOutputDeviceId))
        {
            return _availableOutputs.FirstOrDefault(device => string.Equals(device.Id, settings.PreferredVoicemeeterOutputDeviceId, StringComparison.OrdinalIgnoreCase));
        }

        return _availableOutputs.FirstOrDefault()
            ?? _availableOutputs.FirstOrDefault();
    }

    private void StartReconnect(int delayMs)
    {
        StopReconnect();
        if (_disposed) return;
        _reconnectTimer = new System.Threading.Timer(
            async _ =>
            {
                try
                {
                    await ReconnectAsync();
                }
                catch (Exception ex)
                {
                    await _loggingService.LogVoicemeeter($"Reconnect error: {ex.Message}");
                }
            },
            null,
            delayMs,
            Timeout.Infinite);
    }

    private void StopReconnect()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopReconnect();
    }
}
