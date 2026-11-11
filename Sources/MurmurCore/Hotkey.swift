import CoreGraphics
import Foundation

/// The one key that dictates. Any single key the user recorded; fn by default.
/// Stored as the macOS virtual key code, the same number the event tap reports.
public struct Hotkey: Codable, Hashable, Sendable {
    public let keyCode: UInt16

    public init(keyCode: UInt16) {
        self.keyCode = keyCode
    }

    public static let function = Self(keyCode: KeyCode.function)

    /// Set when the key is a modifier: macOS reports it as a flags change, not as key down/up.
    public var modifier: Modifier? {
        Modifier.all[keyCode]
    }

    /// Whether the key is taken over completely (its own events and its flag never reach other apps).
    /// fn is the exception: fn + arrows, fn + delete and the F-keys of the laptop keyboard need it.
    public var isTakenOver: Bool {
        self != .function
    }

    /// `flags` of another key's event with this key's modifier removed, so that while it is held for
    /// dictation, other keys arrive plain (right ⌘ + c types "c"). The generic flag stays when the
    /// other side's key (left ⌘) is also down.
    public func removingModifier(from flags: CGEventFlags) -> CGEventFlags {
        guard isTakenOver, let modifier else { return flags }
        var result = flags.subtracting(modifier.device)
        if !flags.contains(modifier.otherSide) {
            result.remove(modifier.generic)
        }
        return result
    }

    /// macOS virtual key codes (Carbon's `kVK_*`, which Core can't import).
    public enum KeyCode {
        public static let space: UInt16 = 0x31
        public static let escape: UInt16 = 0x35
        public static let rightCommand: UInt16 = 0x36
        public static let command: UInt16 = 0x37
        public static let shift: UInt16 = 0x38
        public static let capsLock: UInt16 = 0x39
        public static let option: UInt16 = 0x3A
        public static let control: UInt16 = 0x3B
        public static let rightShift: UInt16 = 0x3C
        public static let rightOption: UInt16 = 0x3D
        public static let rightControl: UInt16 = 0x3E
        public static let function: UInt16 = 0x3F
    }

    /// The flags a modifier key sets on events: the generic one (any ⌘) and the ones that say
    /// which physical key (left or right ⌘) is down.
    public struct Modifier: Sendable {
        public let generic: CGEventFlags
        public let device: CGEventFlags
        public let otherSide: CGEventFlags

        // the device-dependent bits from IOKit's NX_DEVICE*KEYMASK
        private static let leftControlBit: UInt64 = 0x0000_0001
        private static let leftShiftBit: UInt64 = 0x0000_0002
        private static let rightShiftBit: UInt64 = 0x0000_0004
        private static let leftCommandBit: UInt64 = 0x0000_0008
        private static let rightCommandBit: UInt64 = 0x0000_0010
        private static let leftOptionBit: UInt64 = 0x0000_0020
        private static let rightOptionBit: UInt64 = 0x0000_0040
        private static let rightControlBit: UInt64 = 0x0000_2000
        private static let leftControl = CGEventFlags(rawValue: leftControlBit)
        private static let leftShift = CGEventFlags(rawValue: leftShiftBit)
        private static let rightShift = CGEventFlags(rawValue: rightShiftBit)
        private static let leftCommand = CGEventFlags(rawValue: leftCommandBit)
        private static let rightCommand = CGEventFlags(rawValue: rightCommandBit)
        private static let leftOption = CGEventFlags(rawValue: leftOptionBit)
        private static let rightOption = CGEventFlags(rawValue: rightOptionBit)
        private static let rightControl = CGEventFlags(rawValue: rightControlBit)

        static let all: [UInt16: Self] = [
            KeyCode.command: Self(generic: .maskCommand, device: leftCommand, otherSide: rightCommand),
            KeyCode.rightCommand: Self(generic: .maskCommand, device: rightCommand, otherSide: leftCommand),
            KeyCode.shift: Self(generic: .maskShift, device: leftShift, otherSide: rightShift),
            KeyCode.rightShift: Self(generic: .maskShift, device: rightShift, otherSide: leftShift),
            KeyCode.option: Self(generic: .maskAlternate, device: leftOption, otherSide: rightOption),
            KeyCode.rightOption: Self(generic: .maskAlternate, device: rightOption, otherSide: leftOption),
            KeyCode.control: Self(generic: .maskControl, device: leftControl, otherSide: rightControl),
            KeyCode.rightControl: Self(generic: .maskControl, device: rightControl, otherSide: leftControl),
            // fn has a single key, so its generic flag is the one to watch
            KeyCode.function: Self(generic: .maskSecondaryFn, device: .maskSecondaryFn, otherSide: []),
        ]

        /// Whether this modifier's key is down, read from any event's flags.
        public func isDown(in flags: CGEventFlags) -> Bool {
            flags.contains(device)
        }
    }
}
