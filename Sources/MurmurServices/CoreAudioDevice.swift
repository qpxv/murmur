import CoreAudio
import Foundation

/// Thin wrappers around the Core Audio property API for input devices.
public enum CoreAudioDevice {
    public static func defaultInput() -> AudioDeviceID? {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyDefaultInputDevice,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain
        )
        var device = AudioDeviceID(kAudioObjectUnknown)
        var size = UInt32(MemoryLayout<AudioDeviceID>.size)
        let status = AudioObjectGetPropertyData(
            AudioObjectID(kAudioObjectSystemObject),
            &address,
            0,
            nil,
            &size,
            &device
        )
        guard status == noErr, device != kAudioObjectUnknown else { return nil }
        return device
    }

    static func uid(of device: AudioDeviceID) -> String? {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioDevicePropertyDeviceUID,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain
        )
        var uid: Unmanaged<CFString>?
        var size = UInt32(MemoryLayout<Unmanaged<CFString>?>.size)
        let status = AudioObjectGetPropertyData(device, &address, 0, nil, &size, &uid)
        guard status == noErr, let uid else { return nil }
        return uid.takeRetainedValue() as String
    }

    static func device(withUID uid: String) -> AudioDeviceID? {
        var address = AudioObjectPropertyAddress(
            mSelector: kAudioHardwarePropertyTranslateUIDToDevice,
            mScope: kAudioObjectPropertyScopeGlobal,
            mElement: kAudioObjectPropertyElementMain
        )
        var cfUID = uid as CFString
        var device = AudioDeviceID(kAudioObjectUnknown)
        var size = UInt32(MemoryLayout<AudioDeviceID>.size)
        let status = withUnsafeMutablePointer(to: &cfUID) { uidPointer in
            AudioObjectGetPropertyData(
                AudioObjectID(kAudioObjectSystemObject),
                &address,
                UInt32(MemoryLayout<CFString>.size),
                uidPointer,
                &size,
                &device
            )
        }
        guard status == noErr, device != kAudioObjectUnknown else { return nil }
        return device
    }

    static func isVolumeSettable(_ device: AudioDeviceID, element: AudioObjectPropertyElement) -> Bool {
        var address = volumeAddress(element: element)
        guard AudioObjectHasProperty(device, &address) else { return false }
        var isSettable: DarwinBoolean = false
        let status = AudioObjectIsPropertySettable(device, &address, &isSettable)
        return status == noErr && isSettable.boolValue
    }

    static func volume(of device: AudioDeviceID, element: AudioObjectPropertyElement) -> Float32? {
        var address = volumeAddress(element: element)
        var volume: Float32 = 0
        var size = UInt32(MemoryLayout<Float32>.size)
        let status = AudioObjectGetPropertyData(device, &address, 0, nil, &size, &volume)
        return status == noErr ? volume : nil
    }

    static func setVolume(_ volume: Float32, of device: AudioDeviceID, element: AudioObjectPropertyElement) {
        var address = volumeAddress(element: element)
        var value = volume
        AudioObjectSetPropertyData(device, &address, 0, nil, UInt32(MemoryLayout<Float32>.size), &value)
    }

    private static func volumeAddress(element: AudioObjectPropertyElement) -> AudioObjectPropertyAddress {
        AudioObjectPropertyAddress(
            mSelector: kAudioDevicePropertyVolumeScalar,
            mScope: kAudioDevicePropertyScopeInput,
            mElement: element
        )
    }
}
