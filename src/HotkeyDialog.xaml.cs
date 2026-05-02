using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace KeystrokeCommander;

public partial class HotkeyDialog : Window
{
    private readonly List<Key> _keysDown = new();
    private readonly HashSet<Key> _modKeys = new()
    {
        Key.LeftShift, Key.RightShift,
        Key.LeftCtrl, Key.RightCtrl,
        Key.LeftAlt, Key.RightAlt,
        Key.LWin, Key.RWin
    };

    private string? _capturedCombo;

    public string? CapturedCombo => _capturedCombo;
    public bool Assigned { get; private set; }

    public HotkeyDialog()
    {
        InitializeComponent();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (!_keysDown.Contains(e.Key) && e.Key != Key.System)
            _keysDown.Add(e.Key);
        UpdateDisplay();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed) return;
        if (!_keysDown.Contains(key))
            _keysDown.Add(key);
        UpdateDisplay();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        _keysDown.Remove(key);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_keysDown.Count == 0)
        {
            ComboText.Text = "(press keys...)";
            _capturedCombo = null;
            AssignBtn.IsEnabled = false;
            return;
        }
        var mods = _keysDown.Where(k => _modKeys.Contains(k))
                            .Select(k => k switch
                            {
                                Key.LeftShift or Key.RightShift => "Shift",
                                Key.LeftCtrl or Key.RightCtrl => "Ctrl",
                                Key.LeftAlt or Key.RightAlt => "Alt",
                                Key.LWin or Key.RWin => "Win",
                                _ => k.ToString()
                            }).Distinct().ToList();
        var normal = _keysDown.Where(k => !_modKeys.Contains(k))
                              .Select(k => k == Key.LeftShift || k == Key.RightShift ? "Shift" :
                                             k == Key.LeftCtrl || k == Key.RightCtrl ? "Ctrl" :
                                             k == Key.LeftAlt || k == Key.RightAlt ? "Alt" :
                                             k == Key.LWin || k == Key.RWin ? "Win" :
                                             k.ToString()).Distinct().ToList();
        if (normal.Count == 0 && mods.Count > 0)
        {
            ComboText.Text = string.Join(" + ", mods) + " + ...";
            _capturedCombo = null;
            AssignBtn.IsEnabled = false;
            return;
        }
        var parts = new List<string>(mods);
        parts.AddRange(normal.Take(1));
        var combo = string.Join("+", parts);
        _capturedCombo = combo;
        ComboText.Text = string.Join(" + ", parts);
        AssignBtn.IsEnabled = true;
    }

    private void AssignBtn_Click(object sender, RoutedEventArgs e)
    {
        Assigned = true;
        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        Assigned = false;
        DialogResult = false;
        Close();
    }
}
