# Murmur

A personal macOS dictation app that lives in the menu bar. Hold **fn**, talk, let go, and the
text appears wherever your cursor is: fast, accurate, and cleaned up by Claude. There is a
[Windows version](#windows) too.

- **Speech to text:** Deepgram `nova-3`, streamed live while you talk (you never see the raw text).
- **Cleanup:** Claude Opus 5.5 fixes misheard words (using context and your vocabulary),
  punctuation, stutters and restarted sentences, in a casual, normal or formal style. It works on
  your speech in pieces while you're still talking, so a 10-minute monologue pastes a few seconds
  after you stop.
- **Never loses speech:** if cleanup fails or is slow, the raw text is pasted. With no text field
  focused, the text goes to the clipboard and a notification tells you.
- **Whispering works:** the mic is turned up to 100% while recording and put back afterwards.
- **App window:** dashboard (words, time saved, speaking speed, streak), searchable history with
  the original text, vocabulary, cleanup options, mic choice, pill position, keys and permissions.

## Using it

| You do | What happens |
|---|---|
| Hold **fn** | Records while held, pastes on release |
| Hold **fn**, press **space** | Locks recording, hands free |
| **fn** (while locked) | Stops and pastes |
| **fn** + any other key | Cancelled (it's a shortcut, not dictation) |
| Tap **fn** briefly | Ignored |

The black pill (top or bottom of the screen, your choice) shows the live voice bar while
recording. Menu bar waveform → **Open Murmur…** (⌘,) opens the app window.

## Set up on a new Mac

Written so you can hand it to Claude Code: *"set up Murmur from the README"*. Steps marked
**(you)** need a person: a click, a password or a key.

1. **Requirements:** macOS 26 or newer, Apple Silicon.
2. **Xcode** from the App Store **(you)**, opened once so it finishes installing. Check:
   `xcode-select -p` prints `/Applications/Xcode.app/Contents/Developer`.
3. **Clone:** `git clone https://github.com/qpxv/murmur.git` (private repo, `gh auth login` first).
4. **Tools:** `brew install swiftlint` (Homebrew from https://brew.sh if missing).
5. **Signing certificate**, so macOS remembers permissions and Keychain access across rebuilds:
   1. **(you)** Xcode → Settings → Accounts → **+** → Apple ID (a free account is enough) →
      Manage Certificates → **+** → **Apple Development**.
   2. Check: `security find-identity -v -p codesigning` lists `Apple Development: …`.
   3. If it shows "0 valid identities", Apple's intermediate certificate is missing. Install it:
      ```sh
      curl -fsSL https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer -o /tmp/AppleWWDRCAG3.cer
      security import /tmp/AppleWWDRCAG3.cer -k ~/Library/Keychains/login.keychain-db
      ```
   4. No Apple ID at hand? `scripts/create-signing-cert.sh` makes a local certificate instead.
      Mic and Accessibility then survive rebuilds, but the Keychain asks again after each build.
6. **Check and build:** `scripts/gates.sh` (must end with "all gates passed"), then
   `scripts/bundle.sh` and `open build/Murmur.app`.
7. **Permissions (you):** allow Microphone and Notifications when asked. For Accessibility, open
   System Settings → Privacy & Security → Accessibility → turn on **Murmur**. The fn hotkey
   starts by itself once it's granted.
8. **fn key (you):** System Settings → Keyboard → "Press 🌐 key to" → **Do Nothing**.
9. **API keys (you):** Open Murmur… → Keys & Permissions:
   - **Deepgram:** from https://console.deepgram.com (new accounts start with free credit).
   - **Claude:** from https://platform.claude.com. Link that Console organization to your Max
     plan so the included monthly API credit pays for cleanup.

   Keys are stored in the macOS Keychain, never in files. Click **Always Allow** when the
   Keychain asks.
10. **Test:** hold fn in any text field and talk.
11. **Stats across devices (optional, you):** in https://console.neon.tech create a project (or
    reuse one), copy its connection string (`postgresql://…`) and paste it under Open Murmur… →
    Keys & Permissions → Stats Sync → **Test Connection**. Murmur creates its table itself.
    Only numbers are shared (time, words, seconds spoken), never what you said. Use the same
    connection string on every device.

## Windows

The same app for Windows (the work laptop), in [`windows/`](windows). **Caps Lock** takes fn's
place, since Windows can't see fn:

| You do | What happens |
|---|---|
| Hold **Caps Lock** | Records while held, pastes on release |
| Hold **Caps Lock**, press **space** | Locks recording, hands free |
| **Caps Lock** (while locked) | Stops and pastes |
| **Caps Lock** + any other key | Cancelled |
| Tap **Caps Lock** briefly | Ignored |

Caps Lock no longer switches to capitals while Murmur runs (Shift still types them). If it was on
when Murmur started, Murmur turns it off once.

1. **Download:** GitHub (signed in, in the browser) → **Releases** → latest `windows-v…` →
   `Murmur.exe`. Builds between releases: **Actions** → latest **windows** run → **Murmur-windows**.
2. **Put it somewhere it stays**, e.g. `%LOCALAPPDATA%\Programs\Murmur\Murmur.exe` (not Downloads),
   so "Start with Windows" keeps pointing at it. For Windows search to find it, put a shortcut to it
   in `%APPDATA%\Microsoft\Windows\Start Menu\Programs`.
   **Updates:** tray icon → **Check for updates…** downloads the new release, swaps the exe and
   restarts Murmur. Keys, settings and history stay.
3. **First start:** double-click it. If SmartScreen warns: **More info → Run anyway**. It's one
   self-contained file, nothing to install; the tray icon (bottom right, maybe behind **^**) is
   Murmur. The window opens on **Keys & Startup**.
4. **Keys:** paste the Deepgram and Claude keys (and the Neon URL for shared stats). They go to the
   Windows Credential Manager, never into a file.
5. **Test:** hold Caps Lock in Notepad and talk. With nothing focused, the text goes to the
   clipboard and a tray message says so.
6. **Something wrong?** Tray icon → **Copy diagnostics**, paste it into the chat. The log is at
   `%LOCALAPPDATA%\Murmur\murmur.log` (timings and errors, never your text or keys).

## Development

| Command | What it does |
|---|---|
| `scripts/gates.sh` | Format check, SwiftLint (strict), release build (warnings are errors), tests |
| `scripts/bundle.sh` | Builds and signs `build/Murmur.app` |
| `scripts/make-icon.sh` | Regenerates `Resources/AppIcon.icns` (then `scripts/make-windows-icon.sh`) |
| `scripts/windows-gates.sh` | Windows app: `dotnet format` check, build (warnings are errors), tests. Runs on the Mac |
| `git tag windows-v0.1.0 && git push origin windows-v0.1.0` | Publishes `Murmur.exe` as a GitHub Release (tag = `<Version>` in `windows/Directory.Build.props`) |
| `/usr/bin/log show --last 10m --predicate 'subsystem == "dev.benwinzer.murmur"'` | App logs (timings, errors; never your text) |

After a rebuild: quit Murmur (menu → Quit), run `scripts/bundle.sh`, then `open build/Murmur.app`.

Architecture, decisions and code rules are in [`CLAUDE.md`](CLAUDE.md).

## Costs

- **Deepgram:** pay as you go, roughly $0.25 per hour of speech.
- **Claude:** cents per dictation, paid from the API credit included with Claude Max.
