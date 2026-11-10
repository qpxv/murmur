import OSLog
import ServiceManagement

/// Opening Murmur at login. macOS holds the state (the user can change it in System Settings),
/// so nothing is stored in `Settings`.
public enum LoginItem {
    private static let logger = Logger(subsystem: Log.subsystem, category: "loginItem")

    public static var isEnabled: Bool {
        SMAppService.mainApp.status == .enabled
    }

    /// Registered, but switched off by the user in System Settings > Login Items.
    public static var needsApproval: Bool {
        SMAppService.mainApp.status == .requiresApproval
    }

    public static func setEnabled(_ isEnabled: Bool) {
        do {
            if isEnabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
        } catch {
            // callers re-read isEnabled, so a failure just shows as unchanged
            logger.error("login item change failed: \(error.localizedDescription, privacy: .public)")
        }
    }

    public static func openSettings() {
        SMAppService.openSystemSettingsLoginItems()
    }
}
