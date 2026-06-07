using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EchoDeck.App.Infrastructure;
using EchoDeck.App.Models;
using EchoDeck.App.Services;
using EchoDeck.App.ViewModels;
using EchoDeck.App.Views;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace EchoDeck.App;

public partial class App : System.Windows.Application
{
    private const string AppDisplayName = "EchoDeck";
    private const string SingleInstanceMutexName = "Global\\EchoDeck.App.SingleInstance";
    private const string MainWindowTitle = AppDisplayName;
    private IHost? _host;
    private NotifyIcon? _trayIcon;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private bool _isExplicitExit;

    private static string GetCrashLogPath()
    {
        try
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EchoDeck", "Logs");
            Directory.CreateDirectory(appData);
            return Path.Combine(appData, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), $"EchoDeck-crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        }
    }

    private static void WriteCrashDump(string path, string title, Exception exception)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine($"{AppDisplayName} Crash Log");
            sb.AppendLine($"Title: {title}");
            sb.AppendLine($"Time:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS:    {Environment.OSVersion}");
            sb.AppendLine($"CLR:   {Environment.Version}");
            sb.AppendLine($"Process: {Environment.ProcessPath}");
            sb.AppendLine("========================================");
            sb.AppendLine();
            sb.AppendLine($"Exception Type: {exception.GetType().FullName}");
            sb.AppendLine($"Message: {exception.Message}");
            sb.AppendLine($"Source:  {exception.Source}");
            sb.AppendLine($"Stack Trace:");
            sb.AppendLine(exception.ToString());
            sb.AppendLine();

            if (exception is AccessViolationException ave)
            {
                sb.AppendLine("*** ACCESS VIOLATION - likely native code crash ***");
            }

            var inner = exception.InnerException;
            var depth = 0;
            while (inner is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"--- Inner Exception ({depth}) ---");
                sb.AppendLine($"Type:    {inner.GetType().FullName}");
                sb.AppendLine($"Message: {inner.Message}");
                sb.AppendLine($"Stack Trace:");
                sb.AppendLine(inner.ToString());
                inner = inner.InnerException;
                depth++;
            }

            sb.AppendLine();
            sb.AppendLine("========================================");
            sb.AppendLine("Loaded Modules:");
            try
            {
                foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                {
                    try
                    {
                        var ver = FileVersionInfo.GetVersionInfo(module.FileName);
                        sb.AppendLine($"  {module.ModuleName} ({ver.FileVersion ?? "no version"})");
                    }
                    catch
                    {
                        sb.AppendLine($"  {module.ModuleName} (no version info)");
                    }
                }
            }
            catch { sb.AppendLine("  (unable to enumerate modules)"); }

            sb.AppendLine("========================================");
            File.WriteAllText(path, sb.ToString());
        }
        catch
        {
        }
    }

    private void SetupCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var path = GetCrashLogPath();
            WriteCrashDump(path, "AppDomain.UnhandledException", ex ?? new Exception("Unknown AppDomain crash"));
            System.Windows.MessageBox.Show(
                $"{AppDisplayName} encountered a fatal error.\n\n" +
                $"{(ex?.Message ?? "Unknown error")}\n\n" +
                $"A crash log has been saved to:\n{path}",
                $"{AppDisplayName} - Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            var path = GetCrashLogPath();
            WriteCrashDump(path, "DispatcherUnhandledException", args.Exception);
            System.Windows.MessageBox.Show(
                $"{AppDisplayName} encountered an unexpected error.\n\n{args.Exception.Message}\n\n" +
                $"A crash log has been saved to:\n{path}",
                $"{AppDisplayName} - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            try
            {
                var path = GetCrashLogPath();
                WriteCrashDump(path, "TaskScheduler.UnobservedTaskException", args.Exception);
                var logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EchoDeck", "Logs");
                try { Directory.CreateDirectory(logPath); } catch { }
            }
            catch { }
        };

        System.Windows.Application.Current.Dispatcher.UnhandledExceptionFilter += (sender, args) =>
        {
            args.RequestCatch = true;
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        SetupCrashLogging();
        try
        {
            base.OnStartup(e);

            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out _ownsSingleInstanceMutex);
            if (!_ownsSingleInstanceMutex)
            {
                ActivateExistingWindow();
                Shutdown();
                return;
            }

            _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureServices(services =>
                {
                    services.AddSingleton<AppPaths>();
                    services.AddSingleton<LoggingService>();
                    services.AddSingleton<SettingsService>();
                    services.AddSingleton<AudioMetadataCacheService>();
                    services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
                    services.AddSingleton<AudioMixerService>();
                    services.AddSingleton<IVoicemeeterService, VoicemeeterService>();
                    services.AddSingleton<FolderWatcherService>();
                    services.AddSingleton<WatchedFoldersService>();
                    services.AddSingleton<LibraryService>();
                    services.AddSingleton<CategoryService>();
                    services.AddSingleton<HotkeyService>();
                    services.AddSingleton<NormalizationService>();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddTransient<SettingsWindow>();
                })
                .Build();

            await _host.StartAsync();

            var paths = _host.Services.GetRequiredService<AppPaths>();
            paths.EnsureCreated();

            var settingsService = _host.Services.GetRequiredService<SettingsService>();
            await settingsService.LoadAsync();
            var voicemeeterService = _host.Services.GetRequiredService<IVoicemeeterService>();
            var voicemeeterResult = await voicemeeterService.DetectAsync();

            // Fix Voicemeeter auto-routing: Auto-set PreferredVoicemeeterOutputDeviceId if not set
            if (voicemeeterResult.IsDetected)
            {
                if (string.IsNullOrWhiteSpace(settingsService.Current.PreferredVoicemeeterOutputDeviceId))
                {
                    var preferred = voicemeeterService.GetPreferredOutput(settingsService.Current);
                    if (preferred is not null)
                    {
                        settingsService.Current.PreferredVoicemeeterOutputDeviceId = preferred.Id;
                        await settingsService.SaveAsync();
                    }
                }
            }

            if (settingsService.IsFirstRun)
            {
                var firstRunDialog = new Views.FirstRunDialog(settingsService, voicemeeterService);
                firstRunDialog.Owner = null;
                firstRunDialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;

                if (firstRunDialog.ShowDialog() == true)
                {
                    await settingsService.SaveAsync();
                    voicemeeterResult = await voicemeeterService.DetectAsync();
                }
            }

            var watchedFoldersService = _host.Services.GetRequiredService<WatchedFoldersService>();
            await watchedFoldersService.LoadAsync();
            var libraryService = _host.Services.GetRequiredService<LibraryService>();
            await libraryService.LoadAsync();
            var categoryService = _host.Services.GetRequiredService<CategoryService>();
            await categoryService.LoadAsync();

            // Auto-add project sounds folder — walk up from EXE to find project root
            var projectSoundsFolder = ResolveProjectSoundsFolder();
            if (!Directory.Exists(projectSoundsFolder))
                Directory.CreateDirectory(projectSoundsFolder);
            if (!watchedFoldersService.WatchedFolders.Any(w => string.Equals(w, projectSoundsFolder, StringComparison.OrdinalIgnoreCase)))
            {
                var added = watchedFoldersService.AddWatchedFolder(projectSoundsFolder, out _);
                if (added)
                {
                    var existingFiles = Directory.EnumerateFiles(projectSoundsFolder, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsSupportedAudioFile(f))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (existingFiles.Count > 0)
                    {
                        var libSvc = _host.Services.GetRequiredService<LibraryService>();
                        var defaultCat = string.IsNullOrWhiteSpace(settingsService.Current.DefaultCategoryId)
                            ? "Uncategorized"
                            : settingsService.Current.DefaultCategoryId;
                        var result = libSvc.ImportPaths(existingFiles, defaultCat);
                        if (result.ImportedCount > 0)
                            await libSvc.SaveAsync();
                    }
                    await watchedFoldersService.SaveAsync();
                }
            }

            // Auto-add default download folder if it exists
            var defaultWatchFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads", "sound");
            if (Directory.Exists(defaultWatchFolder))
            {
                var addedDw = watchedFoldersService.AddWatchedFolder(defaultWatchFolder, out _);
                if (addedDw)
                {
                    var existingFiles = Directory.EnumerateFiles(defaultWatchFolder, "*.*", SearchOption.AllDirectories)
                        .Where(f => IsSupportedAudioFile(f))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (existingFiles.Count > 0)
                    {
                        var libSvc = _host.Services.GetRequiredService<LibraryService>();
                        var defaultCat = string.IsNullOrWhiteSpace(settingsService.Current.DefaultCategoryId)
                            ? "Uncategorized"
                            : settingsService.Current.DefaultCategoryId;
                        var result = libSvc.ImportPaths(existingFiles, defaultCat);
                        if (result.ImportedCount > 0)
                            await libSvc.SaveAsync();
                    }
                    await watchedFoldersService.SaveAsync();
                }
            }

            var folderWatcherService = _host.Services.GetRequiredService<FolderWatcherService>();
            await folderWatcherService.InitializeAsync();
            await watchedFoldersService.InitializeAsync();

            var loggingService = _host.Services.GetRequiredService<LoggingService>();
            await loggingService.LogInformation($"{AppDisplayName} started.");

            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            var hotkeyService = _host.Services.GetRequiredService<HotkeyService>();
            var playbackService = _host.Services.GetRequiredService<IAudioPlaybackService>();
            ApplyTheme(settingsService.Current.Theme);
            ApplyLanguage(settingsService.Current.Language);

            await loggingService.LogInformation("EchoDeck started.");

            var warnings = new[]
            {
                settingsService.LastLoadWarning,
                libraryService.LastLoadWarning,
                voicemeeterResult.Message,
                folderWatcherService.StatusMessage,
                watchedFoldersService.StatusMessage
            }.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray();

            viewModel.StatusText = warnings.Length > 0
                ? string.Join(" | ", warnings)
                : "Ready";

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.StateChanged += OnMainWindowStateChanged;
            window.Closing += OnMainWindowClosing;
            window.SourceInitialized += (_, _) =>
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
                if (source is not null)
                {
                    hotkeyService.Attach(source);
                    hotkeyService.HotkeyPressed += (_, soundId) =>
                    {
                        try
                        {
                            _ = Dispatcher.InvokeAsync(async () =>
                            {
                                try
                                {
                                    if (string.Equals(soundId, "Stop All", StringComparison.OrdinalIgnoreCase))
                                    {
                                        await playbackService.StopAllAsync();
                                        viewModel.StatusText = playbackService.StatusMessage;
                                        return;
                                    }

                                    var sound = viewModel.Sounds.FirstOrDefault(s =>
                                        string.Equals(s.Id, soundId, StringComparison.OrdinalIgnoreCase));
                                    if (sound is null)
                                    {
                                        viewModel.StatusText = $"Hotkey for unknown sound: {soundId}";
                                        return;
                                    }

                                    viewModel.SelectedSound = sound;
                                    await viewModel.PlaySelectedSoundAsync();
                                }
                                catch (Exception ex)
                                {
                                    try
                                    {
                                        await loggingService.LogError(ex, "Hotkey playback failed.");
                                    }
                                    catch
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Hotkey error (logging failed): {ex.Message}");
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Hotkey dispatch failed: {ex.Message}");
                        }
                    };

                    var stopAllGesture = HotkeyGesture.Parse(settingsService.Current.StopAllHotkey);
                    if (string.IsNullOrWhiteSpace(stopAllGesture.Key))
                    {
                        stopAllGesture = new HotkeyGesture { Key = "F12" };
                    }
                    hotkeyService.Register(handle, stopAllGesture, "Stop All");

                    // Load per-sound hotkeys from settings
                    foreach (var kvp in settingsService.Current.PerSoundHotkeys)
                    {
                        var soundId = kvp.Key;
                        var gesture = HotkeyGesture.Parse(kvp.Value);
                        var sound = viewModel.Sounds.FirstOrDefault(s =>
                            string.Equals(s.Id, soundId, StringComparison.OrdinalIgnoreCase));

                        if (sound is not null)
                        {
                            if (hotkeyService.Register(handle, gesture, soundId))
                            {
                                sound.Hotkey = kvp.Value;
                            }
                            else
                            {
                                viewModel.StatusText = $"Failed to register hotkey {kvp.Value} for {sound.Name}";
                            }
                        }
                    }

                    // Also register any hotkeys from library items not yet in PerSoundHotkeys
                    foreach (var sound in viewModel.Sounds)
                    {
                        if (string.IsNullOrWhiteSpace(sound.Hotkey)) continue;
                        if (settingsService.Current.PerSoundHotkeys.ContainsKey(sound.Id)) continue;

                        var gesture = HotkeyGesture.Parse(sound.Hotkey);
                        if (hotkeyService.Register(handle, gesture, sound.Id))
                        {
                            settingsService.Current.PerSoundHotkeys[sound.Id] = sound.Hotkey;
                        }
                    }

                    var missingHotkeys = hotkeyService.IsRegistered("Stop All")
                        ? Array.Empty<string>()
                        : new[] { "Stop All" };
                    if (missingHotkeys.Length > 0)
                    {
                        viewModel.StatusText = $"Hotkey registration warning: {string.Join(", ", missingHotkeys)}";
                    }
                }
            };

            _trayIcon = new NotifyIcon
            {
                Text = AppDisplayName,
                Icon = LoadTrayIcon(),
                Visible = true,
                ContextMenuStrip = BuildTrayMenu()
            };
            _trayIcon.DoubleClick += (_, _) => RestoreMainWindow();

            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            try
            {
                var log = _host?.Services.GetRequiredService<LoggingService>();
                if (log is not null)
                    await log.LogError(ex, "Startup failed");
            }
            catch
            {
            }

            System.Windows.MessageBox.Show($"Startup error: {ex.Message}", AppDisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add($"Open {AppDisplayName}", null, (_, _) => RestoreMainWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Stop All Sounds", null, async (_, _) =>
        {
            if (_host is null) return;
            var playback = _host.Services.GetRequiredService<IAudioPlaybackService>();
            await playback.StopAllAsync();
            if (MainWindow?.DataContext is MainViewModel vm)
                vm.StatusText = playback.StatusMessage;
        });
        menu.Items.Add("Settings", null, (_, _) =>
        {
            if (_host is null) return;
            RestoreMainWindow();
            var settingsWindow = _host.Services.GetRequiredService<SettingsWindow>();
            settingsWindow.Owner = MainWindow as Window;
            settingsWindow.ShowDialog();
        });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => { _isExplicitExit = true; Shutdown(); });
        return menu;
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application;
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        var settings = _host?.Services.GetRequiredService<SettingsService>();
        if (MainWindow?.WindowState == WindowState.Minimized && settings?.Current.MinimizeToTray == true)
        {
            MainWindow.Hide();
        }
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isExplicitExit)
            return;

        if (e.Cancel) return;

        var settings = _host?.Services.GetRequiredService<SettingsService>();
        if (settings?.Current.CloseButtonBehavior == "Exit")
            return;

        if (settings?.Current.MinimizeToTray == true ||
            string.Equals(settings?.Current.CloseButtonBehavior, "MinimizeToTray", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            MainWindow?.Hide();
        }
    }

    private void RestoreMainWindow()
    {
        if (MainWindow is null) return;
        try
        {
            if (!MainWindow.IsVisible)
            {
                MainWindow.Show();
            }

            MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
        }
        catch (InvalidOperationException)
        {
            // Window is closing; ignore.
        }
    }

    public T? GetService<T>() where T : class => _host?.Services.GetService<T>();

    public void ApplyLanguage(string language)
    {
        if (_host is null) return;
        var settingsService = _host.Services.GetRequiredService<SettingsService>();
        var path = GetLanguageDictionaryPath(language);
        var resources = Resources.MergedDictionaries;

        var existing = resources.FirstOrDefault(d =>
        {
            try { return d.Source != null && d.Source.OriginalString.Contains("Languages/", StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        });

        if (existing is not null)
            resources.Remove(existing);

        try
        {
            var dict = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
            resources.Add(dict);
        }
        catch
        {
            if (existing is not null)
                resources.Add(existing);
        }

        Resources["AppFlowDirection"] = System.Windows.FlowDirection.LeftToRight;
    }

    private static string GetLanguageDictionaryPath(string language)
    {
        return string.Equals(language, "Arabic", StringComparison.OrdinalIgnoreCase)
            ? "Resources/Languages/Arabic.xaml"
            : "Resources/Languages/English.xaml";
    }

    public void ApplyTheme(string theme)
    {
        var resolved = string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase)
            ? GetSystemTheme()
            : theme;
        var isLight = string.Equals(resolved, "Light", StringComparison.OrdinalIgnoreCase);

        Resources["AppWindowBackgroundBrush"] = FreezeBrush(new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLight ? "#FFF6F7F8" : "#11161C")!));
        Resources["AppPanelBackgroundBrush"] = FreezeBrush(new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLight ? "#FFFFFFFF" : "#171D25")!));
        Resources["AppCardBackgroundBrush"] = FreezeBrush(new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLight ? "#FFECEFEE" : "#202832")!));
        Resources["AppSurfaceBackgroundBrush"] = FreezeBrush(new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLight ? "#FFF9FAFA" : "#1B222B")!));
        Resources["AppTextBrush"] = FreezeBrush(new SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isLight ? "#FF14181C" : "#F5F8FA")!));
    }

    private static SolidColorBrush FreezeBrush(SolidColorBrush brush)
    {
        if (brush.CanFreeze) brush.Freeze();
        return brush;
    }

    private static string GetSystemTheme()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
                return value == 1 ? "Light" : "Dark";
        }
        catch { }
        return "Dark";
    }

    private static void ActivateExistingWindow()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var existing = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName)
            .FirstOrDefault(p => p.Id != currentProcess.Id && p.MainWindowHandle != IntPtr.Zero);
        if (existing is null) return;

        var hwnd = existing.MainWindowHandle;
        if (IsIconic(hwnd))
            ShowWindowAsync(hwnd, RestoreCommand);
        else
            ShowWindowAsync(hwnd, ShowNormalCommand);

        SetForegroundWindow(hwnd);
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

    private static string ResolveProjectSoundsFolder()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(dir, "EchoDeck.slnx")))
                return Path.Combine(dir, "sounds");
            var parent = Path.GetDirectoryName(dir);
            if (parent is null) break;
            dir = parent;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sounds");
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int RestoreCommand = 9;
    private const int ShowNormalCommand = 1;

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        if (_host is not null)
        {
            var hotkeyService = _host.Services.GetRequiredService<HotkeyService>();
            if (MainWindow is not null)
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(MainWindow).Handle;
                hotkeyService.UnregisterAll(handle);
            }
            hotkeyService.Dispose();
            var settingsService = _host.Services.GetRequiredService<SettingsService>();
            var libraryService = _host.Services.GetRequiredService<LibraryService>();
            await settingsService.SaveAsync();
            await libraryService.SaveAsync();
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();

        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
