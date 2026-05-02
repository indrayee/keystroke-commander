using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace KeystrokeCommander.Services;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public event EventHandler<string>? HotkeyPressed;

    private readonly IntPtr _hwnd;
    private readonly Dictionary<int, string> _hotkeys = new();
    private int _nextId = 1;

    public HotkeyManager(IntPtr hwnd)
    {
        _hwnd = hwnd;
        ComponentDispatcher.ThreadFilterMessage += OnThreadFilterMessage;
    }

    public bool Register(string hotkeyString, out string? error)
    {
        error = null;
        var (mods, vk) = ParseHotkey(hotkeyString);
        if (vk == 0)
        {
            error = "Invalid hotkey";
            return false;
        }
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods, vk))
        {
            error = $"Hotkey '{hotkeyString}' is already in use by another application";
            return false;
        }
        _hotkeys[id] = hotkeyString;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _hotkeys.Keys.ToList())
        {
            UnregisterHotKey(_hwnd, id);
        }
        _hotkeys.Clear();
    }

    public void RefreshProfileHotkeys(List<(string Hotkey, string ProfileId)> bindings)
    {
        UnregisterAll();
        foreach (var (hk, _) in bindings)
        {
            Register(hk, out _);
        }
    }

    private void OnThreadFilterMessage(ref MSG msg, ref bool handled)
    {
        if (msg.message == 0x0312 && _hotkeys.TryGetValue((int)msg.wParam, out var hk))
        {
            HotkeyPressed?.Invoke(this, hk);
            handled = true;
        }
    }

    private static (uint Mods, uint Vk) ParseHotkey(string s)
    {
        uint mods = 0;
        var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        uint vk = 0;
        foreach (var p in parts)
        {
            var upper = p.ToUpperInvariant();
            switch (upper)
            {
                case "CTRL" or "CONTROL": mods |= 0x0002; break;
                case "SHIFT": mods |= 0x0004; break;
                case "ALT": mods |= 0x0001; break;
                case "WIN": mods |= 0x0008; break;
                default:
                    vk = KeyToVk(upper);
                    break;
            }
        }
        return (mods, vk);
    }

    private static uint KeyToVk(string key)
    {
        if (key.StartsWith("F") && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
            return (uint)(0x70 + fn - 1);
        return key switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34, "5" => 0x35,
            "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39, "0" => 0x30,
            "SPACE" => 0x20, "ENTER" => 0x0D, "TAB" => 0x09,
            "END" => 0x23, "HOME" => 0x24, "INSERT" => 0x2D, "DELETE" => 0x2E,
            "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28,
            "ESC" or "ESCAPE" => 0x1B,
            _ => 0
        };
    }

    public void Dispose()
    {
        ComponentDispatcher.ThreadFilterMessage -= OnThreadFilterMessage;
        UnregisterAll();
    }
}
