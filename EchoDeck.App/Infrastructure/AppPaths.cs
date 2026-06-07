using System.IO;

namespace EchoDeck.App.Infrastructure;

public sealed class AppPaths
{
    public string RootFolder { get; }
    public string DataFolder { get; }
    public string LogsFolder { get; }
    public string BackupsFolder { get; }
    public string SettingsFilePath { get; }
    public string LibraryFilePath { get; }
    public string WatchedFoldersFilePath { get; }

    public AppPaths(string? testRoot = null)
    {
        RootFolder = testRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EchoDeck");
        DataFolder = Path.Combine(RootFolder, "Data");
        LogsFolder = Path.Combine(RootFolder, "Logs");
        BackupsFolder = Path.Combine(RootFolder, "Backups");
        SettingsFilePath = Path.Combine(DataFolder, "settings.json");
        LibraryFilePath = Path.Combine(DataFolder, "library.json");
        WatchedFoldersFilePath = Path.Combine(DataFolder, "watched-folders.json");
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootFolder);
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(BackupsFolder);
    }
}
