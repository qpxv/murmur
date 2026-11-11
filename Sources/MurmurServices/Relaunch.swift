import AppKit
import os

/// Quits Murmur and starts it again (e.g. after `bundle.sh` rebuilt the app in place).
public enum Relaunch {
    // a tiny shell outlives us: it waits until this process is gone, then opens the app again,
    // so two copies never run at once (two event taps would both see the dictation key)
    private static let script = "while /bin/kill -0 \"$1\" 2>/dev/null; do /bin/sleep 0.1; done; /usr/bin/open \"$2\""

    @MainActor
    public static func now() {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/bin/sh")
        process.arguments = [
            "-c", script, "relaunch",
            String(ProcessInfo.processInfo.processIdentifier),
            Bundle.main.bundlePath,
        ]
        do {
            try process.run()
        } catch {
            Logger(subsystem: Log.subsystem, category: "app")
                .error("restart failed: \(error.localizedDescription, privacy: .public)")
            return
        }
        NSApplication.shared.terminate(nil)
    }
}
