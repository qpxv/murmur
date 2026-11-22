import MurmurCore
import MurmurServices
import SwiftUI

/// Words and names that must come out spelled exactly right. They're boosted in Deepgram
/// and handed to Claude for cleanup.
struct VocabularyPane: View {
    @Bindable var settingsStore: SettingsStore
    @State private var draft = ""

    var body: some View {
        Form {
            Section {
                HStack {
                    TextField("New term", text: $draft, prompt: Text("Add a word or name"))
                        .labelsHidden()
                        .onSubmit(add)
                    // always clickable: a blank or duplicate entry is simply ignored
                    Button("Add", action: add)
                        .buttonStyle(.borderedProminent)
                }
            } footer: {
                Text("Names, products and jargon that get misheard, e.g. “Claude Code”.")
            }
            Section {
                Toggle("Use names from the screen", isOn: $settingsStore.settings.isScreenContextEnabled)
            } footer: {
                Text(
                    "When you start dictating, the text of the front window (a chat, a web page) is read, so names "
                        + "on it are spelled right. It's sent to Deepgram and Claude with that dictation, never saved."
                )
            }
            Section("\(settingsStore.settings.vocabulary.count) terms") {
                ForEach(settingsStore.settings.vocabulary, id: \.self) { term in
                    HStack {
                        Text(term)
                        Spacer()
                        Button("Remove", systemImage: "minus.circle.fill") { remove(term) }
                            .labelStyle(.iconOnly)
                            .buttonStyle(.borderless)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Vocabulary")
    }

    private func add() {
        if settingsStore.settings.addVocabularyTerm(draft) {
            draft = ""
        }
    }

    private func remove(_ term: String) {
        settingsStore.settings.vocabulary.removeAll { $0 == term }
    }
}
