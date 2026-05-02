using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace KeystrokeCommander.Services;

public class HotkeyRecorder
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    private readonly HashSet<Key> _modKeys = new()
    {
        Key.LeftShift, Key.RightShift,
        Key.LeftCtrl, Key.RightCtrl,
        Key.LeftAlt, Key.RightAlt,
        Key.LWin, Key.RWin
    };

    private List<Key> _pressed = new();

    public bool IsModifier(Key key) => _modKeys.Contains(key);

    public string? RecordRawCombo(List<Key> downKeys)
    {
        var mods = downKeys.Where(IsModifier).Distinct().OrderBy(k => k);
        var normal = downKeys.Where(k => !IsModifier(k)).Distinct().OrderBy(k => k);
        if (!normal.Any()) return null;

        var parts = new List<string>();
        foreach (var m in mods)
        {
            parts.Add(m switch
            {
                Key.LeftShift or Key.RightShift => "Shift",
                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                Key.LeftAlt or Key.RightAlt => "Alt",
                Key.LWin or Key.RWin => "Win",
                _ => m.ToString()
            });
        }
        parts.Add(normal.First().ToString());
        return string.Join("+", parts);
    }

    public string FormatDisplay(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return "(none)";
        return combo.Replace("+", " + ");
    }
}
