import Carbon.HIToolbox
import Foundation
import MurmurCore

/// What a key is called on screen ("Right ⌘", "F5", "§").
public enum KeyName {
    // a key types at most a few utf-16 units (an emoji or a combined character)
    private static let maxCharacters = 4
    private static let special: [Int: String] = [
        kVK_Function: "fn",
        kVK_Command: "Left ⌘",
        kVK_RightCommand: "Right ⌘",
        kVK_Option: "Left ⌥",
        kVK_RightOption: "Right ⌥",
        kVK_Control: "Left ⌃",
        kVK_RightControl: "Right ⌃",
        kVK_Shift: "Left ⇧",
        kVK_RightShift: "Right ⇧",
        kVK_Return: "Return",
        kVK_Tab: "Tab",
        kVK_Delete: "Delete",
        kVK_ForwardDelete: "Forward Delete",
        kVK_Home: "Home",
        kVK_End: "End",
        kVK_PageUp: "Page Up",
        kVK_PageDown: "Page Down",
        kVK_LeftArrow: "←",
        kVK_RightArrow: "→",
        kVK_UpArrow: "↑",
        kVK_DownArrow: "↓",
        kVK_Help: "Help",
        kVK_ANSI_KeypadEnter: "Enter",
        kVK_ANSI_KeypadClear: "Clear",
        kVK_F1: "F1", kVK_F2: "F2", kVK_F3: "F3", kVK_F4: "F4", kVK_F5: "F5", kVK_F6: "F6",
        kVK_F7: "F7", kVK_F8: "F8", kVK_F9: "F9", kVK_F10: "F10", kVK_F11: "F11", kVK_F12: "F12",
        kVK_F13: "F13", kVK_F14: "F14", kVK_F15: "F15", kVK_F16: "F16", kVK_F17: "F17", kVK_F18: "F18",
        kVK_F19: "F19", kVK_F20: "F20",
    ]

    public static func of(_ hotkey: Hotkey) -> String {
        let code = Int(hotkey.keyCode)
        return special[code] ?? typedCharacter(keyCode: hotkey.keyCode) ?? "Key \(code)"
    }

    // what the key types on the current keyboard layout, so a german § or ö shows as itself
    private static func typedCharacter(keyCode: UInt16) -> String? {
        guard
            let source = TISCopyCurrentKeyboardLayoutInputSource()?.takeRetainedValue(),
            let pointer = TISGetInputSourceProperty(source, kTISPropertyUnicodeKeyLayoutData)
        else { return nil }
        let layoutData = Unmanaged<CFData>.fromOpaque(pointer).takeUnretainedValue() as Data
        var deadKeyState: UInt32 = 0
        var characters = [UniChar](repeating: 0, count: maxCharacters)
        var length = 0
        let status = layoutData.withUnsafeBytes { buffer -> OSStatus in
            guard let layout = buffer.baseAddress?.assumingMemoryBound(to: UCKeyboardLayout.self) else {
                return OSStatus(paramErr)
            }
            return UCKeyTranslate(
                layout,
                keyCode,
                UInt16(kUCKeyActionDisplay),
                0,
                UInt32(LMGetKbdType()),
                OptionBits(kUCKeyTranslateNoDeadKeysBit),
                &deadKeyState,
                characters.count,
                &length,
                &characters
            )
        }
        guard status == noErr, length > 0 else { return nil }
        let text = String(utf16CodeUnits: characters, count: length).trimmingCharacters(in: .whitespacesAndNewlines)
        return text.isEmpty ? nil : text.uppercased()
    }
}
