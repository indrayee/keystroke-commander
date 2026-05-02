using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace KeystrokeCommander.Dialogs;

public partial class HotkeyDialog : Window
{
    private readonly HashSet<Key> _downKeys = new();
    private Key? _mainKey;
    private bool _finished = false;

    public string? CapturedHotkey { get; private set; }

    public HotkeyDialog()
    {
        InitializeComponent();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.System && e.SystemKey != Key.None)
        {
            _downKeys.Add(e.SystemKey);
        }
        else if (e.Key != Key.None)
        {
            _downKeys.Add(e.Key);
        }
        UpdateDisplay();
        e.Handled = true;
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (_finished) return;

        Key checkKey = e.Key == Key.System ? e.SystemKey : e.Key;
        if (!_downKeys.Remove(checkKey)) return;

        if (_mainKey.HasValue && !_downKeys.Any(k => IsModifier(k)))
        {
            // Finalize the capture
            var parts = new List<string>();
            if (_downKeys.Contains(Key.LeftCtrl) || _downKeys.Contains(Key.RightCtrl))
                parts.Add("Ctrl");
            if (_downKeys.Contains(Key.LeftShift) || _downKeys.Contains(Key.RightShift))
                parts.Add("Shift");
            if (_downKeys.Contains(Key.LeftAlt) || _downKeys.Contains(Key.RightAlt))
                parts.Add("Alt");
            if (_downKeys.Contains(Key.LWin) || _downKeys.Contains(Key.RWin))
                parts.Add("Win");

            var main = KeyToString(_mainKey.Value);
            if (main != null)
            {
                if (parts.Any() || IsValidMainKey(_mainKey.Value))
                {
                    parts.Add(main);
                    CapturedHotkey = string.Join("+", parts);
                    HotkeyText.Text = CapturedHotkey;
                    HotkeyText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    _finished = true;
                }
            }
        }
    }

    private void UpdateDisplay()
    {
        var modifiers = _downKeys.Where(IsModifier).Select(KeyToString).Where(s => s != null);
        var nonMods = _downKeys.Where(k => !IsModifier(k)).ToList();

        if (nonMods.Any())
            _mainKey = nonMods.Last();

        var parts = new List<string>();
        parts.AddRange(modifiers!);
        if (_mainKey.HasValue)
        {
            var main = KeyToString(_mainKey.Value);
            if (main != null) parts.Add(main);
        }

        HotkeyText.Text = parts.Any() ? string.Join("+", parts) : "(press keys...)";
    }

    private static bool IsModifier(Key key)
    {
        return key == Key.LeftCtrl || key == Key.RightCtrl
            || key == Key.LeftShift || key == Key.RightShift
            || key == Key.LeftAlt || key == Key.RightAlt
            || key == Key.LWin || key == Key.RWin;
    }

    private static bool IsValidMainKey(Key key)
    {
        // Must be a letter, digit, F-key, or navigation key
        return (key >= Key.F1 && key <= Key.F24)
            || (key >= Key.D0 && key <= Key.Z)
            || key == Key.Space || key == Key.Enter || key == Key.Tab
            || key == Key.Home || key == Key.End || key == Key.Insert || key == Key.Delete
            || key == Key.Left || key == Key.Up || key == Key.Right || key == Key.Down
            || key == Key.Escape;
    }

    private static string? KeyToString(Key key)
    {
        if (key >= Key.F1 && key <= Key.F24)
            return "F" + (key - Key.F1 + 1);

        if (key >= Key.D0 && key <= Key.D9)
            return ((char)('0' + (key - Key.D0))).ToString();

        if (key >= Key.A && key <= Key.Z)
            return key.ToString();

        return key switch
        {
            Key.LeftCtrl or Key.RightCtrl => "Ctrl",
            Key.LeftShift or Key.RightShift => "Shift",
            Key.LeftAlt or Key.RightAlt => "Alt",
            Key.LWin or Key.RWin => "Win",
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.Tab => "Tab",
            Key.Home => "Home",
            Key.End => "End",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Left => "Left",
            Key.Up => "Up",
            Key.Right => "Right",
            Key.Down => "Down",
            Key.Escape => "Esc",
            Key.System => "Alt",
            _ => key.ToString()
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = !string.IsNullOrWhiteSpace(CapturedHotkey);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
