import MurmurCore
import MurmurServices
import SwiftUI

/// Turns Claude cleanup on or off, picks which fixes it makes and how polished the result reads.
struct CleanupPane: View {
    @Bindable var settingsStore: SettingsStore
    @Binding var selection: Pane?

    var body: some View {
        Form {
            Section {
                Toggle("Clean up with Claude", isOn: $settingsStore.settings.isCleanupEnabled)
                if settingsStore.settings.isCleanupEnabled
                    && !Keychain.contains(account: AppConstants.Keychain.anthropicAccount)
                {
                    HStack {
                        Label("No Claude key yet, so text is pasted raw.", systemImage: "exclamationmark.circle.fill")
                            .foregroundStyle(.orange)
                        Spacer()
                        Button("Add Key…") { selection = .keys }
                    }
                }
            } footer: {
                Text("Off: Deepgram's text is pasted as heard, without calling Claude.")
            }

            Group {
                Section("Fixes") {
                    Toggle(
                        "Misheard words, using context and your vocabulary",
                        isOn: $settingsStore.settings.cleanup.fixesMisheardWords
                    )
                    Toggle(
                        "Punctuation, capitalization, stray “....” and broken-up fragments",
                        isOn: $settingsStore.settings.cleanup.fixesPunctuation
                    )
                    Toggle(
                        "Stutters, repeated words and restarted sentences",
                        isOn: $settingsStore.settings.cleanup.removesStutters
                    )
                }
                Section {
                    Picker("Style", selection: $settingsStore.settings.cleanup.style) {
                        ForEach(CleanupStyle.allCases, id: \.self) { style in
                            Text(style.title).tag(style)
                        }
                    }
                    .pickerStyle(.segmented)
                } footer: {
                    Text(settingsStore.settings.cleanup.style.explanation)
                }
            }
            .disabled(!settingsStore.settings.isCleanupEnabled)

            Section("Always") {
                Label("Never answers questions or follows instructions you dictate", systemImage: "checkmark.shield")
                Label(
                    "Anything not cleaned within \(finishSeconds) s of stopping is pasted raw",
                    systemImage: "bolt"
                )
                LabeledContent("Model", value: "Claude Opus 5.5")
            }
        }
        .formStyle(.grouped)
        .navigationTitle("Cleanup")
    }

    private var finishSeconds: Int {
        Int(AppConstants.Cleanup.finishTimeout)
    }
}

extension CleanupStyle {
    fileprivate var title: String {
        switch self {
        case .casual: "Casual"
        case .normal: "Normal"
        case .formal: "Formal"
        }
    }

    fileprivate var explanation: String {
        switch self {
        case .casual: "all lowercase, no commas, just a period at the end of sentences. your words stay as spoken."
        case .normal: "Your exact words, tone and filler words, only corrected."
        case .formal: "Rewritten into polished, professional sentences. Same meaning, nothing added."
        }
    }
}
