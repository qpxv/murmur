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

    @Test func pasteLandedWhenFieldGrew() {
        #expect(TextInputRule.didPasteLand(lengthBefore: 0, lengthAfter: 42))
    }

    @Test func pasteReplacingASelectionStillCountsWhenLengthChanged() {
        #expect(TextInputRule.didPasteLand(lengthBefore: 80, lengthAfter: 30))
    }

    @Test func unchangedFieldMeansThePasteWasDropped() {
        #expect(!TextInputRule.didPasteLand(lengthBefore: 12, lengthAfter: 12))
    }

    @Test func fieldWithoutLengthCountsAsLanded() {
        #expect(TextInputRule.didPasteLand(lengthBefore: nil, lengthAfter: 5))
        #expect(TextInputRule.didPasteLand(lengthBefore: 5, lengthAfter: nil))
    }
}
