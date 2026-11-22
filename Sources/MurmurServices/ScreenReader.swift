import AppKit
import ApplicationServices
import MurmurCore
import Synchronization
import os

/// Reads the text of the front window through accessibility, for `ScreenContext`.
/// Never logs what it read, only how much and how long it took. Chromium apps show their page
/// text only once `TextInserter` has switched their accessibility on (after the first paste there).
public enum ScreenReader {
    private static let logger = Logger(subsystem: Log.subsystem, category: "screen")
    private static let attributes = [
        kAXSubroleAttribute, kAXValueAttribute, kAXTitleAttribute, kAXDescriptionAttribute, kAXChildrenAttribute,
    ]
    // value, title, description: the first one that holds text is the element's text
    private static let textAttributeCount = 3
    private static let secureSubrole = kAXSecureTextFieldSubrole

    /// What the front window shows, with its names judged against the dictionary of `language`.
    @MainActor
    public static func context(vocabulary: [String], language: DictationLanguage) async -> ScreenContext {
        let screen = ScreenContext(
            lines: await frontWindowLines(),
            vocabulary: vocabulary,
            language: language,
            isKnownWord: Spelling.knownWordCheck(for: language)
        )
        logger.info("screen context: \(screen.text.count) characters, \(screen.terms.count) terms")
        return screen
    }

    // the texts of the front window's elements, top to bottom. gives up after the read timeout
    // with whatever it has by then (a hung app answers late)
    @MainActor
    private static func frontWindowLines() async -> [String] {
        guard let app = NSWorkspace.shared.frontmostApplication,
            app.processIdentifier != ProcessInfo.processInfo.processIdentifier
        else { return [] }
        let pid = app.processIdentifier
        let started = ContinuousClock.now
        let walk = Walk()
        Thread.detachNewThread { read(pid: pid, into: walk) }
        let lines = await walk.result(waitingAtMost: AppConstants.Screen.readTimeout)
        logger.info("read \(lines.count) texts in \((ContinuousClock.now - started) / .milliseconds(1)) ms")
        return lines
    }

    private static func read(pid: pid_t, into walk: Walk) {
        let app = AXUIElementCreateApplication(pid)
        guard let window = element(app, kAXFocusedWindowAttribute) else {
            walk.finish()
            return
        }
        var pending = [window]
        var visited = 0
        while let next = pending.popLast(), visited < AppConstants.Screen.maxElements, !walk.isStopped {
            visited += 1
            pending.append(contentsOf: visit(next, into: walk).reversed())
        }
        walk.finish()
    }

    // one round trip per element; returns its children
    private static func visit(_ element: AXUIElement, into walk: Walk) -> [AXUIElement] {
        var values: CFArray?
        let status = AXUIElementCopyMultipleAttributeValues(element, attributes as CFArray, [], &values)
        guard status == .success, let values = values as? [Any] else { return [] }
        let strings = values.map { $0 as? String }
        let subrole = strings.first.flatMap { $0 }
        // a password field and everything in it stays unread
        guard subrole != secureSubrole else { return [] }
        let text = strings.dropFirst().prefix(textAttributeCount).compactMap { $0 }.first { !$0.isEmpty }
        if let text {
            walk.add(text)
        }
        return (values.last as? [AXUIElement]) ?? []
    }

    private static func element(_ source: AXUIElement, _ attribute: String) -> AXUIElement? {
        var value: CFTypeRef?
        let status = AXUIElementCopyAttributeValue(source, attribute as CFString, &value)
        guard status == .success, let value, CFGetTypeID(value) == AXUIElementGetTypeID() else { return nil }
        // safe: the type id was checked right above
        return unsafeDowncast(value, to: AXUIElement.self)
    }
}

/// The texts one read has found so far, shared between the reading thread and the caller,
/// who takes them when the read is done or the time is up, whichever comes first.
private final class Walk: Sendable {
    private struct State {
        var lines: [String] = []
        var isStopped = false
        var waiter: CheckedContinuation<Void, Never>?
    }

    private let state = Mutex(State())

    var isStopped: Bool { state.withLock(\.isStopped) }

    func add(_ line: String) {
        state.withLock { state in
            // a label and its value often repeat the same text
            guard !state.isStopped, state.lines.last != line else { return }
            state.lines.append(line)
        }
    }

    func finish() {
        stop()?.resume()
    }

    func result(waitingAtMost timeout: TimeInterval) async -> [String] {
        await withCheckedContinuation { continuation in
            let isDone = state.withLock { state in
                if state.isStopped { return true }
                state.waiter = continuation
                return false
            }
            if isDone {
                continuation.resume()
                return
            }
            Task {
                try? await Task.sleep(for: .seconds(timeout))
                self.finish()
            }
        }
        return state.withLock(\.lines)
    }

    // stops the walk; hands out the waiter once, so it's resumed exactly once
    private func stop() -> CheckedContinuation<Void, Never>? {
        state.withLock { state in
            state.isStopped = true
            let waiter = state.waiter
            state.waiter = nil
            return waiter
        }
    }
}
