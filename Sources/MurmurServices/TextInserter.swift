import AppKit
import ApplicationServices
import Carbon.HIToolbox
import MurmurCore
import os

/// Delivers finished text to wherever the user is right now.
@MainActor
public enum TextInserter {
    public enum Outcome: Sendable {
        case pasted
        case copiedToClipboard
    }

    private static let logger = Logger(subsystem: Log.subsystem, category: "insert")

    /// Pastes into the focused text input and restores the previous clipboard afterwards.
    /// With no text input focused, the text is left on the clipboard instead.
    public static func insert(_ text: String) -> Outcome {
        let pasteboard = NSPasteboard.general
        guard isTextInputFocused() else {
            pasteboard.clearContents()
            pasteboard.setString(text, forType: .string)
            return .copiedToClipboard
        }

        let saved = savedItems(of: pasteboard)
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        let ourChange = pasteboard.changeCount
        postCommandV()

        Task {
            try? await Task.sleep(for: .seconds(AppConstants.Paste.clipboardRestoreDelay))
            // only restore if nothing else touched the clipboard in the meantime
            guard pasteboard.changeCount == ourChange else { return }
            pasteboard.clearContents()
            if !saved.isEmpty {
                pasteboard.writeObjects(saved)
            }
        }
        return .pasted
    }

    private static func isTextInputFocused() -> Bool {
        let systemWide = AXUIElementCreateSystemWide()
        var focused: CFTypeRef?
        let status = AXUIElementCopyAttributeValue(systemWide, kAXFocusedUIElementAttribute as CFString, &focused)
        guard status == .success, let focused, CFGetTypeID(focused) == AXUIElementGetTypeID() else {
            return false
        }
        // safe: the type id was checked right above
        let element = unsafeDowncast(focused, to: AXUIElement.self)

        var role: CFTypeRef?
        AXUIElementCopyAttributeValue(element, kAXRoleAttribute as CFString, &role)
        var isSettable: DarwinBoolean = false
        AXUIElementIsAttributeSettable(element, kAXValueAttribute as CFString, &isSettable)
        var range: CFTypeRef?
        let rangeStatus = AXUIElementCopyAttributeValue(element, kAXSelectedTextRangeAttribute as CFString, &range)

        return TextInputRule.isTextInput(
            role: role as? String,
            isValueSettable: isSettable.boolValue,
            hasSelectedTextRange: rangeStatus == .success
        )
    }

    private static func savedItems(of pasteboard: NSPasteboard) -> [NSPasteboardItem] {
        (pasteboard.pasteboardItems ?? []).map { item in
            let copy = NSPasteboardItem()
            for type in item.types {
                if let data = item.data(forType: type) {
                    copy.setData(data, forType: type)
                }
            }
            return copy
        }
    }

    private static func postCommandV() {
        let source = CGEventSource(stateID: .combinedSessionState)
        let keyCode = CGKeyCode(kVK_ANSI_V)
        let keyDown = CGEvent(keyboardEventSource: source, virtualKey: keyCode, keyDown: true)
        let keyUp = CGEvent(keyboardEventSource: source, virtualKey: keyCode, keyDown: false)
        // set explicitly so a still-held fn key doesn't turn this into fn+cmd+v
        keyDown?.flags = .maskCommand
        keyUp?.flags = .maskCommand
        keyDown?.post(tap: .cgSessionEventTap)
        keyUp?.post(tap: .cgSessionEventTap)
        if keyDown == nil || keyUp == nil {
            logger.error("couldn't create paste key events")
        }
    }
}
