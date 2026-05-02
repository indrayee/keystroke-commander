using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using KeystrokeCommander.Models;

namespace KeystrokeCommander.Services;

public class MacroEngine : IDisposable
{
    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    static extern uint SendInput(uint nInputs, IntPtr pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint Type;
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    const uint INPUT_KEYBOARD = 1;
    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_SCANCODE = 0x0008;

    private readonly WindowManager _windowManager;
    private readonly Random _rng = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _runningRepeaters = new();
    private CancellationTokenSource? _sequentialCts;

    public bool IsRunning { get; private set; }

    public MacroEngine(WindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    public void StartSequential(Profile profile)
    {
        StopAll();
        _sequentialCts = new CancellationTokenSource();
        _ = RunSequentialAsync(profile, _sequentialCts.Token);
    }

    public void StartConcurrent(Profile profile)
    {
        StopAll();
        foreach (var r in profile.Repeaters)
        {
            var cts = new CancellationTokenSource();
            _runningRepeaters[r.Id] = cts;
            _ = RunRepeaterAsync(r, cts.Token);
        }
        IsRunning = true;
    }

    public void StopAll()
    {
        _sequentialCts?.Cancel();
        _sequentialCts = null;
        foreach (var cts in _runningRepeaters.Values)
        {
            cts.Cancel();
        }
        _runningRepeaters.Clear();
        IsRunning = false;
    }

    private async Task RunSequentialAsync(Profile profile, CancellationToken ct)
    {
        IsRunning = true;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                foreach (var step in profile.Steps)
                {
                    if (ct.IsCancellationRequested) break;
                    var delay = step.Jitter ? Jitter(step.DelayMs) : step.DelayMs;
                    _windowManager.SendToTarget(() => SendKey(step.Key, step.HoldMs));
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally { IsRunning = false; }
    }

    private async Task RunRepeaterAsync(ConcurrentRepeater r, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var interval = r.Jitter ? Jitter(r.IntervalMs) : r.IntervalMs;
                _windowManager.SendToTarget(() => SendKey(r.Key, r.HoldMs));
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private int Jitter(int baseMs)
    {
        var variance = baseMs * 0.1;
        return (int)(baseMs + _rng.NextDouble() * variance * 2 - variance);
    }

    const uint INPUT_MOUSE = 0;
    const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    const uint MOUSEEVENTF_LEFTUP = 0x0004;
    const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    private void SendKey(string key, int holdMs)
    {
        var parts = key.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var modifiers = parts[..^1].Select(ParseMod).Where(m => m != 0).ToList();
        var mainKey = parts[^1].ToUpperInvariant();

        if (mainKey is "CLICK" or "LCLICK" or "RCLICK")
        {
            SendMouseWithMods(modifiers, mainKey, holdMs);
            return;
        }

        var vk = KeyToVk(mainKey);
        if (vk == 0) return;

        // Down
        foreach (var mod in modifiers)
            SendKeybd(mod, 0);
        SendKeybd(vk, 0);
        if (holdMs > 0) Thread.Sleep(holdMs);
        // Up
        SendKeybd(vk, KEYEVENTF_KEYUP);
        foreach (var mod in modifiers.AsEnumerable().Reverse())
            SendKeybd(mod, KEYEVENTF_KEYUP);
    }

    private static void SendKeybd(ushort vk, uint flags)
    {
        var inp = new INPUT
        {
            Type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT { Vk = vk, Flags = flags, Time = 0, ExtraInfo = IntPtr.Zero }
        };
        SendInput(1, new[] { inp }, Marshal.SizeOf<INPUT>());
    }

    private static void SendMouseWithMods(List<ushort> modifiers, string clickType, int holdMs)
    {
        var (down, up) = clickType switch
        {
            "RCLICK" => (MOUSEEVENTF_RIGHTDOWN, MOUSEEVENTF_RIGHTUP),
            _ => (MOUSEEVENTF_LEFTDOWN, MOUSEEVENTF_LEFTUP),
        };

        // Modifiers down
        foreach (var mod in modifiers) SendKeybd(mod, 0);
        // Mouse down
        SendMouse(down);
        if (holdMs > 0) Thread.Sleep(holdMs);
        // Mouse up
        SendMouse(up);
        // Modifiers up
        foreach (var mod in modifiers.AsEnumerable().Reverse()) SendKeybd(mod, KEYEVENTF_KEYUP);
    }

    private static void SendMouse(uint flags)
    {
        var inp = new INPUT
        {
            Type = INPUT_MOUSE,
            ki = new KEYBDINPUT()
        };
        // We need MOUSEINPUT layout. Since MOUSEINPUT and KEYBDINPUT are the same size (24 bytes)
        // and share offset, we can abuse the union by writing raw bytes.
        // Simpler: define a proper struct for marshaling.
        // Actually, let's just use a separate struct and marshal.
        // Win: Create a byte array of the right size.
        int sz = Marshal.SizeOf<INPUT>();
        byte[] raw = new byte[sz];
        BitConverter.GetBytes((int)INPUT_MOUSE).CopyTo(raw, 0);
        BitConverter.GetBytes((int)0).CopyTo(raw, 4); // dx
        BitConverter.GetBytes((int)0).CopyTo(raw, 8); // dy
        BitConverter.GetBytes((uint)0).CopyTo(raw, 12); // mouseData
        BitConverter.GetBytes(flags).CopyTo(raw, 16); // dwFlags
        BitConverter.GetBytes((uint)0).CopyTo(raw, 20); // time
        // extraInfo is pointer-sized, leave zeroed
        var ptr = Marshal.AllocHGlobal(sz);
        Marshal.Copy(raw, 0, ptr, sz);
        SendInput(1, ptr, sz);
        Marshal.FreeHGlobal(ptr);
    }

    private static ushort ParseMod(string mod)
    {
        return mod.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => 0x11,
            "SHIFT" => 0x10,
            "ALT" => 0x12,
            "WIN" => 0x5B,
            _ => 0
        };
    }

    private static ushort KeyToVk(string key)
    {
        if (key.StartsWith("F") && int.TryParse(key[1..], out var fn) && fn is >= 1 and <= 24)
            return (ushort)(0x70 + fn - 1);
        return key switch
        {
            "A" => 0x41, "B" => 0x42, "C" => 0x43, "D" => 0x44, "E" => 0x45,
            "F" => 0x46, "G" => 0x47, "H" => 0x48, "I" => 0x49, "J" => 0x4A,
            "K" => 0x4B, "L" => 0x4C, "M" => 0x4D, "N" => 0x4E, "O" => 0x4F,
            "P" => 0x50, "Q" => 0x51, "R" => 0x52, "S" => 0x53, "T" => 0x54,
            "U" => 0x55, "V" => 0x56, "W" => 0x57, "X" => 0x58, "Y" => 0x59, "Z" => 0x5A,
            "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34, "5" => 0x35,
            "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39, "0" => 0x30,
            "SPACE" => 0x20, "ENTER" => 0x0D, "TAB" => 0x09,
            "END" => 0x23, "HOME" => 0x24, "INSERT" => 0x2D, "DELETE" => 0x2E,
            "LEFT" => 0x25, "UP" => 0x26, "RIGHT" => 0x27, "DOWN" => 0x28,
            "ESC" or "ESCAPE" => 0x1B,
            "CLICK" or "LCLICK" => 0x01,
            "RCLICK" => 0x02,
            "MCLICK" => 0x04,
            _ => 0
        };
    }

    public void Dispose()
    {
        StopAll();
    }
}
