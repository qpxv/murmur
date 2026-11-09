import CoreAudio
import Foundation
import MurmurCore
import os

/// Turns the input device up to full volume while recording, so quiet speech and whispers
/// come through, and puts it back afterwards. The original volume is written to disk before
/// boosting, so it is also restored on the next launch after a crash or force quit.
@MainActor
public final class MicGain {
    private struct ChannelVolume: Codable {
        let element: AudioObjectPropertyElement
        let volume: Float32
    }

    private struct Snapshot: Codable {
        let deviceUID: String
        let channels: [ChannelVolume]
    }

    // the main element covers most devices; some only expose per-channel volume
    private static let leftChannel: AudioObjectPropertyElement = 1
    private static let rightChannel: AudioObjectPropertyElement = 2
    private static let candidateElements = [kAudioObjectPropertyElementMain, leftChannel, rightChannel]
    private static let fullVolume: Float32 = 1

    private let defaults: UserDefaults
    private let logger = Logger(subsystem: Log.subsystem, category: "mic-gain")
    private var active: (device: AudioDeviceID, snapshot: Snapshot)?

    public init(defaults: UserDefaults = .standard) {
        self.defaults = defaults
    }

    public func boost(device: AudioDeviceID) {
        guard active == nil, let uid = CoreAudioDevice.uid(of: device) else { return }
        let channels = Self.candidateElements.compactMap { element -> ChannelVolume? in
            guard CoreAudioDevice.isVolumeSettable(device, element: element),
                let volume = CoreAudioDevice.volume(of: device, element: element)
            else { return nil }
            return ChannelVolume(element: element, volume: volume)
        }
        guard !channels.isEmpty else {
            logger.info("input device has no settable volume, skipping boost")
            return
        }
        let snapshot = Snapshot(deviceUID: uid, channels: channels)
        persist(snapshot)
        active = (device, snapshot)
        for channel in channels {
            CoreAudioDevice.setVolume(Self.fullVolume, of: device, element: channel.element)
        }
    }

    public func restore() {
        guard let active else { return }
        apply(active.snapshot, to: active.device)
        self.active = nil
        defaults.removeObject(forKey: AppConstants.Storage.pendingMicRestoreKey)
    }

    /// Restores a boost that the last run never undid (crash, force quit).
    public func restorePendingFromLastRun() {
        guard let data = defaults.data(forKey: AppConstants.Storage.pendingMicRestoreKey) else { return }
        defaults.removeObject(forKey: AppConstants.Storage.pendingMicRestoreKey)
        guard let snapshot = try? JSONDecoder().decode(Snapshot.self, from: data),
            let device = CoreAudioDevice.device(withUID: snapshot.deviceUID)
        else { return }
        apply(snapshot, to: device)
        logger.info("restored mic volume left boosted by the previous run")
    }

    private func apply(_ snapshot: Snapshot, to device: AudioDeviceID) {
        for channel in snapshot.channels {
            CoreAudioDevice.setVolume(channel.volume, of: device, element: channel.element)
        }
    }

    private func persist(_ snapshot: Snapshot) {
        do {
            defaults.set(try JSONEncoder().encode(snapshot), forKey: AppConstants.Storage.pendingMicRestoreKey)
        } catch {
            logger.error("couldn't save mic volume snapshot: \(error.localizedDescription)")
        }
    }
}
