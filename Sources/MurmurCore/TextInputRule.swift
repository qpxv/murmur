import Foundation

/// Decides from accessibility attributes whether the focused element accepts typed text,
/// i.e. whether to paste into it or fall back to the clipboard.
public enum TextInputRule {
    static let textRoles: Set<String> = ["AXTextField", "AXTextArea", "AXComboBox", "AXSearchField"]

    public static func isTextInput(role: String?, isValueSettable: Bool, hasSelectedTextRange: Bool) -> Bool {
        if let role, textRoles.contains(role) { return true }
        // web contenteditable and custom editors don't use the standard roles but still
        // expose an editable value with a caret
        return isValueSettable && hasSelectedTextRange
    }

    /// A paste landed when the field's length changed. Fields that don't report a length
    /// count as landed, since there is nothing to check against.
    public static func didPasteLand(lengthBefore: Int?, lengthAfter: Int?) -> Bool {
        guard let lengthBefore, let lengthAfter else { return true }
        return lengthAfter != lengthBefore
    }
}
