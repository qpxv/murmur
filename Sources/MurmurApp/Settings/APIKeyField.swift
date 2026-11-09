import MurmurServices
import SwiftUI

/// One API key stored in the Keychain. A saved key is never shown in an editable field, so a
/// stray keystroke can't wipe it; replacing it is an explicit step.
struct APIKeyField: View {
    let title: String
    let account: String

    @State private var hasSavedKey: Bool
    @State private var isEditing = false
    @State private var draft = ""

    init(title: String, account: String) {
        self.title = title
        self.account = account
        _hasSavedKey = State(initialValue: Keychain.read(account: account) != nil)
    }

    var body: some View {
        if hasSavedKey && !isEditing {
            LabeledContent(title) {
                HStack {
                    Label("Saved in Keychain", systemImage: "checkmark.circle.fill")
                        .foregroundStyle(.green)
                    Button("Replace…") { isEditing = true }
                }
            }
        } else {
            LabeledContent(title) {
                HStack {
                    SecureField("Paste key", text: $draft)
                        .labelsHidden()
                        .onSubmit(save)
                    if hasSavedKey {
                        Button("Cancel", action: cancel)
                    }
                    Button("Save", action: save)
                        .disabled(draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
        }
    }

    private func save() {
        let key = draft.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty, Keychain.write(key, account: account) else { return }
        hasSavedKey = true
        cancel()
    }

    private func cancel() {
        draft = ""
        isEditing = false
    }
}
