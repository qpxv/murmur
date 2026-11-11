# Murmur

Personal macOS menu bar dictation app (a Superwhisper replacement). Speech streams to
Deepgram, finished chunks get cleaned up by Claude while still speaking, and the result is
pasted where the user is when they stop. The user does not read or write code here: Claude
owns architecture, quality and maintenance.

**Windows version:** in [`windows/`](windows), built per [`docs/windows-plan.md`](docs/windows-plan.md)
(all four phases written, being tested on the laptop). See "Windows" below.

## Behaviour

- **fn hold** = push to talk, release stops. **fn + space** (while fn is held) locks
  recording; the next fn press stops it. The space is swallowed, never typed.
- An unlocked fn press shorter than `AppConstants.Hotkey.minimumHold` is discarded, and so is
  an unlocked recording where another key is pressed with fn (fn + arrow etc. are shortcuts).
- While recording, the user can click around, switch apps, use Mission Control: nothing is
  tied to the app that was focused at start. The overlay never takes focus.
- On stop: if a text input is focused *at that moment*, paste into it (clipboard restored
  afterwards). Otherwise leave the text on the clipboard and post a notification saying so.
  Text is never lost: after pasting, the field's character count is compared
  (`TextInputRule.didPasteLand`); if it didn't change, the text stays on the clipboard too.
- Chromium apps (Chrome, Electron) hide web content from accessibility until asked, so a
  focused page input reads as "no input" (system-wide focus returns -25212). When the first
  check fails, `TextInserter` sets `AXManualAccessibility` (Electron) and
  `AXEnhancedUserInterface` (Chrome) on the front app, waits `Paste.accessibilityWarmupDelay`
  and asks the app element for its focus. Chrome returns -25205/-25208 for these setters even
  though they work, so the result is logged, never trusted. Chrome keeps it on until it quits.

## Decisions (keep in sync, no aspirational rules)

- **Swift 6 + SwiftUI/AppKit, macOS 26+ only.** Swift Package, no Xcode project.
- **Speech-to-text: Deepgram streaming** (WebSocket). The Claude API takes no audio.
- **Cleanup: `claude-opus-5-5`, effort `low`**, `fallbacks: "default"`, via the Messages API
  over `URLSession` (no official Swift SDK). Paid from the Max plan's included API credit.
  Flow: `DeepgramStream.results` -> `CleanupSession` -> `Chunker` closes chunks at pauses
  (40+ words) or at 150 -> a closed chunk waits for the next segment, then one `ClaudeCleaner`
  request per chunk with `<previous>` and `<next>` context (so sentences split at a chunk edge
  still join up), in parallel while speaking ->
  `TranscriptAssembler` joins in order. After stop, at most `Cleanup.finishTimeout` is waited;
  any chunk not cleaned by then (or failed/refused) is pasted raw. Speech is never lost.
  Toggle: `Settings.isCleanupEnabled`. Timing per request is logged (`category == "cleanup"`).
  `Settings.cleanup` (`CleanupOptions`): three fix toggles + style casual/normal/formal; the
  system prompt is built from it by `CleanupPrompt.system(for:)`. Nothing to do (all fixes off,
  normal style) means no Claude call at all.
- **Mic selection:** only switch the engine's device when the user picked a specific mic.
  Forcing even the default device onto `AVAudioEngine` left it silent once.
- **Open at login:** `SMAppService.mainApp` (`LoginItem`), toggled from the menu bar and Keys &
  Permissions. macOS holds the state (the user can change it in Login Items), never `Settings`.
  Off by default. It points at `build/Murmur.app`, which `bundle.sh` rebuilds at the same path.
- **Signing** (`scripts/bundle.sh`): Apple Development cert if present, else the self-signed
  "Murmur Local Signing", else ad hoc. With the self-signed cert, Keychain re-asks after every
  build (partition is the binary's cdhash); an Apple Development cert (team id) fixes that.
- **No third-party Swift dependencies.** Apple frameworks only.
- API keys live in the Keychain, never in UserDefaults, files or logs.
- Overlay pill position: any cell of a 3x3 grid (`OverlayPosition`), default bottom center.
  Black dynamic-island style pill; 1pt edge (0.5pt vanishes on 1x displays).
- **App window** (`MainWindow`, a `Window` scene, suppressed at launch): sidebar panes Home
  (stats), History, Vocabulary, Cleanup, Audio, Shortcut & Overlay, Keys & Permissions. Opened
  from the menu bar ("Open Murmur…"). While open the app is `.regular` (Dock, ⌘-Tab), back to
  `.accessory` when closed. There is no separate Settings window.
- **History: SwiftData**, `DictationRecord` in Application Support/<bundle id>/history.store,
  written by `HistoryStore`, read in views with `@Query`. Falls back to an in-memory store if the
  file can't open (dictation must never break). `Settings.isHistoryEnabled` off keeps counts
  only, no text. Dashboard numbers come from `DictationStats` (Core, tested) in the local calendar.
- **Stats sync (optional): the user's Neon database**, via Neon's HTTPS SQL endpoint
  (`POST https://<host>/sql`, `Neon-Connection-String` header; format from Neon's driver source),
  no Postgres driver. Table `murmur_dictations` (id, device, dictated_at, word_count,
  speaking_seconds), created by the app. **Numbers only, never text.** The database URL lives only
  in the Keychain (`neon-database-url`), never in the repo or logs. Each record gets its
  `syncID` on first upload (never a migration default) and inserts are `on conflict do nothing`,
  so retries can't double-count. `StatsSyncController` uploads after each dictation and at launch,
  fetches when Home opens; no polling. Home shows all devices when synced, else this Mac only.
  The Windows app uses the same table (see `docs/windows-plan.md`).

## Layout

```
shared/                  config.json, cleanup-prompt.json: read by the mac and the windows app
windows/                 the windows app (see "Windows")
Sources/MurmurCore/      pure logic + wire types. Foundation/CoreGraphics only. unit-tested.
Sources/MurmurServices/  system integrations (audio, network, keychain, paste, hotkey, storage)
Sources/MurmurApp/       SwiftUI + AppKit glue: menu bar, overlay, main window, controller
Tests/MurmurCoreTests/   Swift Testing
Resources/               Info.plist (single source for name, bundle id, version), AppIcon.icns
scripts/                 gates.sh, bundle.sh, make-icon.sh, create-signing-cert.sh
```

- Dependencies point one way: App -> Services -> Core. Enforced by SPM targets.
- Logic that can be pure goes in Core with tests; Services/App stay thin.
- Each service sits behind a small protocol only once a second implementation or a test
  double actually needs it.

## Single source of truth

- What both apps must agree on lives in `shared/`: `config.json` (model, API version, beta, effort,
  max tokens, chunk sizes, context words, timeouts, Deepgram URL/model/language) and
  `cleanup-prompt.json` (every prompt text). The Mac bundles them as a SwiftPM resource through the
  `Sources/MurmurCore/Shared` symlink (`SharedFiles.swift`; `bundle.sh` copies the resource bundle
  into the app); Windows embeds them. `AppConstants` re-exports them. Edit the JSON, never a copy.
- Numbers, timings, model IDs, URLs: `MurmurCore/AppConstants.swift`.
- Visual tokens (spacing, radii, durations): `MurmurApp/Theme.swift` (added with the overlay).
- User settings: one `Settings` struct, persisted by `SettingsStore`. New fields decode with a
  default (`decodeIfPresent`), so saved settings survive upgrades.
- App name / bundle id / version: `Resources/Info.plist`; scripts read it via PlistBuddy, Swift
  reads `Bundle.main`.

## Commands

- `scripts/gates.sh`: swift format lint, swiftlint --strict, release build, tests. Must be
  green before anything counts as done.
- `scripts/bundle.sh`: builds and signs `build/Murmur.app`. `open build/Murmur.app` to run.
- `scripts/make-icon.sh`: regenerates `Resources/AppIcon.icns`.
- Logs: `/usr/bin/log show --last 10m --predicate 'subsystem == "dev.benwinzer.murmur"'` (the
  full path: in zsh `log` is a builtin). Each recording logs its audio block and clipped count.
- History DB (raw vs cleaned text, for checking cleanup quality): `sqlite3 ~/Library/Application\
  Support/dev.benwinzer.murmur/history.store "SELECT ZRAWTEXT, ZTEXT FROM ZDICTATIONRECORD ORDER BY
  ZDATE DESC LIMIT 1"`. Only read it when the user asks you to look at a dictation.
- New Mac setup: follow `README.md` → "Set up on a new Mac".

## Windows

- **C# / .NET 10 + WPF** (Fluent theme, `ThemeMode="System"`), solution `windows/Murmur.slnx`:
  `Murmur.Core` (net10.0, pure, tested), `Murmur.Services` (Windows integrations), `Murmur.App`
  (WPF), `tests/Murmur.Core.Tests` (xUnit v3). Layers point one way, enforced by project references.
- **Strict:** `Directory.Build.props` (warnings are errors, analyzers `latest-all`, nullable),
  `.editorconfig` (the few rules turned off each say why), package versions only in
  `Directory.Packages.props`. Dependencies: NAudio (`NAudio.Wasapi`), Anthropic (official SDK).
- **Builds:** `scripts/windows-gates.sh` on the Mac (SDK in `~/.dotnet`, user-local). GitHub
  Actions (`.github/workflows/windows.yml`) runs the gates on a Windows runner and publishes one
  self-contained `Murmur.exe` per push; a `windows-v*` tag matching `<Version>` makes a Release.
  `.gitattributes` forces LF (the runner would check out CRLF and `dotnet format` fails).
- **Hotkey:** Caps Lock through a low-level keyboard hook on the UI thread (`KeyboardHook`). The
  hook only decides and swallows; work is dispatched after it returns (Windows drops slow hooks).
  Caps Lock is always swallowed; injected keys (our Ctrl+V) pass through.
- **Audio:** `WasapiRecorderBuilder` (NAudio 3; `WasapiCapture` is obsolete) in the device's mix
  format, converted to 16 kHz mono by NAudio's WDL resampler, not the engine's auto-convert.
- **Paste:** UI Automation always off the UI thread (it deadlocks on our own windows otherwise).
  Input = Edit/Document, a writable value pattern, or a text pattern (terminals), unless the value
  is read-only (web pages). Logs the focused element's control type and class, never its text.
- **Cleanup:** one `ClaudeCleaner` (SDK client) is kept across dictations while key and options
  stay the same, so its connection stays warm. `StopReason.Raw()` is the wire value.
- **Storage:** `%LOCALAPPDATA%\Murmur`: `settings.json`, `history.json` (an unreadable one is moved
  aside, never overwritten), `murmur.log` (trimmed at 1 MB), `mic-restore.json`. Secrets in the
  Credential Manager (`Murmur/...` targets). Start with Windows = HKCU Run key.
- **Remote debugging:** Claude can't run it. The user sends **Copy diagnostics** (tray or Keys).

## Toolchain

- Xcode is installed and selected (`xcode-select -p`). Everything builds from the terminal via
  SwiftPM; the Xcode app never needs to be open.

## Code rules

- No force unwraps/casts/tries, no magic numbers outside `AppConstants`/`Theme` (tests excepted).
- Errors: services return or throw typed errors; the controller turns them into a
  `DictationState.failed(DictationError)` whose `message` the overlay shows. Log with `os.Logger`
  (`Log.subsystem`), never log transcripts or keys.
- Idle = zero CPU: no timers, no polling, no animation unless recording.
- Comments only for the why, lowercase and short.
