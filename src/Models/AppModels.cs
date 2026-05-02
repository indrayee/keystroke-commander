using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KeystrokeCommander.Models;

public enum MacroMode
{
    Sequential,
    Concurrent
}

public class MacroStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public int DelayMs { get; set; } = 1000;
    public int HoldMs { get; set; } = 50;
    public bool Jitter { get; set; } = true;
}

public class ConcurrentRepeater
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public int IntervalMs { get; set; } = 1000;
    public int DelayMs => IntervalMs; // alias for XAML binding
    public int HoldMs { get; set; } = 50;
    public bool Jitter { get; set; } = true;
}

public class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Profile";
    public MacroMode Mode { get; set; } = MacroMode.Sequential;
    public string? Hotkey { get; set; }
    public List<MacroStep> Steps { get; set; } = new();
    public List<ConcurrentRepeater> Repeaters { get; set; } = new();

    public string HotkeyDisplay => string.IsNullOrWhiteSpace(Hotkey) ? "—" : Hotkey;
    public string ModeDisplay => Mode == MacroMode.Sequential ? "Sequential" : "Concurrent";
}

public class AppSettings
{
    public int Version { get; set; } = 1;
    public string? DefaultProfileId { get; set; }
    public string GlobalStopHotkey { get; set; } = "Shift+Alt+End";
    public List<Profile> Profiles { get; set; } = new();
}

public enum TargetStatus
{
    None,
    Locked,
    Dead,
    Minimized
}

public record WindowInfo(IntPtr Hwnd, string Title, string ProcessName, int Width, int Height);
