import Foundation

/// Shared unified-logging subsystem, so `log stream --predicate 'subsystem == ...'` shows all app logs.
public enum Log {
    public static let subsystem = Bundle.main.bundleIdentifier ?? "Murmur"
}
