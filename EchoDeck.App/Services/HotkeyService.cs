using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using EchoDeck.App.Models;

namespace EchoDeck.App.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;

    private readonly Dictionary<int, string> _hotkeyNames = new();
    private readonly Dictionary<string, int> _keyToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HotkeyGesture> _gestures = new(StringComparer.OrdinalIgnoreCase);
    private HwndSource? _source;
    private int _nextId = 1;

    public event EventHandler<string>? HotkeyPressed;

    public void Attach(HwndSource source)
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
        }
        _source = source;
        _source.AddHook(WndProc);
    }

    public bool Register(IntPtr handle, Key key, string name)
    {
        return Register(handle, new HotkeyGesture { Key = key.ToString() }, name);
    }

    public bool Register(IntPtr handle, HotkeyGesture gesture, string name)
    {
        if (!Enum.TryParse<Key>(gesture.Key, ignoreCase: true, out var key))
            return false;

        var id = _nextId++;
        var vk = KeyInterop.VirtualKeyFromKey(key);

        if (!RegisterHotKey(handle, id, gesture.ModifiersMask, (uint)vk))
        {
            return false;
        }

        _hotkeyNames[id] = name;
        _keyToId[name] = id;
        _gestures[name] = gesture;
        return true;
    }

    public bool IsRegistered(string name) => _keyToId.ContainsKey(name);

    public HotkeyGesture? GetGesture(string name) =>
        _gestures.TryGetValue(name, out var gesture) ? gesture : null;

    public bool Unregister(IntPtr handle, string name)
    {
        if (!_keyToId.TryGetValue(name, out var id))
            return false;

        UnregisterHotKey(handle, id);
        _hotkeyNames.Remove(id);
        _keyToId.Remove(name);
        _gestures.Remove(name);
        return true;
    }

    public void UnregisterAll(IntPtr handle)
    {
        foreach (var id in _hotkeyNames.Keys.ToArray())
        {
            UnregisterHotKey(handle, id);
        }

        _hotkeyNames.Clear();
        _keyToId.Clear();
        _gestures.Clear();
    }

    public void UnregisterAllExceptStopAll(IntPtr handle)
    {
        var stopAllId = _keyToId.GetValueOrDefault("Stop All");
        foreach (var kvp in _keyToId.ToList())
        {
            if (string.Equals(kvp.Key, "Stop All", StringComparison.OrdinalIgnoreCase))
                continue;

            UnregisterHotKey(handle, kvp.Value);
            _hotkeyNames.Remove(kvp.Value);
            _keyToId.Remove(kvp.Key);
            _gestures.Remove(kvp.Key);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            var id = wParam.ToInt32();
            if (_hotkeyNames.TryGetValue(id, out var name))
            {
                try
                {
                    HotkeyPressed?.Invoke(this, name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Hotkey WndProc error: {ex.Message}");
                }
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            UnregisterAll(_source.Handle);
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
