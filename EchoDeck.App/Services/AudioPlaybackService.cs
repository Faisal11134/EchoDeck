using System.Collections.Concurrent;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using EchoDeck.App.Models;
using EchoDeck.App.ViewModels;

namespace EchoDeck.App.Services;

public interface IAudioPlaybackService
{
    string StatusMessage { get; }
    int ActivePlaybackCount { get; }
    IReadOnlyList<PlaybackInstance> ActivePlaybacks { get; }
    event EventHandler<PlaybackStateChangedEventArgs>? ActivePlaybackChanged;
    Task<string> PlayAsync(SoundItemViewModel sound, AppSettings settings);
    Task StopAllAsync();
    Task StopAsync(Guid playbackInstanceId);
    void SetVolume(double volume);
}

public sealed class PlaybackStateChangedEventArgs : EventArgs
{
    public string SoundId { get; }
    public bool IsPlaying { get; }
    public PlaybackStateChangedEventArgs(string soundId, bool isPlaying)
    {
        SoundId = soundId;
        IsPlaying = isPlaying;
    }
}

public sealed class AudioPlaybackService : IAudioPlaybackService, IDisposable
{
    private readonly AudioMetadataCacheService _audioCacheService;
    private readonly AudioMixerService? _mixer;
    private readonly ConcurrentDictionary<Guid, PlaybackInstance> _activePlaybacks = new();

    public AudioPlaybackService(AudioMetadataCacheService audioCacheService, AudioMixerService? mixer = null)
    {
        _audioCacheService = audioCacheService;
        _mixer = mixer;
    }

    public string StatusMessage { get; private set; } = "Playback not started yet.";
    public int ActivePlaybackCount => _activePlaybacks.Count;
    public IReadOnlyList<PlaybackInstance> ActivePlaybacks => _activePlaybacks.Values.ToList();
    public event EventHandler<PlaybackStateChangedEventArgs>? ActivePlaybackChanged;

    public async Task<string> PlayAsync(SoundItemViewModel sound, AppSettings settings)
    {
        try
        {
            sound.IsMissingFile = !string.IsNullOrWhiteSpace(sound.FilePath) && !File.Exists(sound.FilePath);

            if (sound.IsMissingFile || string.IsNullOrWhiteSpace(sound.FilePath) || !File.Exists(sound.FilePath))
            {
                StatusMessage = $"Cannot play missing file: {sound.Name}";
                return StatusMessage;
            }

            var cached = _audioCacheService.GetOrCreate(sound.FilePath);

            var device = ResolveVoicemeeterDevice(settings);
            if (device is null)
            {
                StatusMessage = $"No Voicemeeter output available for {sound.Name}";
                return StatusMessage;
            }

            if (!settings.AllowOverlap)
            {
                await StopSameSoundAsync(sound.Id);
            }

            var perSoundFactor = Math.Clamp(sound.Volume, 0.0, 2.0) *
                (settings.EnableNormalization && sound.Normalized ? Math.Clamp(sound.NormalizationGain, 0.0, 3.0) : 1.0);
            var effectiveVolume = (float)(Math.Clamp(settings.MasterVolume, 0.0, 2.0) * perSoundFactor);

            var reader = new AudioFileReader(sound.FilePath)
            {
                Volume = Math.Clamp(effectiveVolume, 0.0f, 2.0f)
            };

            if (_mixer?.IsRunning == true)
            {
                _mixer.AddSoundReader(sound.FilePath, effectiveVolume);
            }

            sound.PlayCount++;
            sound.LastPlayedAt = DateTime.UtcNow;

            var output = new WasapiOut(device, AudioClientShareMode.Shared, true, 50);
            output.Init(reader);

            var instance = new PlaybackInstance(Guid.NewGuid(), sound.Id, sound.Name, cached.FilePath, "Voicemeeter", output, reader, perSoundFactor);
            _activePlaybacks[instance.Id] = instance;
            var capturedId = instance.Id;
            output.PlaybackStopped += (_, _) =>
            {
                try
                {
                    var disp = System.Windows.Application.Current?.Dispatcher;
                    if (disp is not null && !disp.CheckAccess())
                    {
                        disp.InvokeAsync(() => RemoveInstance(capturedId));
                    }
                    else if (disp is not null)
                    {
                        disp.InvokeAsync(() => RemoveInstance(capturedId));
                    }
                }
                catch
                {
                }
            };
            try
            {
                output.Play();
            }
            catch
            {
                RemoveInstance(instance.Id);
                throw;
            }

            ActivePlaybackChanged?.Invoke(this, new PlaybackStateChangedEventArgs(sound.Id, true));
            StatusMessage = $"Playing {sound.Name} on Voicemeeter ({device.FriendlyName})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Playback failed for {sound.Name}: {ex.Message}";
        }
        await Task.CompletedTask;
        return StatusMessage;
    }

    public Task StopAllAsync()
    {
        foreach (var instance in _activePlaybacks.Values.ToList())
        {
            TryStopInstance(instance);
        }

        StatusMessage = "Stopped all active playback";
        return Task.CompletedTask;
    }

    public Task StopAsync(Guid playbackInstanceId)
    {
        if (_activePlaybacks.TryGetValue(playbackInstanceId, out var instance))
        {
            TryStopInstance(instance);
            StatusMessage = $"Stopped playback: {instance.SoundName}";
        }

        return Task.CompletedTask;
    }

    public void SetVolume(double volume)
    {
        foreach (var instance in _activePlaybacks.Values)
        {
            instance.Reader.Volume = Math.Clamp((float)(Math.Clamp(volume, 0.0, 2.0) * instance.PerSoundFactor), 0.0f, 2.0f);
        }
        StatusMessage = $"Volume set to {volume:P0}";
    }

    public void Dispose()
    {
        foreach (var instance in _activePlaybacks.Values.ToList())
        {
            TryStopInstance(instance);
        }
    }

    private Task StopSameSoundAsync(string soundId)
    {
        foreach (var instance in _activePlaybacks.Values.Where(instance => string.Equals(instance.SoundId, soundId, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            TryStopInstance(instance);
        }

        return Task.CompletedTask;
    }

    private void TryStopInstance(PlaybackInstance instance)
    {
        try
        {
            instance.OutputDevice.Stop();
        }
        catch
        {
        }

        RemoveInstance(instance.Id);
    }

    private void RemoveInstance(Guid id)
    {
        if (_activePlaybacks.TryRemove(id, out var instance))
        {
            _mixer?.RemoveSoundReader(instance.FilePath);
            var soundId = instance.SoundId;
            instance.Dispose();
            ActivePlaybackChanged?.Invoke(this, new PlaybackStateChangedEventArgs(soundId, false));
        }
    }

    private static MMDevice? ResolveVoicemeeterDevice(AppSettings settings)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var deviceId = settings.PreferredVoicemeeterOutputDeviceId;

            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                try
                {
                    var device = enumerator.GetDevice(deviceId);
                    if (device.DataFlow == DataFlow.Render)
                        return device;
                }
                catch
                {
                }
            }

            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            return null;
        }
    }
}
