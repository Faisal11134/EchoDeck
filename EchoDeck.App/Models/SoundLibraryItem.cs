namespace EchoDeck.App.Models;

public sealed class SoundLibraryItem
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Category { get; set; } = "Uncategorized";
    public string Hotkey { get; set; } = string.Empty;
    public string Duration { get; set; } = "0:00";
    public double Volume { get; set; } = 1.0;
    public bool IsFavorite { get; set; }
    public bool IsMissingFile { get; set; }
    public bool Normalized { get; set; }
    public double NormalizationGain { get; set; } = 1.0;
    public int PlayCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastPlayedAt { get; set; }
}
