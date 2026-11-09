import Foundation
import UserNotifications
import os

/// Posts macOS notifications (e.g. "copied to clipboard" when there was nowhere to paste).
public enum Notifier {
    private static let logger = Logger(subsystem: Log.subsystem, category: "notify")

    public static func requestAuthorization() async {
        do {
            _ = try await UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound])
        } catch {
            logger.error("notification authorization failed: \(error.localizedDescription)")
        }
    }

    public static func post(title: String, body: String) async {
        let content = UNMutableNotificationContent()
        content.title = title
        content.body = body
        let request = UNNotificationRequest(identifier: UUID().uuidString, content: content, trigger: nil)
        do {
            try await UNUserNotificationCenter.current().add(request)
        } catch {
            logger.error("posting notification failed: \(error.localizedDescription)")
        }
    }
}
