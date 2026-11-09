import MurmurCore
import Testing

struct TextInputRuleTests {
    @Test(arguments: ["AXTextField", "AXTextArea", "AXComboBox", "AXSearchField"])
    func standardTextRolesAreInputs(role: String) {
        #expect(TextInputRule.isTextInput(role: role, isValueSettable: false, hasSelectedTextRange: false))
    }

    @Test func editableValueWithCaretIsInput() {
        #expect(TextInputRule.isTextInput(role: "AXGroup", isValueSettable: true, hasSelectedTextRange: true))
    }

    @Test func buttonsAndStaticTextAreNotInputs() {
        #expect(!TextInputRule.isTextInput(role: "AXButton", isValueSettable: false, hasSelectedTextRange: false))
        #expect(!TextInputRule.isTextInput(role: "AXStaticText", isValueSettable: false, hasSelectedTextRange: true))
    }

    @Test func nothingFocusedIsNotInput() {
        #expect(!TextInputRule.isTextInput(role: nil, isValueSettable: false, hasSelectedTextRange: false))
    }
}
