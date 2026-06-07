# EchoDeck

**Native Windows Soundboard** — built with WPF and .NET 10.

EchoDeck is a fast, focused soundboard for Windows. It provides instant audio playback with optional Voicemeeter integration, per-sound hotkeys, category management, and full dark/light theme support.

## Features

- **Instant Playback** — click to play sounds with low-latency WASAPI output
- **Voicemeeter Integration** — automatic detection, connection, reconnection, and preferred output device selection
- **Per-Sound Hotkeys** — assign F1–F24 + modifier keys to any sound; global Stop All hotkey
- **Categories** — organize sounds into custom categories with rename, reorder, and bulk reassignment
- **Sound Normalization** — optional RMS-based gain normalization for consistent volume
- **Overlap Control** — prevent or allow simultaneous playback of the same sound
- **Bulk Operations** — multi-select mode for batch volume adjustment and deletion
- **Search & Filter** — real-time search by name, category, or hotkey; filter by favorites or category
- **Watched Folders** — monitor folders for new audio files and auto-import them
- **Multi-Language** — English and Arabic interface; extensible XAML resource dictionary system
- **Dark / Light / System Themes** — full theme support with smooth transitions
- **Import** — import individual audio files or entire folders; drag-and-drop support
- **Library Persistence** — JSON-based library and settings store in the user data folder
- **Minimize to Tray** — optionally minimize or close to system tray

## Requirements

- **Windows 10** or **Windows 11**
- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** (to build from source)
- **Optional:** [Voicemeeter](https://vb-audio.com/Voicemeeter/) (Banana, Potato, or Standard) for advanced audio routing

## Quick Start

```powershell
# Restore and build
dotnet restore
dotnet build

# Run
dotnet run --project EchoDeck.App
```

## Build & Publish

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Self-contained publish (x64)
dotnet publish -c Release -r win-x64 --self-contained true -o artifacts\publish
```

## Project Structure

```
EchoDeck.App/
├── Converters/               # XAML value converters
├── Infrastructure/           # JSON file store, path utilities
├── Models/                   # Settings, library items, categories, Voicemeeter models
├── Resources/
│   ├── Languages/
│   │   ├── Arabic.xaml       # Arabic resource dictionary
│   │   └── English.xaml      # English resource dictionary
│   └── ThemeStyles.xaml      # Dark/Light theme styles
├── Services/                 # Audio playback, Voicemeeter, hotkeys, library, etc.
├── ViewModels/               # MVVM view models
├── Views/                    # XAML windows and dialogs
├── App.xaml                  # Application entry point
└── EchoDeck.App.csproj
```

## Configuration

Settings are stored in JSON at:

```
%APPDATA%\EchoDeck\settings.json
```

The library database is stored in the same directory as `library.json`.

## License

MIT — see [LICENSE](LICENSE).
