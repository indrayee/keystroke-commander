# Keystroke Commander — Design Document

## 1. Purpose
Standalone Windows desktop app for sending automated keystrokes to a **locked target window**, regardless of whether that window has foreground focus. Designed for ARPGs, idle games, and productivity automation where concurrent repeating keystrokes are required.

## 2. Core Principles
- **Fail-closed**: If target window dies or disappears, macros stop. No stray input to whatever window accidentally gains focus.
- **No botting signatures**: Randomized jitter on delays, human-like keydown/keyup timing to reduce anti-cheat detection vectors.
- **Zero friction**: Start app → pick window → load profile → hit hotkey. No ceremony.

---

## 3. High-Level Architecture

```
┌──────────────────────────────────────────────────────────────┐
│  Keystroke Commander (Win32 / C# WPF or Qt / Tauri+Rust)    │
│                                                              │
│  ┌─────────────────┐    ┌─────────────────────────────┐   │
│  │  Target Bar     │    │  Profile + Sequence Editor  │   │
│  │  (Window Lock)  │    │                             │   │
│  └─────────────────┘    └─────────────────────────────┘   │
│                                                              │
│  Services:                                                   │
│  • WindowManager (EnumWindows, handle caching)              │
│  • HotkeyManager (RegisterHotKey / low-level hook fallback)│
│  • MacroEngine (timer pool, SendInput dispatcher)           │
│  • ProfileStore (JSON on disk, portable)                   │
│  • AntiCheatJitter (±10% delay variance)                    │
└──────────────────────────────────────────────────────────────┘
```

---

## 4. UI Layout

### 4.1 Top Bar: Target Lock
| Element | Behavior |
|---------|----------|
| **Window Picker dropdown** | Lists `Window Title (exeName)` from `EnumWindows`. Updates live every 2s while open. |
| **Lock / Unlock button** | Locks selected HWND. While locked, all macro output routes exclusively to this window via `PostMessage` or `SendInput` with focus-then-restore. |
| **Target Status indicator** | Green = locked & alive. Red = dead/minimized/focus lost. Yellow = locked but not foreground. |
| **"Always on Top" toggle** | Keeps commander visible during gameplay. |

**Window targeting modes** (dropdown options):
- `Exact HWND` (fastest, brittle)
- `Exact title match`
- `Fuzzy title contains` (e.g., "Diablo")
- `Process name` (e.g., `Diablo IV.exe`) — survives title changes, survives restarts

### 4.2 Left Panel: Profile List
- **Tree/List view** of all profiles.
- Columns: `Name`, `Hotkey`, `Active?`
- Context menu: Rename, Duplicate, Delete, Set as Default.
- **Global Stop Hotkey** row pinned at top (always visible, editable).
- **New Profile** button.

### 4.3 Right Panel: Sequence Editor
Two **mutually exclusive modes** per profile (chosen via toggle/tab):

#### Mode A: Sequential Macro
Runs keystrokes in order, one after another, with per-step delays.

| Step | Keystroke | Delay After (ms) | Repeat N× | Randomize |
|------|-----------|------------------|-----------|-----------|
| 1    | `1`       | 1000             | 1         | [x]       |
| 2    | `Shift+Click` | 500          | 3         | [ ]       |

- **Randomize checkbox**: adds ±10% jitter to the delay on each execution.
- **Hold duration**: optional ms to hold the key down before releasing (default 50ms).

#### Mode B: Concurrent Repeaters ("Diablo Mode")
Multiple independent keystrokes, each on its own repeating timer.

| Keystroke | Interval (ms) | Hold (ms) | Jitter | Status |
|-----------|---------------|-----------|--------|--------|
| `1`       | 1000          | 50        | [x]    | ▶ Running |
| `2`       | 2000          | 50        | [x]    | ▶ Running |
| `3`       | 5000          | 50        | [x]    | ⏸ Paused  |

Each row is a self-contained timer. Start/stop per row or all at once.

---

## 5. Profile System

### 5.1 File Format (`profiles/*.json`)
```json
{
  "version": 1,
  "defaultProfileId": "uuid-abc",
  "globalStopHotkey": "Shift+Alt+End",
  "profiles": [
    {
      "id": "uuid-abc",
      "name": "Diablo Farm",
      "mode": "concurrent",
      "hotkey": "F2",
      "sequences": [
        { "key": "1", "intervalMs": 1000, "holdMs": 50, "jitter": true },
        { "key": "2", "intervalMs": 2000, "holdMs": 50, "jitter": true }
      ]
    }
  ]
}
```

### 5.2 Hotkey Specification
Stored as canonical strings: `Ctrl+Shift+A`, `F1`, `Shift+Alt+S`, `Win+X`.
Validation rules:
- Must contain at least one modifier OR be a function key (F1–F24).
- No bare letter keys alone (prevents accidental typing interference).
- No system-reserved combos (Ctrl+Alt+Del, Win+L, etc.).

### 5.3 Save / Load
- Auto-save on every edit (debounced 500ms).
- Manual **Export/Import** to `.ksc` file (zipped JSON + assets) for sharing.
- Portable: store in `%LOCALAPPDATA%\KeystrokeCommander\` or next to exe if `portable` flag file exists.

---

## 6. Macro Engine

### 6.1 Sending Methods (runtime selectable per profile)
| Method | Use Case | Drawback |
|--------|----------|----------|
| `SendInput` | Games with raw input, anti-cheat lenient | Requires restoring focus briefly; some AC detects synthetic input |
| `PostMessage(WM_KEYDOWN/UP)` | Safe, no focus steal | Many modern games ignore posted messages |
| `SendMessage` | Same as PostMessage but blocking | Same limitations |

**Default strategy**: `SendInput` with focus-then-restore for the locked window.

### 6.2 Focus-then-Restore Flow
```
1. Save current foreground HWND
2. SetForegroundWindow(target)
3. SendInput(keys)
4. SetForegroundWindow(saved)
```
- If `target` is already foreground, skip steps 1 & 4.
- If step 2 fails (target hung/minimized), abort the send and mark target red.

### 6.3 Timer Pool
- One `System.Threading.Timer` per concurrent repeater.
- One sequential runner per active profile.
- Max concurrent timers capped at 32 (sane limit).

### 6.4 Jitter Formula
```
actualDelay = baseDelay * (1 + rand(-0.1, +0.1))
```
Optional per-step override: 0% to 50%.

---

## 7. Global Hotkey System

### 7.1 Primary: `RegisterHotKey` (Win32)
- Reliable, low overhead, no hook controversy.
- Limited: max 16384 per process, modifiers only (no bare keys).
- If a hotkey is already taken by another app, show toast: "F5 is already bound by Discord. Choose another."

### 7.2 Fallback: Low-Level Keyboard Hook (`SetWindowsHookEx`)
- Used only if user insists on bare-key triggers (not recommended).
- Requires admin for some contexts; flagged by anti-cheat more aggressively.

---

## 8. Safety & Anti-Cheat Considerations

**This app will be flagged by aggressive anti-cheat (EAC, Vanguard, BattlEye).** Design mitigations:
1. **No DLL injection** — ever. Only OS-level input APIs.
2. **No memory reading/writing**.
3. **Configurable randomization** — breaks perfectly periodic patterns.
4. **Humanized hold times** — random 45–75ms instead of fixed 0ms.
5. **Disclaimer on first run**: "Use at your own risk. Some online games may ban for automation."
6. **Offline-only profile preset** for known multiplayer games — warn before activation.

---

## 9. Additional Ideas / Future Features

| Priority | Feature | Notes |
|----------|---------|-------|
| P1 | **Mouse click injection** | `SendInput` with MOUSEEVENTF_LEFTDOWN. Critical for Diablo-style games. |
| P1 | **Profile switcher hotkey** | Cycle default profile without opening UI. |
| P2 | **Screen pixel trigger** | Start macro when pixel at (x,y) changes color (health potion auto-use). |
| P2 | **Audio cue on start/stop** | Short beep so user knows macro engaged while in fullscreen. |
| P2 | **Run-once vs toggle vs hold** | Toggle: hit F2 to start, hit F2 to stop. Hold: only while key is held. |
| P3 | **Lua scripting** | Advanced users write custom logic instead of static sequences. |
| P3 | **OBS / streaming mode** | Hide window from capture so it doesn't appear on stream. |

---

## 10. Tech Stack Recommendation

**Option A: C# WPF (.NET 8)** — Recommended
- Native P/Invoke for Win32 APIs is trivial.
- Rich UI, data binding, JSON serialization built-in.
- Single-file publish to `.exe`.

**Option B: Tauri (Rust + Web UI)**
- Rust has excellent `windows` crate for Win32 APIs.
- Modern web UI, but P/Invoke bridge is more ceremony.

**Option C: Qt6 (C++)**
- Maximum performance, maximum boilerplate.

**Decision**: C# WPF — fastest to iterate, easiest to maintain for a solo dev.

---

## 11. MVP Milestones

1. **V0.1**: Window picker + lock. Single sequential macro with 3 steps. Save/load JSON.
2. **V0.2**: Concurrent repeater mode (Diablo mode). Global stop hotkey.
3. **V0.3**: Hotkey assignment per profile. Full profile CRUD.
4. **V0.4**: Jitter + humanized timing. Mouse click support.
5. **V0.5**: Polished UI, installer, autostart with Windows option.
