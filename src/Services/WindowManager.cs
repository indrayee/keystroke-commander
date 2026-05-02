using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using KeystrokeCommander.Models;

namespace KeystrokeCommander.Services;

public class WindowManager
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    private IntPtr _lockedHwnd = IntPtr.Zero;
    private string _lockedProcessName = string.Empty;
    private string _lockedTitle = string.Empty;

    public IntPtr LockedHwnd => _lockedHwnd;
    public TargetStatus Status { get; private set; } = TargetStatus.None;

    public void LockWindow(IntPtr hwnd, string title, string processName)
    {
        _lockedHwnd = hwnd;
        _lockedTitle = title;
        _lockedProcessName = processName;
        RefreshStatus();
    }

    public void Unlock()
    {
        _lockedHwnd = IntPtr.Zero;
        Status = TargetStatus.None;
    }

    public void RefreshStatus()
    {
        if (_lockedHwnd == IntPtr.Zero)
        {
            Status = TargetStatus.None;
            return;
        }
        if (!IsWindow(_lockedHwnd))
        {
            Status = TargetStatus.Dead;
            return;
        }
        if (IsIconic(_lockedHwnd))
        {
            Status = TargetStatus.Minimized;
            return;
        }
        Status = TargetStatus.Locked;
    }

    public void SendToTarget(Action sendAction)
    {
        RefreshStatus();
        if (Status != TargetStatus.Locked)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowManager] SendToTarget blocked — status={Status}");
            return;
        }

        var current = GetForegroundWindow();
        bool needRestore = current != _lockedHwnd && current != IntPtr.Zero;

        if (needRestore)
        {
            var ourThread = GetCurrentThreadId();
            var fgThread = GetWindowThreadProcessId(current, out _);
            AttachThreadInput(ourThread, fgThread, true);
            bool focused = SetForegroundWindow(_lockedHwnd);
            AttachThreadInput(ourThread, fgThread, false);

            if (!focused)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowManager] SetForegroundWindow FAILED for hwnd={_lockedHwnd}");
                return;
            }

            // Windows focus switch is async — give it a beat
            System.Threading.Thread.Sleep(50);
            var nowFg = GetForegroundWindow();
            if (nowFg != _lockedHwnd)
            {
                System.Diagnostics.Debug.WriteLine($"[WindowManager] Focus mismatch after switch. Expected {_lockedHwnd}, got {nowFg}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[WindowManager] Sending keystrokes to hwnd={_lockedHwnd}");
        sendAction();
        System.Threading.Thread.Sleep(50); // Let keystrokes be processed before we steal focus back

        if (needRestore)
        {
            var ourThread = GetCurrentThreadId();
            var fgThread = GetWindowThreadProcessId(current, out _);
            AttachThreadInput(ourThread, fgThread, true);
            SetForegroundWindow(current);
            AttachThreadInput(ourThread, fgThread, false);
        }
    }

    public List<WindowInfo> GetVisibleWindows()
    {
        var list = new List<WindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(512);
            GetWindowText(hwnd, sb, 512);
            var title = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(title)) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            string processName = GetProcessName(pid);
            GetWindowRect(hwnd, out var rect);
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w > 0 && h > 0)
                list.Add(new WindowInfo(hwnd, title, processName, w, h));
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return "unknown"; }
    }
}
