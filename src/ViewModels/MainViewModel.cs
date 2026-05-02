using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KeystrokeCommander;
using KeystrokeCommander.Models;
using KeystrokeCommander.Services;

namespace KeystrokeCommander.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly WindowManager _windowManager = new();
    private readonly MacroEngine _macroEngine;
    private readonly ProfileStore _store = new();
    private readonly AppSettings _settings;
    private HotkeyManager? _hotkeyManager;

    [ObservableProperty]
    private ObservableCollection<Profile> _profiles = new();

    [ObservableProperty]
    private Profile? _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LockButtonText))]
    [NotifyPropertyChangedFor(nameof(LockDotColor))]
    private bool _isLocked;

    [ObservableProperty]
    private bool _alwaysOnTop;

    public bool WindowPickerIsOpen { get; set; }

    [ObservableProperty]
    private ObservableCollection<WindowInfo> _visibleWindows = new();

    [ObservableProperty]
    private WindowInfo? _selectedWindow;

    [ObservableProperty]
    private string _globalStopHotkey = "Shift+Alt+End";

    [ObservableProperty]
    private bool _isRecordingHotkey;

    [ObservableProperty]
    private string _hotkeyStatusText = "Idle";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HotkeyStatusColor))]
    private bool _hotkeysActive;

    public Brush HotkeyStatusColor => HotkeysActive ? Brushes.LimeGreen : Brushes.OrangeRed;

    [ObservableProperty]
    private string _runningStatusText = "";

    public void UpdateRunningStatus(bool running) => RunningStatusText = running ? "🟢 RUNNING" : "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorTitle))]
    [NotifyPropertyChangedFor(nameof(AddRowText))]
    [NotifyPropertyChangedFor(nameof(ModeHint))]
    [NotifyPropertyChangedFor(nameof(SeqButtonBg))]
    [NotifyPropertyChangedFor(nameof(SeqButtonFg))]
    [NotifyPropertyChangedFor(nameof(ConButtonBg))]
    [NotifyPropertyChangedFor(nameof(ConButtonFg))]
    [NotifyPropertyChangedFor(nameof(IsSequential))]
    [NotifyPropertyChangedFor(nameof(IsConcurrent))]
    [NotifyPropertyChangedFor(nameof(EditorItems))]
    private MacroMode _currentMode = MacroMode.Sequential;

    public ObservableCollection<object> EditorItems { get; } = new();

    public MainViewModel()
    {
        _macroEngine = new MacroEngine(_windowManager);
        _settings = _store.Load();
        GlobalStopHotkey = _settings.GlobalStopHotkey;
        foreach (var p in _settings.Profiles)
            Profiles.Add(p);

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id.ToString() == _settings.DefaultProfileId)
                       ?? Profiles.FirstOrDefault()
                       ?? new Profile { Name = "Default" };

        if (!Profiles.Contains(SelectedProfile))
            Profiles.Add(SelectedProfile);

        _ = RefreshWindowsLoopAsync();
    }

    public void InitHotkeys(IntPtr hwnd)
    {
        _hotkeyManager = new HotkeyManager(hwnd);
        _hotkeyManager.HotkeyPressed += (_, hotkey) =>
        {
            if (hotkey == GlobalStopHotkey)
            {
                _macroEngine.StopAll();
                UpdateRunningStatus(false);
                return;
            }
            var profile = Profiles.FirstOrDefault(p => p.Hotkey == hotkey);
            if (profile == null) return;
            if (_macroEngine.IsRunning)
            {
                _macroEngine.StopAll();
                UpdateRunningStatus(false);
                return;
            }
            StartProfile(profile);
            UpdateRunningStatus(true);
        };
        RefreshHotkeys();
    }

    private void RefreshHotkeys()
    {
        _hotkeyManager?.UnregisterAll();
        int ok = 0, fail = 0;
        List<string> failDetails = new();

        if (_hotkeyManager?.Register(GlobalStopHotkey, out var err) == true) ok++; else { fail++; if (err != null) failDetails.Add(err); }
        foreach (var p in Profiles.Where(p => !string.IsNullOrWhiteSpace(p.Hotkey)))
        {
            if (_hotkeyManager?.Register(p.Hotkey!, out var e2) == true) ok++; else { fail++; if (e2 != null) failDetails.Add(e2); }
        }
        HotkeysActive = fail == 0 && ok > 0;
        if (fail > 0)
            HotkeyStatusText = $"{ok} OK, {fail} FAILED";
        else if (ok > 0)
            HotkeyStatusText = $"{ok} registered ✓";
        else
            HotkeyStatusText = "None";
    }

    private async Task RefreshWindowsLoopAsync()
    {
        while (true)
        {
            await Task.Delay(3000);
            if (WindowPickerIsOpen) continue;
            var list = _windowManager.GetVisibleWindows();
            await System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                var prev = SelectedWindow;
                VisibleWindows.Clear();
                foreach (var w in list) VisibleWindows.Add(w);
                // Restore selection if window still exists
                if (prev != null)
                {
                    var match = list.FirstOrDefault(w => w.Hwnd == prev.Hwnd);
                    if (match != null) SelectedWindow = match;
                }
            })!;
        }
    }

    [RelayCommand]
    private void RefreshWindows()
    {
        var list = _windowManager.GetVisibleWindows();
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var prev = SelectedWindow;
            VisibleWindows.Clear();
            foreach (var w in list) VisibleWindows.Add(w);
            // Try to restore selection if window still exists
            if (prev != null)
            {
                var match = VisibleWindows.FirstOrDefault(w => w.Hwnd == prev.Hwnd);
                if (match != null) SelectedWindow = match;
            }
        });
    }

    [RelayCommand]
    private void LockWindow()
    {
        if (IsLocked)
        {
            _windowManager.Unlock();
            IsLocked = false;
            return;
        }
        if (SelectedWindow == null) return;
        _windowManager.LockWindow(SelectedWindow.Hwnd, SelectedWindow.Title, SelectedWindow.ProcessName);
        IsLocked = true;
    }

    [RelayCommand]
    private void StopAll() => _macroEngine.StopAll();

    [RelayCommand]
    private void NewProfile()
    {
        var p = new Profile { Name = $"Profile {Profiles.Count + 1}" };
        Profiles.Add(p);
        SelectedProfile = p;
        SaveProfiles();
    }

    [RelayCommand]
    private void SetModeSequential()
    {
        if (SelectedProfile == null) return;
        SelectedProfile.Mode = MacroMode.Sequential;
        CurrentMode = MacroMode.Sequential;
        SaveProfiles();
    }

    [RelayCommand]
    private void SetModeConcurrent()
    {
        if (SelectedProfile == null) return;
        SelectedProfile.Mode = MacroMode.Concurrent;
        CurrentMode = MacroMode.Concurrent;
        SaveProfiles();
    }

    [RelayCommand]
    private void AddRow()
    {
        if (SelectedProfile == null) return;
        if (SelectedProfile.Mode == MacroMode.Sequential)
        {
            var step = new MacroStep();
            SelectedProfile.Steps.Add(step);
            EditorItems.Add(step);
        }
        else
        {
            var rep = new ConcurrentRepeater();
            SelectedProfile.Repeaters.Add(rep);
            EditorItems.Add(rep);
        }
        SaveProfiles();
    }

    [RelayCommand]
    private void DeleteRow(object? parameter)
    {
        if (parameter is not { } item || SelectedProfile == null) return;
        if (item is MacroStep s)
        {
            SelectedProfile.Steps.Remove(s);
            EditorItems.Remove(s);
        }
        else if (item is ConcurrentRepeater r)
        {
            SelectedProfile.Repeaters.Remove(r);
            EditorItems.Remove(r);
        }
        SaveProfiles();
    }

    [RelayCommand]
    private void ChangeHotkey()
    {
        if (SelectedProfile == null) return;
        IsRecordingHotkey = true;
        var dlg = new HotkeyDialog { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.CapturedCombo))
        {
            SelectedProfile.Hotkey = dlg.CapturedCombo;
            OnPropertyChanged(nameof(SelectedProfile));
            RefreshHotkeys();
            SaveProfiles();
        }
        IsRecordingHotkey = false;
    }

    [RelayCommand]
    private void ChangeGlobalStopHotkey()
    {
        IsRecordingHotkey = true;
        var dlg = new HotkeyDialog { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.CapturedCombo))
        {
            GlobalStopHotkey = dlg.CapturedCombo;
            RefreshHotkeys();
            SaveProfiles();
        }
        IsRecordingHotkey = false;
    }

    [RelayCommand]
    private void SelectProfile(object? parameter)
    {
        if (parameter is Profile p)
        {
            SelectedProfile = p;
        }
    }

    private void StartProfile(Profile profile)
    {
        if (profile.Mode == MacroMode.Sequential)
            _macroEngine.StartSequential(profile);
        else
            _macroEngine.StartConcurrent(profile);
    }

    public void RefreshEditor()
    {
        EditorItems.Clear();
        if (SelectedProfile == null) return;
        var items = SelectedProfile.Mode == MacroMode.Sequential
            ? SelectedProfile.Steps.Cast<object>()
            : SelectedProfile.Repeaters.Cast<object>();
        foreach (var item in items)
            EditorItems.Add(item);
    }

    partial void OnSelectedProfileChanged(Profile? value)
    {
        if (value != null)
        {
            CurrentMode = value.Mode;
            RefreshEditor();
        }
    }

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        if (IsLocked && value != null)
        {
            _windowManager.LockWindow(value.Hwnd, value.Title, value.ProcessName);
        }
    }

    public string LockButtonText => IsLocked ? "Unlock" : "Lock";
    public Brush LockDotColor => IsLocked ? Brushes.LimeGreen : Brushes.Gray;
    public string ProfileCountText => $"{Profiles.Count} profiles";

    public string EditorTitle => CurrentMode == MacroMode.Sequential ? "Sequential Steps" : "Concurrent Repeaters";
    public string AddRowText => CurrentMode == MacroMode.Sequential ? "+ Add Step" : "+ Add Repeater";
    public string ModeHint => CurrentMode == MacroMode.Sequential
        ? "Sequential mode: Steps run in order, one after another. Use for skill rotations or combo sequences."
        : "Concurrent mode (\"Diablo Mode\"): Each row runs on its own independent timer. Start/stop individually or all at once.";

    public Brush SeqButtonBg => CurrentMode == MacroMode.Sequential ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    public Brush SeqButtonFg => CurrentMode == MacroMode.Sequential ? Brushes.White : Brushes.Gray;
    public Brush ConButtonBg => CurrentMode == MacroMode.Concurrent ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
    public Brush ConButtonFg => CurrentMode == MacroMode.Concurrent ? Brushes.White : Brushes.Gray;

    public bool IsSequential => CurrentMode == MacroMode.Sequential;
    public bool IsConcurrent => CurrentMode == MacroMode.Concurrent;

    public void Cleanup()
    {
        _macroEngine.Dispose();
        _hotkeyManager?.Dispose();
        SaveProfiles();
    }

    public void SaveProfiles()
    {
        _settings.Profiles = Profiles.ToList();
        _settings.DefaultProfileId = SelectedProfile?.Id.ToString();
        _settings.GlobalStopHotkey = GlobalStopHotkey;
        _store.Save(_settings);
    }
}
