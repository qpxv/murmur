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
    public static func insert(_ text: String) async -> Outcome {
        let pasteboard = NSPasteboard.general
        guard await isTextInputFocusedInFrontApp() else {
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

    /// Chrome, Electron apps and other Chromium browsers expose no web content to accessibility
    /// until a client asks for it, so a focused page input looks like no text input at all.
    /// The first time, the tree takes a moment to build, hence the one retry.
    private static func isTextInputFocusedInFrontApp() async -> Bool {
        if isTextInputFocused(in: AXUIElementCreateSystemWide()) { return true }
        guard let app = NSWorkspace.shared.frontmostApplication else { return false }
        let element = AXUIElementCreateApplication(app.processIdentifier)
        enableAccessibility(of: element, appID: app.bundleIdentifier ?? "unknown")
        // chromium reports an error for both switches even when they take effect, so always look again
        try? await Task.sleep(for: .seconds(AppConstants.Paste.accessibilityWarmupDelay))
        return isTextInputFocused(in: element)
    }

    /// electron knows AXManualAccessibility, chrome AXEnhancedUserInterface (what voiceover sets)
    private static func enableAccessibility(of app: AXUIElement, appID: String) {
        let manual = AXUIElementSetAttributeValue(app, "AXManualAccessibility" as CFString, kCFBooleanTrue)
        let enhanced = AXUIElementSetAttributeValue(app, "AXEnhancedUserInterface" as CFString, kCFBooleanTrue)
        logger.info(
            "accessibility on in \(appID, privacy: .public): manual \(manual.rawValue), enhanced \(enhanced.rawValue)"
        )
    }

    private static func isTextInputFocused(in source: AXUIElement) -> Bool {
        var focused: CFTypeRef?
        let status = AXUIElementCopyAttributeValue(source, kAXFocusedUIElementAttribute as CFString, &focused)
        guard status == .success, let focused, CFGetTypeID(focused) == AXUIElementGetTypeID() else {
            logger.info("focused element unreadable: \(status.rawValue)")
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
        let roleName = role as? String ?? "none"
        logger.info(
            "focused \(roleName, privacy: .public): settable \(isSettable.boolValue), range \(rangeStatus.rawValue)"
        )

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
