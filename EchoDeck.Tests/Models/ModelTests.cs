using EchoDeck.App.Models;

namespace EchoDeck.Tests.Models;

public sealed class AppSettingsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var s = new AppSettings();

        Assert.Equal(1, s.SchemaVersion);
        Assert.Equal("Dark", s.Theme);
        Assert.Equal(0.85, s.MasterVolume);
        Assert.True(s.EnableNormalization);
        Assert.False(s.AllowOverlap);
        Assert.Equal("F12", s.StopAllHotkey);
        Assert.Equal("Library", s.StartupView);
        Assert.True(s.MinimizeToTray);
        Assert.Equal("MinimizeToTray", s.CloseButtonBehavior);
        Assert.True(s.AutoReconnectVoicemeeter);
        Assert.Equal("Uncategorized", s.DefaultCategoryId);
        Assert.Null(s.PreferredVoicemeeterOutputDeviceId);
        Assert.Null(s.SelectedWatchedFolder);
    }
}

public sealed class SoundLibraryItemTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var item = new SoundLibraryItem();

        Assert.Equal(1, item.SchemaVersion);
        Assert.NotEmpty(item.Id);
        Assert.Empty(item.Name);
        Assert.Empty(item.FilePath);
        Assert.Equal("Uncategorized", item.Category);
        Assert.Empty(item.Hotkey);
        Assert.Equal("0:00", item.Duration);
        Assert.Equal(1.0, item.Volume);
        Assert.False(item.IsFavorite);
        Assert.False(item.IsMissingFile);
        Assert.False(item.Normalized);
        Assert.Equal(1.0, item.NormalizationGain);
        Assert.Equal(0, item.PlayCount);
        Assert.Null(item.LastPlayedAt);
        Assert.NotEqual(default, item.CreatedAt);
    }

    [Fact]
    public void Id_IsUnique()
    {
        var item1 = new SoundLibraryItem();
        var item2 = new SoundLibraryItem();
        Assert.NotEqual(item1.Id, item2.Id);
    }
}

public sealed class CategoryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var c = new Category();

        Assert.Empty(c.Id);
        Assert.Empty(c.Name);
        Assert.Equal(0, c.SortOrder);
        Assert.Null(c.Color);
        Assert.Null(c.Icon);
    }
}

public sealed class AudioDeviceInfoTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var d = new AudioDeviceInfo();

        Assert.Empty(d.Id);
        Assert.Empty(d.Name);
        Assert.Equal(AudioDeviceKind.Input, d.Kind);
        Assert.False(d.IsDefault);
        Assert.False(d.IsVoicemeeter);
        Assert.True(d.IsAvailable);
    }
}

public sealed class VoicemeeterDetectionResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var r = new VoicemeeterDetectionResult();

        Assert.False(r.IsDetected);
        Assert.Null(r.Edition);
        Assert.Empty(r.Outputs);
        Assert.Null(r.Message);
    }
}
