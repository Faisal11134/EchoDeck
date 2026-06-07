using System.Collections.Concurrent;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class AudioMixerService : IDisposable
{
    private sealed class ActiveSound
    {
        public CachedSound Sound { get; }
        public int Position { get; set; }
        public float Volume { get; set; }

        public ActiveSound(CachedSound sound, float volume)
        {
            Sound = sound;
            Volume = volume;
            Position = 0;
        }
    }

    private readonly LoggingService _loggingService;
    private readonly ConcurrentDictionary<string, CachedSound> _soundCache = new(StringComparer.OrdinalIgnoreCase);
    private WasapiCapture? _micCapture;
    private WaveFormat? _micCaptureFormat;
    private BufferedWaveProvider? _mixerBuffer;
    private WasapiOut? _cableOutput;
    private WaveFormat _mixFormat;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isRunning;
    private readonly Dictionary<string, ActiveSound> _activeSounds = new(StringComparer.OrdinalIgnoreCase);
    private System.Threading.Timer? _mixTimer;
    private float _micVolume = 1.0f;
    private float _soundVolume = 1.0f;

    public event EventHandler<EventArgs>? StatusChanged;
    public event EventHandler<string>? ErrorOccurred;

    public AudioMixerService(LoggingService loggingService)
    {
        _loggingService = loggingService;
        _mixFormat = new WaveFormat(48000, 16, 2);
    }

    public bool IsRunning => _isRunning;
    public bool HasVirtualCable { get; private set; }
    public string StatusMessage { get; private set; } = "Virtual mic not active";
    public string ActiveMicName { get; private set; } = string.Empty;
    public string ActiveCableName { get; private set; } = string.Empty;

    public bool DetectVirtualCable()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                using var endpoint = ep;
                if (endpoint.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase))
                {
                    HasVirtualCable = true;
                    return true;
                }
            }
        }
        catch { }
        HasVirtualCable = false;
        return false;
    }

    public List<AudioDeviceInfo> GetVirtualCableDevices()
    {
        var results = new List<AudioDeviceInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                using var endpoint = ep;
                if (endpoint.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                    endpoint.FriendlyName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new AudioDeviceInfo
                    {
                        Id = endpoint.ID,
                        Name = endpoint.FriendlyName,
                        Kind = AudioDeviceKind.Output,
                        IsDefault = false,
                        IsVoicemeeter = endpoint.FriendlyName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase),
                        IsAvailable = true
                    });
                }
            }
        }
        catch { }
        return results;
    }

    public List<AudioDeviceInfo> GetInputDevices()
    {
        var results = new List<AudioDeviceInfo>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                using var endpoint = ep;
                results.Add(new AudioDeviceInfo
                {
                    Id = endpoint.ID,
                    Name = endpoint.FriendlyName,
                    Kind = AudioDeviceKind.Input,
                    IsDefault = false,
                    IsVoicemeeter = endpoint.FriendlyName.Contains("VoiceMeeter", StringComparison.OrdinalIgnoreCase) ||
                                    endpoint.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                                    endpoint.FriendlyName.Contains("CABLE", StringComparison.OrdinalIgnoreCase),
                    IsAvailable = true
                });
            }
        }
        catch { }
        return results;
    }

    public bool Start(string micDeviceId, string? virtualCableDeviceId = null)
    {
        if (_isRunning) Stop();

        try
        {
            using var enumerator = new MMDeviceEnumerator();

            MMDevice? micDevice;
            try
            {
                micDevice = enumerator.GetDevice(micDeviceId);
            }
            catch
            {
                micDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            }
            if (micDevice is null)
            {
                StatusMessage = "No microphone device available.";
                ErrorOccurred?.Invoke(this, StatusMessage);
                StatusChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }
            ActiveMicName = micDevice.FriendlyName;

            try
            {
                var nativeFormat = micDevice.AudioClient.MixFormat;
                _mixFormat = new WaveFormat(nativeFormat.SampleRate, 16, nativeFormat.Channels);
            }
            catch
            {
                _mixFormat = new WaveFormat(48000, 16, 2);
            }

            _mixerBuffer = new BufferedWaveProvider(_mixFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(100),
                DiscardOnBufferOverflow = true
            };

            MMDevice? cableDevice = null;
            if (!string.IsNullOrWhiteSpace(virtualCableDeviceId))
            {
                try { cableDevice = enumerator.GetDevice(virtualCableDeviceId); }
                catch { }
            }
            if (cableDevice is null)
            {
                var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var ep in endpoints)
                {
                    if (ep.FriendlyName.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase) ||
                        ep.FriendlyName.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase) ||
                        ep.FriendlyName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
                    {
                        cableDevice = ep;
                        break;
                    }
                }
            }
            if (cableDevice is null)
            {
                var allCables = GetVirtualCableDevices();
                if (allCables.Count > 0)
                {
                    try { cableDevice = enumerator.GetDevice(allCables[0].Id); } catch { }
                }
            }

            if (cableDevice is not null)
            {
                ActiveCableName = cableDevice.FriendlyName;
                _cableOutput = new WasapiOut(cableDevice, AudioClientShareMode.Shared, true, 50);
                _cableOutput.Init(_mixerBuffer);
                _cableOutput.Play();
                HasVirtualCable = true;
            }
            else
            {
                ActiveCableName = "No virtual cable (listening locally only)";
                HasVirtualCable = false;
            }

            _micCapture = new WasapiCapture(micDevice, true, 50);
            _micCaptureFormat = _micCapture.WaveFormat;
            _micCapture.DataAvailable += OnMicDataAvailable;
            _micCapture.RecordingStopped += (_, args) =>
            {
                if (args.Exception is not null)
                {
                    ErrorOccurred?.Invoke(this, $"Mic capture error: {args.Exception.Message}");
                }
                _isRunning = false;
                StatusMessage = "Mic capture stopped unexpectedly.";
                StatusChanged?.Invoke(this, EventArgs.Empty);
            };
            _micCapture.StartRecording();

            _mixTimer?.Dispose();
            _mixTimer = new System.Threading.Timer(_ => MixActiveSounds(), null, 0, 30);

            _isRunning = true;
            var cableNote = HasVirtualCable ? $" → {ActiveCableName}" : " (no virtual cable)";
            StatusMessage = $"Virtual mic active: {ActiveMicName}{cableNote}";
            StatusChanged?.Invoke(this, EventArgs.Empty);
            _ = _loggingService.LogVoicemeeter(StatusMessage);
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start virtual mic: {ex.Message}";
            ErrorOccurred?.Invoke(this, StatusMessage);
            StatusChanged?.Invoke(this, EventArgs.Empty);
            _ = _loggingService.LogVoicemeeter(StatusMessage);
            return false;
        }
    }

    public void Stop()
    {
        if (!_isRunning) return;

        try { _micCapture?.StopRecording(); } catch { }
        try { _micCapture?.Dispose(); } catch { }
        _micCapture = null;
        _micCaptureFormat = null;

        _mixTimer?.Dispose();
        _mixTimer = null;

        try { _cableOutput?.Stop(); } catch { }
        try { _cableOutput?.Dispose(); } catch { }
        _cableOutput = null;

        lock (_lock)
        {
            _activeSounds.Clear();
        }

        _isRunning = false;
        ActiveMicName = string.Empty;
        ActiveCableName = string.Empty;
        _mixerBuffer = null;
        StatusMessage = "Virtual mic stopped.";
        StatusChanged?.Invoke(this, EventArgs.Empty);
        _ = _loggingService.LogVoicemeeter("Virtual mic stopped.");
    }

    public void AddSoundReader(string filePath, double volume)
    {
        if (!_isRunning || _disposed) return;
        lock (_lock)
        {
            if (_activeSounds.ContainsKey(filePath))
                return;
        }
        var cached = _soundCache.GetOrAdd(filePath, path => new CachedSound(path));
        var vol = Math.Clamp((float)volume, 0.0f, 2.0f);
        lock (_lock)
        {
            if (!_activeSounds.ContainsKey(filePath))
                _activeSounds[filePath] = new ActiveSound(cached, vol);
        }
    }

    public void RemoveSoundReader(string filePath)
    {
        lock (_lock)
        {
            _activeSounds.Remove(filePath);
        }
    }

    public void SetMicVolume(double volume)
    {
        _micVolume = Math.Clamp((float)volume, 0.0f, 2.0f);
    }

    public void SetSoundVolume(double volume)
    {
        _soundVolume = Math.Clamp((float)volume, 0.0f, 2.0f);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private void OnMicDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_disposed || _mixerBuffer is null) return;

        byte[] buffer;
        int bytesRecorded;

        if (_micCaptureFormat is not null &&
            _micCaptureFormat.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            buffer = ConvertIeeeFloatToPcm16(e.Buffer, e.BytesRecorded);
            bytesRecorded = buffer.Length;
        }
        else
        {
            buffer = e.Buffer;
            bytesRecorded = e.BytesRecorded;
        }

        var micSamples = ApplyVolume(buffer, bytesRecorded, _micVolume);
        _mixerBuffer.AddSamples(micSamples, 0, micSamples.Length);
    }

    private void MixActiveSounds()
    {
        if (_disposed || _mixerBuffer is null) return;

        const int chunkFrames = 1470; // ~30ms at 48000Hz, ~33ms at 44100Hz
        var maxChannels = _mixFormat.Channels;
        var targetFrameSize = maxChannels * 2;

        List<string> completed = new();
        float[] mixBuffer;

        lock (_lock)
        {
            if (_activeSounds.Count == 0) return;

            var totalFrames = chunkFrames;
            mixBuffer = new float[totalFrames * maxChannels];

            foreach (var kvp in _activeSounds)
            {
                var active = kvp.Value;
                var src = active.Sound;
                var srcChannels = src.WaveFormat.Channels;
                var srcRate = src.WaveFormat.SampleRate;
                var vol = active.Volume * _soundVolume;

                var srcFramesToUse = srcChannels > 0 && srcRate > 0
                    ? (int)((long)totalFrames * srcRate / _mixFormat.SampleRate)
                    : totalFrames;
                var srcSamplesNeeded = srcFramesToUse * srcChannels;
                var samplesAvail = src.Samples.Length - active.Position;
                var samplesToRead = Math.Min(srcSamplesNeeded, samplesAvail);

                if (samplesToRead <= 0)
                {
                    completed.Add(kvp.Key);
                    continue;
                }

                for (var i = 0; i < samplesToRead; i++)
                {
                    var srcFrame = (active.Position + i) / srcChannels;
                    var targetFrame = srcRate > 0
                        ? (int)((long)srcFrame * _mixFormat.SampleRate / srcRate)
                        : srcFrame;
                    var targetCh = i % srcChannels;
                    if (targetCh >= maxChannels) targetCh = maxChannels - 1;
                    var targetIdx = targetFrame * maxChannels + targetCh;

                    if (targetIdx < mixBuffer.Length)
                    {
                        mixBuffer[targetIdx] += src.Samples[active.Position + i] * vol;
                    }
                }

                active.Position += samplesToRead;
                if (active.Position >= src.Samples.Length)
                    completed.Add(kvp.Key);
            }

            foreach (var fp in completed)
                _activeSounds.Remove(fp);
        }

        var output = new byte[mixBuffer.Length * 2];
        for (var i = 0; i < mixBuffer.Length; i++)
        {
            var sample = (short)Math.Clamp(mixBuffer[i] * 32767f, -32768f, 32767f);
            output[i * 2] = (byte)(sample & 0xFF);
            output[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        _mixerBuffer.AddSamples(output, 0, output.Length);
    }

    private static byte[] ConvertIeeeFloatToPcm16(byte[] buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / 4;
        var result = new byte[sampleCount * 2];
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BitConverter.ToSingle(buffer, i * 4);
            sample = Math.Clamp(sample, -1.0f, 1.0f);
            var pcm16 = (short)(sample * 32767);
            result[i * 2] = (byte)(pcm16 & 0xFF);
            result[i * 2 + 1] = (byte)((pcm16 >> 8) & 0xFF);
        }
        return result;
    }

    private static byte[] ApplyVolume(byte[] buffer, int bytesRecorded, float volume)
    {
        if (Math.Abs(volume - 1.0f) < 0.001f)
            return bytesRecorded == buffer.Length ? buffer : buffer[..bytesRecorded];

        var result = new byte[bytesRecorded];
        for (var i = 0; i < bytesRecorded; i += 2)
        {
            if (i + 1 >= bytesRecorded) break;
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            sample = (short)Math.Clamp(sample * volume, -32768, 32767);
            result[i] = (byte)(sample & 0xFF);
            result[i + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return result;
    }
}
