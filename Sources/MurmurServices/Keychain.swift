import Foundation
import Security
import os

/// Stores secrets (API keys) as generic passwords in the login keychain.
public enum Keychain {
    private static let service = Log.subsystem
    private static let logger = Logger(subsystem: Log.subsystem, category: "keychain")

    public static func read(account: String) -> String? {
        let query: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
            kSecReturnData as String: true,
            kSecMatchLimit as String: kSecMatchLimitOne,
        ]
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess, let data = result as? Data else {
            if status != errSecItemNotFound {
                logger.error("keychain read failed: \(status)")
            }
            return nil
        }
        return String(data: data, encoding: .utf8)
    }

    public static func contains(account: String) -> Bool {
        read(account: account) != nil
    }

    /// Saves `value`, or deletes the entry when `value` is empty.
    @discardableResult
    public static func write(_ value: String, account: String) -> Bool {
        let match: [String: Any] = [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account,
        ]
        SecItemDelete(match as CFDictionary)
        guard !value.isEmpty else { return true }

        var item = match
        item[kSecValueData as String] = Data(value.utf8)
        let status = SecItemAdd(item as CFDictionary, nil)
        if status != errSecSuccess {
            logger.error("keychain write failed: \(status)")
        }
        return status == errSecSuccess
    }
}
