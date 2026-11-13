import AppKit
import MurmurServices
import SwiftUI

/// One past dictation: when, where, how long, the text, and a copy button. With
/// `showsOriginal`, the uncleaned Deepgram text can be expanded below.
struct DictationRow: View {
    let record: DictationRecord
    var showsOriginal = false
    @State private var isCopied = false

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.StatCard.valueSpacing) {
            HStack {
                Text(record.date, format: .dateTime.weekday(.abbreviated).hour().minute())
                if let app = record.appName {
                    Text(app)
                }
                Text("\(record.wordCount) words")
                if record.wasCleaned {
                    Image(systemName: "wand.and.sparkles")
                        .help("Cleaned up by Claude")
                }
                Spacer()
                if !record.text.isEmpty {
                    if isCopied {
                        Label("Copied", systemImage: "checkmark")
                            .foregroundStyle(.green)
                            .transition(.opacity)
                    } else {
                        Button("Copy", systemImage: "doc.on.doc", action: copy)
                            .labelStyle(.iconOnly)
                            .buttonStyle(.borderless)
                            .help("Copy text")
                    }
                }
            }
            .font(.caption)
            .foregroundStyle(.secondary)

            if record.text.isEmpty {
                Text("Text not saved").foregroundStyle(.tertiary)
            } else {
                Text(record.text)
                    .textSelection(.enabled)
                    .lineLimit(showsOriginal ? nil : Theme.Window.previewLines)
            }

            if showsOriginal, !record.rawText.isEmpty, record.rawText != record.text {
                DisclosureGroup("Original") {
                    Text(record.rawText)
                        .foregroundStyle(.secondary)
                        .textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }
                .font(.callout)
            }
        }
        .padding(.vertical, Theme.StatCard.valueSpacing)
        // one-shot: flips "Copied" back after a moment, cancelled if the row goes away
        .task(id: isCopied) {
            guard isCopied else { return }
            try? await Task.sleep(for: .seconds(Theme.Window.copiedFeedback))
            withAnimation { isCopied = false }
        }
    }

    private func copy() {
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(record.text, forType: .string)
        withAnimation { isCopied = true }
    }
}
