# Plan: Murmur for Windows (work laptop)

Status: **all four phases written and pushed (2026-11-11), green in CI, untested on Windows.**
The user agreed to build all phases in one go while at work and test afterwards on the laptop.
Next: the user's test results (Copy diagnostics). The user works from home on Wednesday mornings
(from 06:30) and tests on the laptop there.

Deviations from the plan below, decided while building: the solution is `Murmur.slnx` (the .NET 10
format); the exe is `Murmur.exe`; the Mac's "Keys & Permissions" pane is "Keys & Startup" (Windows
has no permission prompts to list, only the microphone privacy switch, linked from there).

## Context

Murmur works on the Mac (see `README.md` and `CLAUDE.md`). The user wants the same dictation on
their Windows work laptop, as a downloadable `.exe`.

Swift doesn't run on Windows, so this is a second app with the same behaviour. Decided with the user:
- **Caps Lock** is the hotkey. Windows can't see fn (laptop keyboards handle it in firmware): hold
  Caps Lock to talk, Caps Lock + space locks, Caps Lock stops a locked recording.
- **Same repo**, in a `windows/` folder.
- Whether IT allows unsigned `.exe` files and sending speech to Deepgram/Anthropic is the user's to
  check. Build anyway.

Claude can build and unit-test on the Mac, but **can't run the Windows app**. Every runtime check
happens on the user's laptop, using the `.exe` that GitHub builds.

## Stack

- **C# / .NET 10 (LTS) + WPF** with the built-in Fluent theme (`ThemeMode="System"`): native Windows
  look, light/dark following the system.
- **One single-file, self-contained `murmur.exe`** (no .NET install needed). Built by **GitHub Actions
  on a Windows runner** on every push to `main`. A tag `windows-v*` publishes it as a GitHub Release
  you download.
- **Dependencies** (each needed; Windows has no simple built-in replacement):
  - **NAudio**: microphone capture (WASAPI) and the mic-volume boost (endpoint volume).
  - **Anthropic** (official C# SDK): cleanup requests, as the claude-api skill requires. Same
    model, effort and server-side refusal fallback as the Mac (see `shared/config.json` once it exists).
  - Everything else is built in: `ClientWebSocket` (Deepgram), UI Automation (is a text field
    focused), `SendInput` (Ctrl+V), the Windows Credential Manager via P/Invoke (API keys), WinForms
    `NotifyIcon` (tray icon + "copied to clipboard" balloon), `System.Text.Json` (settings, history).
- **Strict gates**: `TreatWarningsAsErrors`, latest .NET analyzers at "All", nullable on,
  `dotnet format --verify-no-changes`, xUnit tests. Same rules as the Swift side: no magic numbers,
  no force-unwrap equivalents (`!`), one source of truth.

## Single source of truth across both apps

What has to stay identical moves into `shared/`, and both apps read it:
- `shared/cleanup-prompt.json`: intro, the three fix rules, the three style texts, the boundary and
  output rules (today in `Sources/MurmurCore/CleanupPrompt.swift`).
- `shared/config.json`: model ID, API version, beta header, effort, max tokens, chunk sizes, context
  words, finish/request timeouts, Deepgram model and URL (today in `AppConstants.swift`).

The Mac app loads both as SwiftPM resources (`bundle.sh` copies the resource bundle into the `.app`);
the Windows app embeds them. A prompt fix reaches both apps, and the existing Swift prompt tests keep
guarding it. Platform-only values (hotkey, audio format, UI sizes) stay in each app's own constants.

## Layout

```
shared/                     cleanup-prompt.json, config.json   (read by both apps)
windows/
  Murmur.sln, Directory.Build.props (strict settings for every project), .editorconfig
  src/Murmur.Core/          net10.0, pure: gesture, state, chunker, assembler, prompt, stats,
                            settings, Deepgram/Anthropic wire types (ports of MurmurCore)
  src/Murmur.Services/      net10.0-windows: keyboard hook, audio, mic boost, Deepgram socket,
                            Claude cleaner, cleanup session, paste/UIA, credentials, history store
  src/Murmur.App/           WPF: tray icon, overlay pill, main window (sidebar panes), controller
  tests/Murmur.Core.Tests/  xUnit, mirrors the Swift tests
.github/workflows/windows.yml   build + test + publish exe (artifact; release on windows-v* tag)
```
The layers point one way, `App → Services → Core`, as on the Mac, enforced by project references.

## Windows-specific behaviour

- **Hotkey:** a low-level keyboard hook (`WH_KEYBOARD_LL`) catches Caps Lock down/up and swallows
  it, so Caps Lock never toggles (Shift still types capitals). Space is swallowed while Caps Lock is
  held during a recording. Any other key held with Caps Lock cancels, and a short tap is ignored:
  the same `HotkeyGesture` rules as the Mac.
- **Pill:** a borderless, transparent, topmost window that never takes focus or clicks
  (`WS_EX_NOACTIVATE`, `WS_EX_TRANSPARENT`, `WS_EX_TOOLWINDOW`). The same black pill, voice bar, lock
  icon and 3×3 positions. It stays on top of other apps and visible during alt-tab.
- **Paste:** UI Automation checks the focused element (Edit/Document, or an editable Value/Text
  pattern). If it's a text input: clipboard + `SendInput` Ctrl+V, then the old clipboard comes back.
  Otherwise the text stays on the clipboard and a tray balloon says so.
- **Keys:** Windows Credential Manager (per user, encrypted by Windows), never in files.
- **History:** a local JSON file in `%LOCALAPPDATA%\Murmur`. Same fields and same dashboard stats.
- **Stats sync:** already built on the Mac (see `CLAUDE.md` → Stats sync). Windows uses the same Neon
  table `murmur_dictations` and the same HTTPS endpoint and SQL (`Sources/MurmurCore/NeonSQL.swift`
  is the reference; port it to Core with the same tests). Each local record gets its sync id on
  first upload and a synced flag. "Neon database URL" goes in the Keys pane and Credential Manager;
  the device name defaults to the PC name. Home shows all devices when synced.
- **Main window:** opened from the tray icon. The same panes: Home, History, Vocabulary, Cleanup,
  Audio, Shortcut & Overlay, Keys.
- **Start with Windows:** a toggle in the app (per-user Run registry key).

## Phases (each one tested by the user on Windows before the next)

1. **Foundation:** install the .NET SDK on the Mac (`brew install --cask dotnet-sdk`; it can build
   WPF for Windows and run the Core tests there), the `shared/` move with the Mac app switched over
   and its gates green, the solution skeleton with strict settings, Core ports + tests, and the
   GitHub Actions workflow. The user gets a first `.exe` that shows the tray icon and the pill.
   That's where we find out whether the work laptop runs it at all, so do this first and fast.
2. **Dictation:** Caps Lock hook, audio, mic boost, Deepgram, paste or clipboard, live pill.
3. **Cleanup:** chunked Claude cleanup with the same prompt and settings.
4. **App window:** sidebar panes, history, stats, keys, start with Windows.

## Remote debugging (the laptop only has the work GitHub CLI)

- Claude can't run anything on the laptop. The loop is: the user downloads the `.exe` in the browser
  (signed in to the private GitHub there; the CLI isn't needed), tries it, and reports back. Claude
  fixes it on the Mac and pushes. GitHub builds a new `.exe` in a few minutes.
- To make reports easy, the app writes its own log file (`%LOCALAPPDATA%\Murmur\murmur.log`: timings,
  errors, audio/clipping counts, never text or keys) and has a **"Copy diagnostics"** button (tray
  menu and Keys pane). It puts the version, Windows build, permission/device state and the last
  log lines on the clipboard, ready to paste into chat. A crash on start also writes the exception
  to that log.
- Cost: private-repo Actions minutes are free up to the monthly allowance (2,000 min on the free
  plan; Windows runners count double). One build is about 5 minutes, so ~10 billed minutes.

## Verification

- Every commit: Mac `scripts/gates.sh` green (the shared files touch it), Windows `dotnet build`
  (warnings are errors), `dotnet format --verify-no-changes`, and `dotnet test` locally and in CI.
  Core tests mirror the Swift ones (gesture timing, chunk edges, assembly order, prompt building,
  stats across midnight/month in the local zone).
- Per phase on the laptop: download the `.exe` from the Actions run or the Release, start it (if
  SmartScreen warns: "More info → Run anyway"), then test Caps Lock hold/lock/cancel in Notepad,
  Teams/Outlook and a browser, the clipboard fallback with nothing focused, whispering, and a
  2-minute dictation for speed.
- `README.md` gets a "Windows" section (download, first start, keys), and `CLAUDE.md` gets the
  Windows decisions. Mark this plan's status as you go.
