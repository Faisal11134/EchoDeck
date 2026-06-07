namespace EchoDeck.App.ViewModels;

public sealed class SoundItemViewModel : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private string _filePath = string.Empty;
    private string _category = "Uncategorized";
    private string _hotkey = string.Empty;
    private string _duration = "0:00";
    private double _volume = 1.0;
    private bool _isFavorite;
    private bool _isMissingFile;
    private bool _normalized;
    private double _normalizationGain = 1.0;
    private int _playCount;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime? _lastPlayedAt;
    private bool _isPlaying;
    private bool _isMarkedForBulkAction;

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public string Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public double Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, Math.Clamp(value, 0.0, 2.0));
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsMissingFile
    {
        get => _isMissingFile;
        set => SetProperty(ref _isMissingFile, value);
    }

    public bool Normalized
    {
        get => _normalized;
        set => SetProperty(ref _normalized, value);
    }

    public double NormalizationGain
    {
        get => _normalizationGain;
        set => SetProperty(ref _normalizationGain, value);
    }

    public int PlayCount
    {
        get => _playCount;
        set => SetProperty(ref _playCount, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTime? LastPlayedAt
    {
        get => _lastPlayedAt;
        set => SetProperty(ref _lastPlayedAt, value);
    }

    public bool IsMarkedForBulkAction
    {
        get => _isMarkedForBulkAction;
        set => SetProperty(ref _isMarkedForBulkAction, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetProperty(ref _isPlaying, value);
    }
}
