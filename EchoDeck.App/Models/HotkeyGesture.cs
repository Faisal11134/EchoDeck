namespace EchoDeck.App.Models;

public sealed class HotkeyGesture
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = string.Empty;

    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Alt) parts.Add("Alt");
            if (Shift) parts.Add("Shift");
            if (Win) parts.Add("Win");
            if (!string.IsNullOrWhiteSpace(Key)) parts.Add(Key);
            return string.Join(" + ", parts);
        }
    }

    public uint ModifiersMask
    {
        get
        {
            uint mask = 0;
            if (Alt) mask |= 0x0001;
            if (Ctrl) mask |= 0x0002;
            if (Shift) mask |= 0x0004;
            if (Win) mask |= 0x0008;
            return mask;
        }
    }

    public static HotkeyGesture Parse(string? text)
    {
        var gesture = new HotkeyGesture();
        if (string.IsNullOrWhiteSpace(text))
            return gesture;

        var parts = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (string.Equals(part, "Ctrl", StringComparison.OrdinalIgnoreCase))
                gesture.Ctrl = true;
            else if (string.Equals(part, "Alt", StringComparison.OrdinalIgnoreCase))
                gesture.Alt = true;
            else if (string.Equals(part, "Shift", StringComparison.OrdinalIgnoreCase))
                gesture.Shift = true;
            else if (string.Equals(part, "Win", StringComparison.OrdinalIgnoreCase))
                gesture.Win = true;
            else
                gesture.Key = part;
        }

        return gesture;
    }

    public override string ToString() => DisplayText;
}