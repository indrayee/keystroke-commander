using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

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

public partial class Profile : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "New Profile";

    public MacroMode Mode { get; set; } = MacroMode.Sequential;

    [ObservableProperty]
    private string? _hotkey;

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

public class WindowInfo : IEquatable<WindowInfo>
{
    public IntPtr Hwnd { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ProcessName { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }

    public WindowInfo(IntPtr hwnd, string title, string processName, int width, int height)
    {
        Hwnd = hwnd;
        Title = title;
        ProcessName = processName;
        Width = width;
        Height = height;
    }

    public bool Equals(WindowInfo? other) => other is not null && Hwnd == other.Hwnd;
    public override bool Equals(object? obj) => obj is WindowInfo other && Equals(other);
    public override int GetHashCode() => Hwnd.GetHashCode();

    public override string ToString() => $"{ProcessName} \"{Title}\" ({Width}x{Height})";
}
