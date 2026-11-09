# Murmur

Personal macOS menu bar dictation app (a Superwhisper replacement). Speech streams to
Deepgram, finished chunks get cleaned up by Claude while still speaking, and the result is
pasted where the user is when they stop. The user does not read or write code here: Claude
owns architecture, quality and maintenance.

**Windows version:** approved plan in [`docs/windows-plan.md`](docs/windows-plan.md), not started
yet. Follow it when the user asks for the Windows version.

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
