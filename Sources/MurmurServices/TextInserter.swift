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
    /// With no text input focused, or when the paste visibly didn't land, the text is left on
    /// the clipboard instead, so it is never lost.
    public static func insert(_ text: String) async -> Outcome {
        let pasteboard = NSPasteboard.general
        guard let input = await focusedTextInputInFrontApp() else {
            leaveOnClipboard(text)
            return .copiedToClipboard
        }

        let saved = savedItems(of: pasteboard)
        let lengthBefore = characterCount(of: input)
        pasteboard.clearContents()
        pasteboard.setString(text, forType: .string)
        let ourChange = pasteboard.changeCount
        postCommandV()

        try? await Task.sleep(for: .seconds(AppConstants.Paste.clipboardRestoreDelay))
        let lengthAfter = characterCount(of: input)
        guard TextInputRule.didPasteLand(lengthBefore: lengthBefore, lengthAfter: lengthAfter) else {
            logger.error("paste didn't land: field length stayed \(lengthAfter ?? -1), leaving text on the clipboard")
            leaveOnClipboard(text)
            return .copiedToClipboard
        }
        // only restore if nothing else touched the clipboard in the meantime
        if pasteboard.changeCount == ourChange {
            pasteboard.clearContents()
            if !saved.isEmpty {
                pasteboard.writeObjects(saved)
            }
        }
        return .pasted
    }

    private static func leaveOnClipboard(_ text: String) {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(text, forType: .string)
    }

    private static func characterCount(of element: AXUIElement) -> Int? {
        var count: CFTypeRef?
        let status = AXUIElementCopyAttributeValue(element, kAXNumberOfCharactersAttribute as CFString, &count)
        guard status == .success else { return nil }
        return count as? Int
    }

    /// Chrome, Electron apps and other Chromium browsers expose no web content to accessibility
    /// until a client asks for it, so a focused page input looks like no text input at all.
    /// The first time, the tree takes a moment to build, hence the one retry.
    private static func focusedTextInputInFrontApp() async -> AXUIElement? {
        if let input = focusedTextInput(in: AXUIElementCreateSystemWide()) { return input }
        guard let app = NSWorkspace.shared.frontmostApplication else { return nil }
        let element = AXUIElementCreateApplication(app.processIdentifier)
        enableAccessibility(of: element, appID: app.bundleIdentifier ?? "unknown")
        // chromium reports an error for both switches even when they take effect, so always look again
        try? await Task.sleep(for: .seconds(AppConstants.Paste.accessibilityWarmupDelay))
        return focusedTextInput(in: element)
    }

    /// electron knows AXManualAccessibility, chrome AXEnhancedUserInterface (what voiceover sets)
    private static func enableAccessibility(of app: AXUIElement, appID: String) {
        let manual = AXUIElementSetAttributeValue(app, "AXManualAccessibility" as CFString, kCFBooleanTrue)
        let enhanced = AXUIElementSetAttributeValue(app, "AXEnhancedUserInterface" as CFString, kCFBooleanTrue)
        logger.info(
            "accessibility on in \(appID, privacy: .public): manual \(manual.rawValue), enhanced \(enhanced.rawValue)"
        )
    }

    private static func focusedTextInput(in source: AXUIElement) -> AXUIElement? {
        var focused: CFTypeRef?
        let status = AXUIElementCopyAttributeValue(source, kAXFocusedUIElementAttribute as CFString, &focused)
        guard status == .success, let focused, CFGetTypeID(focused) == AXUIElementGetTypeID() else {
            logger.info("focused element unreadable: \(status.rawValue)")
            return nil
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

        let isInput = TextInputRule.isTextInput(
            role: role as? String,
            isValueSettable: isSettable.boolValue,
            hasSelectedTextRange: rangeStatus == .success
        )
        return isInput ? element : nil
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
