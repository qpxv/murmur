import Foundation

/// Messages the app shows outside of errors (notifications). Errors live in `DictationError`.
public enum UserMessages {
    public static let copiedTitle = "Copied to clipboard"
    public static let copiedBody = "No text field was focused, so your dictation is on the clipboard. Paste it with ⌘V."
}
