import Foundation
import MurmurCore
import Observation
import os

/// Loads and saves `Settings`. Every change is written right away.
@MainActor
@Observable
public final class SettingsStore {
    public var settings: Settings {
        didSet { save() }
    }

    @ObservationIgnored private let defaults: UserDefaults
    @ObservationIgnored private let logger = Logger(subsystem: Log.subsystem, category: "settings")

    public init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
        self.settings = Self.load(from: defaults)
    }

    private static func load(from defaults: UserDefaults) -> Settings {
        guard let data = defaults.data(forKey: AppConstants.Storage.settingsKey) else {
            return .defaults
        }
        do {
            return try JSONDecoder().decode(Settings.self, from: data)
        } catch {
            Logger(subsystem: Log.subsystem, category: "settings")
                .error("settings unreadable, using defaults: \(error.localizedDescription)")
            return .defaults
        }
    }

    private func save() {
        do {
            let data = try JSONEncoder().encode(settings)
            defaults.set(data, forKey: AppConstants.Storage.settingsKey)
        } catch {
            logger.error("saving settings failed: \(error.localizedDescription)")
        }
    }
}
