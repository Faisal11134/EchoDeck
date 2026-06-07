using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using EchoDeck.App.Models;
using EchoDeck.App.Services;

namespace EchoDeck.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public string[] ThemeOptions => ["Dark", "Light", "System"];
    public string[] StartupViewOptions => ["Library", "Favorites", "Categories"];
    public string[] CloseButtonBehaviorOptions => ["MinimizeToTray", "Exit"];
    public string[] LanguageOptions => ["English", "Arabic"];
    public string[] HotkeyConflictBehaviorOptions => ["Warn", "Override", "Disable duplicate"];

    private readonly SettingsService _settingsService;
    private readonly IVoicemeeterService _voicemeeterService;
    private readonly AudioMixerService _audioMixerService;
    private readonly LoggingService _loggingService;
    private readonly WatchedFoldersService _watchedFoldersService;
    private readonly LibraryService _libraryService;
    private string _theme = "Dark";
    private string? _selectedInputDeviceId;
    private bool _enableNormalization = true;
    private bool _allowOverlap = true;
    private string _stopAllHotkey = "F12";
    private string _startupView = "Library";
    private bool _minimizeToTray = true;
    private string _closeButtonBehavior = "MinimizeToTray";
    private string _language = "Arabic";
    private string _hotkeyConflictBehavior = "Warn";
    private string? _preferredVoicemeeterOutputDeviceId;
    private bool _virtualMicEnabled;
    private double _virtualMicVolume = 0.85;
    private double _monitorVolume = 1.0;
    private double _outputVolume = 1.0;
    private bool _autoReconnectVoicemeeter = true;
    private string _defaultCategoryId = "Uncategorized";
    private string _hotkeyStatus = "Stop All hotkey is ready.";
    private string _watchedFoldersStatus = "Watched folders deferred";
    private string _watchedFolderPath = string.Empty;
    private string? _selectedWatchedFolder;
    private bool _suppressWatchedFolderSelectionSync;
    private string? _selectedPerSoundHotkeyItem;

    public SettingsViewModel(SettingsService settingsService, IVoicemeeterService voicemeeterService, AudioMixerService audioMixerService, LoggingService loggingService, WatchedFoldersService watchedFoldersService, LibraryService libraryService)
    {
        _settingsService = settingsService;
        _voicemeeterService = voicemeeterService;
        _audioMixerService = audioMixerService;
        _loggingService = loggingService;
        _watchedFoldersService = watchedFoldersService;
        _libraryService = libraryService;
        _watchedFoldersService.WatchedFolders.CollectionChanged += WatchedFolders_CollectionChanged;
        LoadFromSettings();
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool VirtualMicEnabled
    {
        get => _virtualMicEnabled;
        set
        {
            if (SetProperty(ref _virtualMicEnabled, value))
            {
                NotifyPropertyChanged(nameof(VirtualMicCableInstalled));
                NotifyPropertyChanged(nameof(VirtualMicStatusText));
                NotifyPropertyChanged(nameof(VirtualMicActiveColor));
                if (value)
                {
                    _audioMixerService.DetectVirtualCable();
                    _audioMixerService.Start(SelectedInputDeviceId ?? string.Empty, string.Empty);
                }
                else
                {
                    _audioMixerService.Stop();
                }
                NotifyPropertyChanged(nameof(VirtualMicIsRunning));
                NotifyPropertyChanged(nameof(VirtualMicStatusText));
                NotifyPropertyChanged(nameof(VirtualMicActiveColor));
            }
        }
    }

    public double VirtualMicVolume
    {
        get => _virtualMicVolume;
        set
        {
            if (SetProperty(ref _virtualMicVolume, value))
            {
                _audioMixerService.SetSoundVolume(value);
            }
        }
    }

    public bool VirtualMicCableInstalled => _audioMixerService.HasVirtualCable;
    public bool VirtualMicIsRunning => _audioMixerService.IsRunning;
    public string VirtualMicStatusText => _audioMixerService.StatusMessage;
    public string VirtualMicActiveColor => VirtualMicIsRunning ? "#4CAF50" : "#888";

    public double MonitorVolume
    {
        get => _monitorVolume;
        set
        {
            if (SetProperty(ref _monitorVolume, value))
            {
                _voicemeeterService.SetMonitorVolume(value);
            }
        }
    }

    public double OutputVolume
    {
        get => _outputVolume;
        set
        {
            if (SetProperty(ref _outputVolume, value))
            {
                _voicemeeterService.SetOutputVolume(value);
            }
        }
    }

    public IReadOnlyList<AudioDeviceInfo> InputDevicesList => _audioMixerService.GetInputDevices();

    public IReadOnlyList<AudioDeviceInfo> VoicemeeterOutputDevices => _voicemeeterService.AvailableVoicemeeterOutputs;

    public string? SelectedInputDeviceId
    {
        get => _selectedInputDeviceId;
        set => SetProperty(ref _selectedInputDeviceId, value);
    }

    public bool EnableNormalization
    {
        get => _enableNormalization;
        set => SetProperty(ref _enableNormalization, value);
    }

    public bool AllowOverlap
    {
        get => _allowOverlap;
        set => SetProperty(ref _allowOverlap, value);
    }

    public string StopAllHotkey
    {
        get => _stopAllHotkey;
        set
        {
            if (SetProperty(ref _stopAllHotkey, value))
            {
                ValidateHotkeyConfiguration();
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public string CloseButtonBehavior
    {
        get => _closeButtonBehavior;
        set => SetProperty(ref _closeButtonBehavior, value);
    }

    public string Language
    {
        get => _language;
        set => SetProperty(ref _language, value);
    }

    public string HotkeyConflictBehavior
    {
        get => _hotkeyConflictBehavior;
        set => SetProperty(ref _hotkeyConflictBehavior, value);
    }

    public ObservableCollection<string> PerSoundHotkeyItems { get; } = new();

    public string? SelectedPerSoundHotkeyItem
    {
        get => _selectedPerSoundHotkeyItem;
        set
        {
            if (SetProperty(ref _selectedPerSoundHotkeyItem, value))
            {
                NotifyPropertyChanged(nameof(CanRemovePerSoundHotkey));
            }
        }
    }

    public bool CanRemovePerSoundHotkey => !string.IsNullOrWhiteSpace(SelectedPerSoundHotkeyItem);
    public bool CanClearPerSoundHotkeys => PerSoundHotkeyItems.Count > 0;
    public string PerSoundHotkeysStatus => $"{PerSoundHotkeyItems.Count} hotkey(s) assigned";

    public void RefreshPerSoundHotkeys()
    {
        PerSoundHotkeyItems.Clear();
        foreach (var kvp in _settingsService.Current.PerSoundHotkeys)
        {
            var soundId = kvp.Key;
            var hotkey = kvp.Value;
            var sound = _libraryService.Sounds.FirstOrDefault(s => string.Equals(s.Id, soundId, StringComparison.OrdinalIgnoreCase));
            var name = sound?.Name ?? soundId[..Math.Min(8, soundId.Length)];
            PerSoundHotkeyItems.Add($"{name}  →  {hotkey}");
        }
        NotifyPropertyChanged(nameof(PerSoundHotkeysStatus));
        NotifyPropertyChanged(nameof(CanClearPerSoundHotkeys));
    }

    public void RemoveSelectedPerSoundHotkey()
    {
        if (string.IsNullOrWhiteSpace(SelectedPerSoundHotkeyItem)) return;
        var parts = SelectedPerSoundHotkeyItem.Split("→", StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return;
        var hotkey = parts[^1];

        var toRemove = _settingsService.Current.PerSoundHotkeys
            .Where(kvp => string.Equals(kvp.Value, hotkey, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _settingsService.Current.PerSoundHotkeys.Remove(id);
        }

        RefreshPerSoundHotkeys();
    }

    public void ClearAllPerSoundHotkeys()
    {
        _settingsService.Current.PerSoundHotkeys.Clear();
        RefreshPerSoundHotkeys();
    }

    public string? PreferredVoicemeeterOutputDeviceId
    {
        get => _preferredVoicemeeterOutputDeviceId;
        set => SetProperty(ref _preferredVoicemeeterOutputDeviceId, value);
    }

    public bool AutoReconnectVoicemeeter
    {
        get => _autoReconnectVoicemeeter;
        set => SetProperty(ref _autoReconnectVoicemeeter, value);
    }

    public string DefaultCategoryId
    {
        get => _defaultCategoryId;
        set => SetProperty(ref _defaultCategoryId, value);
    }

    public string HotkeyStatus
    {
        get => _hotkeyStatus;
        set => SetProperty(ref _hotkeyStatus, value);
    }

    public string WatchedFoldersStatus
    {
        get => _watchedFoldersStatus;
        set => SetProperty(ref _watchedFoldersStatus, value);
    }

    public int WatchedFoldersCount => _watchedFoldersService.WatchedFolders.Count;
    public bool CanRemoveWatchedFolder => !string.IsNullOrWhiteSpace(SelectedWatchedFolder);
    public bool CanClearWatchedFolders => WatchedFoldersCount > 0;

    public ObservableCollection<string> WatchedFolders => _watchedFoldersService.WatchedFolders;

    public string WatchedFolderPath
    {
        get => _watchedFolderPath;
        set
        {
            if (SetProperty(ref _watchedFolderPath, value))
            {
                NotifyPropertyChanged(nameof(CanAddWatchedFolder));
            }
        }
    }

    public bool CanAddWatchedFolder => !string.IsNullOrWhiteSpace(WatchedFolderPath);

    public string? SelectedWatchedFolder
    {
        get => _selectedWatchedFolder;
        set
        {
            if (SetProperty(ref _selectedWatchedFolder, value))
            {
                _settingsService.Current.SelectedWatchedFolder = value;
                NotifyPropertyChanged(nameof(CanRemoveWatchedFolder));
                if (string.IsNullOrWhiteSpace(value))
                {
                    WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
                }
                else
                {
                    WatchedFoldersStatus = $"Selected watched folder: {value}";
                }
            }
        }
    }

    public string StartupView
    {
        get => _startupView;
        set => SetProperty(ref _startupView, value);
    }

    public async Task SaveAsync()
    {
        _settingsService.Current.Theme = Theme;
        _settingsService.Current.MasterVolume = 0.85;
        _settingsService.Current.EnableNormalization = EnableNormalization;
        _settingsService.Current.AllowOverlap = AllowOverlap;
        _settingsService.Current.StopAllHotkey = StopAllHotkey;
        _settingsService.Current.StartupView = StartupView;
        _settingsService.Current.MinimizeToTray = MinimizeToTray;
        _settingsService.Current.CloseButtonBehavior = CloseButtonBehavior;
        _settingsService.Current.Language = Language;
        _settingsService.Current.HotkeyConflictBehavior = HotkeyConflictBehavior;
        _settingsService.Current.PreferredVoicemeeterOutputDeviceId = PreferredVoicemeeterOutputDeviceId;
        _settingsService.Current.AutoReconnectVoicemeeter = AutoReconnectVoicemeeter;
        _settingsService.Current.DefaultCategoryId = DefaultCategoryId;
        _settingsService.Current.SelectedWatchedFolder = SelectedWatchedFolder;
        _settingsService.Current.VirtualMicEnabled = VirtualMicEnabled;
        _settingsService.Current.VirtualMicVolume = VirtualMicVolume;
        _settingsService.Current.MonitorVolume = MonitorVolume;
        _settingsService.Current.OutputVolume = OutputVolume;
        _settingsService.Current.SelectedInputDeviceId = SelectedInputDeviceId;
        ValidateHotkeyConfiguration();
        await _settingsService.SaveAsync();
    }

    public void RefreshDeviceLists()
    {
        NotifyPropertyChanged(nameof(InputDevicesList));
        NotifyPropertyChanged(nameof(VoicemeeterOutputDevices));
        NotifyPropertyChanged(nameof(VirtualMicCableInstalled));
        _audioMixerService.DetectVirtualCable();
    }

    public async Task AddWatchedFolderAsync()
    {
        var candidate = WatchedFolderPath.Trim();
        if (!_watchedFoldersService.AddWatchedFolder(candidate, out var storedPath))
        {
            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            return;
        }

        _suppressWatchedFolderSelectionSync = true;
        try
        {
            await _watchedFoldersService.SaveAsync();

            // Import existing audio files in the added folder
            if (storedPath is not null && Directory.Exists(storedPath))
            {
                var existingFiles = Directory.EnumerateFiles(storedPath, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupportedAudioFile)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (existingFiles.Count > 0)
                {
                    var result = _libraryService.ImportPaths(existingFiles, _settingsService.Current.DefaultCategoryId);
                    if (result.ImportedCount > 0)
                        await _libraryService.SaveAsync();
                }
            }

            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            SelectedWatchedFolder = storedPath;
            WatchedFolderPath = string.Empty;
            NotifyPropertyChanged(nameof(WatchedFoldersCount));
            NotifyPropertyChanged(nameof(CanClearWatchedFolders));
        }
        finally
        {
            _suppressWatchedFolderSelectionSync = false;
        }
    }

    public async Task RemoveWatchedFolderAsync()
    {
        var path = SelectedWatchedFolder;
        if (string.IsNullOrWhiteSpace(path))
        {
            WatchedFoldersStatus = "Select a watched folder to remove.";
            return;
        }

        var deletedIndex = WatchedFolders.IndexOf(path);
        if (!_watchedFoldersService.RemoveWatchedFolder(path))
        {
            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            return;
        }

        _suppressWatchedFolderSelectionSync = true;
        try
        {
            await _watchedFoldersService.SaveAsync();
            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            if (WatchedFolders.Count == 0)
            {
                SelectedWatchedFolder = null;
            }
            else
            {
                var fallbackIndex = deletedIndex >= 0
                    ? Math.Min(deletedIndex, WatchedFolders.Count - 1)
                    : 0;

                SelectedWatchedFolder = WatchedFolders[fallbackIndex];
            }

            NotifyPropertyChanged(nameof(WatchedFoldersCount));
            NotifyPropertyChanged(nameof(CanClearWatchedFolders));
        }
        finally
        {
            _suppressWatchedFolderSelectionSync = false;
        }
    }

    public async Task ScanWatchedFoldersAsync()
    {
        var folders = WatchedFolders.Where(Directory.Exists).ToList();
        if (folders.Count == 0)
        {
            WatchedFoldersStatus = "No watched folders to scan.";
            return;
        }

        var allFiles = folders.SelectMany(Directory.EnumerateFiles)
            .Where(f => IsSupportedAudioFile(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = _libraryService.ImportPaths(allFiles, _settingsService.Current.DefaultCategoryId);
        if (result.ImportedCount > 0)
        {
            await _libraryService.SaveAsync();
            WatchedFoldersStatus = $"Scanned {allFiles.Count} file(s), imported {result.ImportedCount} new sound(s).";
        }
        else
        {
            WatchedFoldersStatus = $"Scanned {allFiles.Count} file(s), nothing new to import.";
        }
    }

    private static bool IsSupportedAudioFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".wma", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aac", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ClearWatchedFoldersAsync()
    {
        if (WatchedFolders.Count == 0)
        {
            WatchedFoldersStatus = "Watched folders are already empty.";
            return;
        }

        _suppressWatchedFolderSelectionSync = true;
        try
        {
            _watchedFoldersService.ClearWatchedFolders();
            await _watchedFoldersService.SaveAsync();
            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            SelectedWatchedFolder = null;
            NotifyPropertyChanged(nameof(WatchedFoldersCount));
            NotifyPropertyChanged(nameof(CanClearWatchedFolders));
        }
        finally
        {
            _suppressWatchedFolderSelectionSync = false;
        }
    }

    public void OpenSelectedWatchedFolder()
    {
        var path = SelectedWatchedFolder;
        if (string.IsNullOrWhiteSpace(path))
        {
            WatchedFoldersStatus = "Select a watched folder to open.";
            return;
        }

        if (!Directory.Exists(path))
        {
            WatchedFoldersStatus = "The selected watched folder no longer exists.";
            return;
        }

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(startInfo);
            WatchedFoldersStatus = $"Opened watched folder: {path}";
        }
        catch
        {
            WatchedFoldersStatus = "Unable to open the selected watched folder.";
        }
    }

    public void CopySelectedWatchedFolderPath()
    {
        var path = SelectedWatchedFolder;
        if (string.IsNullOrWhiteSpace(path))
        {
            WatchedFoldersStatus = "Select a watched folder to copy.";
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(path);
            WatchedFoldersStatus = $"Copied watched folder path: {path}";
        }
        catch
        {
            WatchedFoldersStatus = "Unable to copy the selected watched folder path.";
        }
    }

    public void OpenLogsFolder()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = _loggingService.LogFolderPath,
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(startInfo);
            WatchedFoldersStatus = $"Opened logs folder: {_loggingService.LogFolderPath}";
        }
        catch
        {
            WatchedFoldersStatus = "Unable to open logs folder.";
        }
    }

    public async Task ResetSettingsAsync()
    {
        await _settingsService.ResetAsync();
        LoadFromSettings();
        RefreshDeviceLists();
        ValidateHotkeyConfiguration();
        WatchedFoldersStatus = "Settings reset to defaults.";
    }

    private void LoadFromSettings()
    {
        AppSettings current = _settingsService.Current;
        Theme = current.Theme;
        EnableNormalization = current.EnableNormalization;
        AllowOverlap = current.AllowOverlap;
        StopAllHotkey = current.StopAllHotkey;
        StartupView = current.StartupView;
        MinimizeToTray = current.MinimizeToTray;
        CloseButtonBehavior = current.CloseButtonBehavior;
        Language = current.Language;
        HotkeyConflictBehavior = current.HotkeyConflictBehavior;
        RefreshPerSoundHotkeys();
        PreferredVoicemeeterOutputDeviceId = current.PreferredVoicemeeterOutputDeviceId;
        AutoReconnectVoicemeeter = current.AutoReconnectVoicemeeter;
        DefaultCategoryId = current.DefaultCategoryId;
        WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
        SelectedWatchedFolder = ResolveSelectedWatchedFolder(current.SelectedWatchedFolder);
        _virtualMicEnabled = current.VirtualMicEnabled;
        _selectedInputDeviceId = current.SelectedInputDeviceId;
        _virtualMicVolume = current.VirtualMicVolume;
        _monitorVolume = current.MonitorVolume;
        _outputVolume = current.OutputVolume;
        _audioMixerService.DetectVirtualCable();
        ValidateHotkeyConfiguration();
    }

    private void ValidateHotkeyConfiguration()
    {
        var gesture = HotkeyGesture.Parse(StopAllHotkey);

        if (string.IsNullOrWhiteSpace(gesture.Key))
        {
            HotkeyStatus = "Stop All hotkey is invalid. Keeping the previous valid hotkey.";
            return;
        }

        if (!Enum.TryParse(gesture.Key, ignoreCase: true, out System.Windows.Input.Key _))
        {
            HotkeyStatus = "Stop All hotkey is invalid. Keeping the previous valid hotkey.";
            return;
        }

        var conflictingSound = _settingsService.Current.PerSoundHotkeys
            .FirstOrDefault(kvp => string.Equals(kvp.Value, StopAllHotkey, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(conflictingSound.Key))
        {
            HotkeyStatus = "Stop All hotkey conflicts with a sound hotkey. Choose another key.";
            return;
        }

        HotkeyStatus = string.Equals(StopAllHotkey, "F12", StringComparison.OrdinalIgnoreCase)
            ? "Stop All hotkey is ready."
            : $"Stop All hotkey set to {StopAllHotkey}.";
    }

    private string? ResolveSelectedWatchedFolder(string? persistedSelection)
    {
        if (!string.IsNullOrWhiteSpace(persistedSelection))
        {
            var normalized = TryNormalizeSelectedWatchedFolder(persistedSelection);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                var match = WatchedFolders.FirstOrDefault(folder => string.Equals(folder, normalized, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }
        }

        return WatchedFolders.FirstOrDefault();
    }

    private static string? TryNormalizeSelectedWatchedFolder(string path)
    {
        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        }
        catch
        {
            return path.Trim();
        }
    }

    private void WatchedFolders_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var shouldPreserveSelection = _suppressWatchedFolderSelectionSync;
        if (_suppressWatchedFolderSelectionSync)
        {
            NotifyPropertyChanged(nameof(WatchedFoldersCount));
            NotifyPropertyChanged(nameof(CanClearWatchedFolders));
            return;
        }

        NotifyPropertyChanged(nameof(WatchedFoldersCount));
        NotifyPropertyChanged(nameof(CanClearWatchedFolders));

        if (WatchedFolders.Count == 0)
        {
            SelectedWatchedFolder = null;
            WatchedFoldersStatus = _watchedFoldersService.StatusMessage;
            return;
        }

        if (!shouldPreserveSelection && (SelectedWatchedFolder is null || !WatchedFolders.Any(folder => string.Equals(folder, SelectedWatchedFolder, StringComparison.OrdinalIgnoreCase))))
        {
            SelectedWatchedFolder = WatchedFolders.FirstOrDefault();
        }
    }
}
