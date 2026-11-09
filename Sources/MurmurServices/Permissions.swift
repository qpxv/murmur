import AVFoundation
import AppKit
import ApplicationServices

/// Microphone and accessibility permission checks, plus links into System Settings.
public enum Permissions {
    private static let microphoneSettingsURL =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_Microphone"
    private static let accessibilitySettingsURL =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility"
    private static let keyboardSettingsURL = "x-apple.systempreferences:com.apple.Keyboard-Settings.extension"
    // "Press 🌐 key to": 0 means "Do nothing", anything else fights with the fn hotkey
    private static let fnUsageDomain = "com.apple.HIToolbox"
    private static let fnUsageKey = "AppleFnUsageType"

    public static var isMicrophoneAuthorized: Bool {
        AVCaptureDevice.authorizationStatus(for: .audio) == .authorized
    }

    /// Asks once if undecided; returns the final answer.
    public static func requestMicrophone() async -> Bool {
        switch AVCaptureDevice.authorizationStatus(for: .audio) {
        case .authorized: true
        case .notDetermined: await AVCaptureDevice.requestAccess(for: .audio)
        default: false
        }
    }

    /// Needed for the fn hotkey (event tap) and for pasting into other apps.
    public static var isAccessibilityTrusted: Bool {
        AXIsProcessTrusted()
    }

    /// Shows the system accessibility prompt if the app isn't trusted yet.
    public static func promptForAccessibility() {
        let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
        AXIsProcessTrustedWithOptions(options)
    }

    public static var isFnKeyFree: Bool {
        UserDefaults(suiteName: fnUsageDomain)?.integer(forKey: fnUsageKey) == 0
    }

    @MainActor public static func openMicrophoneSettings() { open(microphoneSettingsURL) }
    @MainActor public static func openAccessibilitySettings() { open(accessibilitySettingsURL) }
    @MainActor public static func openKeyboardSettings() { open(keyboardSettingsURL) }

    @MainActor private static func open(_ string: String) {
        guard let url = URL(string: string) else { return }
        NSWorkspace.shared.open(url)
    }
}
