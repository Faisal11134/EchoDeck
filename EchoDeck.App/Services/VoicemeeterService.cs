using NAudio.CoreAudioApi;
using EchoDeck.App.Models;
using VoiceMeeter;
using Voicemeeter;

namespace EchoDeck.App.Services;

public sealed class VoicemeeterService : IVoicemeeterService, IDisposable
{
    private readonly List<AudioDeviceInfo> _availableOutputs = [];
    private readonly LoggingService _loggingService;
    private System.Threading.Timer? _reconnectTimer;
    private bool _remoteLoggedIn;
    private RunVoicemeeterParam _vmEdition = RunVoicemeeterParam.None;
    private int _vmStripIndex = -1;
    private bool _hasAttemptedRemoteLogin;
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

        _vmEdition = isDetected
            ? _availableOutputs.Any(device => device.Name.Contains("Potato", StringComparison.OrdinalIgnoreCase))
                ? RunVoicemeeterParam.VoicemeeterPotato
                : _availableOutputs.Any(device => device.Name.Contains("Banana", StringComparison.OrdinalIgnoreCase))
                    ? RunVoicemeeterParam.VoicemeeterBanana
                    : RunVoicemeeterParam.Voicemeeter
            : RunVoicemeeterParam.None;

        return Task.FromResult(new VoicemeeterDetectionResult
        {
            IsDetected = isDetected,
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
            await LoginToRemoteApi();
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
        LogoutFromRemoteApi();
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
            await LoginToRemoteApi();
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

    public void SetMonitorVolume(double volume)
    {
        if (!_remoteLoggedIn || _vmStripIndex < 0) return;
        try
        {
            Remote.SetParameter($"Strip[{_vmStripIndex}].A1", (float)Math.Clamp(volume, 0.0, 1.0));
        }
        catch
        {
        }
    }

    public void SetOutputVolume(double volume)
    {
        if (!_remoteLoggedIn || _vmStripIndex < 0) return;
        try
        {
            Remote.SetParameter($"Strip[{_vmStripIndex}].B1", (float)Math.Clamp(volume, 0.0, 1.0));
        }
        catch
        {
        }
    }

    private async Task LoginToRemoteApi()
    {
        if (_hasAttemptedRemoteLogin) return;
        _hasAttemptedRemoteLogin = true;

        try
        {
            Remote.Start(_vmEdition);
            var loggedIn = await Remote.Login(_vmEdition, retry: true);
            if (loggedIn)
            {
                _remoteLoggedIn = true;
                _vmStripIndex = FindEchoDeckStripIndex();
                await _loggingService.LogVoicemeeter($"Voicemeeter Remote API logged in. EchoDeck strip index: {_vmStripIndex}");
            }
            else
            {
                await _loggingService.LogVoicemeeter("Voicemeeter Remote API login failed");
                _vmStripIndex = -1;
            }
        }
        catch (Exception ex)
        {
            await _loggingService.LogVoicemeeter($"Voicemeeter Remote API login failed: {ex.Message}");
            _vmStripIndex = -1;
        }
    }

    private void LogoutFromRemoteApi()
    {
        try
        {
            Remote.Shutdown();
        }
        catch
        {
        }
        _remoteLoggedIn = false;
        _vmStripIndex = -1;
        _hasAttemptedRemoteLogin = false;
    }

    private int FindEchoDeckStripIndex()
    {
        if (!_remoteLoggedIn) return -1;

        var stripCount = _vmEdition == RunVoicemeeterParam.VoicemeeterPotato ? 8
            : _vmEdition == RunVoicemeeterParam.VoicemeeterBanana ? 6 : 4;
        var firstVirtual = _vmEdition == RunVoicemeeterParam.VoicemeeterPotato ? 5
            : _vmEdition == RunVoicemeeterParam.VoicemeeterBanana ? 4 : 3;

        try
        {
            var preferredName = GetPreferredOutputDeviceFriendlyName();
            for (int i = firstVirtual; i < stripCount; i++)
            {
                try
                {
                    var label = Remote.GetTextParameter($"Strip[{i}].Label");
                    if (!string.IsNullOrWhiteSpace(label) && preferredName.Contains(label, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return firstVirtual;
    }

    private string GetPreferredOutputDeviceFriendlyName()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var defaultEndpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return defaultEndpoint.FriendlyName;
        }
        catch
        {
            return string.Empty;
        }
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
        LogoutFromRemoteApi();
    }
}
