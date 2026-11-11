import Foundation

/// What Claude is told when cleaning a dictated chunk. The system prompt depends only on the
/// cleanup options (so it stays identical across requests); the user message carries
/// everything that varies. The texts live in shared/cleanup-prompt.json (the windows app uses them too).
public enum CleanupPrompt {
    private static let texts = SharedFiles.cleanupPrompt

    public static func system(for options: CleanupOptions) -> String {
        let fixes = [
            options.fixesMisheardWords ? texts.fixes.misheardWords : nil,
            options.fixesPunctuation ? texts.fixes.punctuation : nil,
            options.removesStutters ? texts.fixes.stutters : nil,
        ]
        .compactMap { $0 }
        let changes = fixes.isEmpty ? "" : texts.fixesHeader + "\n" + fixes.joined(separator: "\n")
        return [texts.intro, changes, style(options.style), texts.boundaries, texts.output]
            .filter { !$0.isEmpty }
            .joined(separator: "\n\n")
    }

    public static func userMessage(
        transcript: String,
        previous: String,
        next: String,
        vocabulary: [String],
        appName: String?
    ) -> String {
        let terms = vocabulary.filter { !$0.trimmingCharacters(in: .whitespaces).isEmpty }
        let lines = [
            terms.isEmpty ? nil : "<vocabulary>\(terms.joined(separator: ", "))</vocabulary>",
            appName.map { "<app>\($0)</app>" },
            previous.isEmpty ? nil : "<previous>\(previous)</previous>",
            "<transcript>\n\(transcript)\n</transcript>",
            next.isEmpty ? nil : "<next>\(next)</next>",
        ]
        return lines.compactMap { $0 }.joined(separator: "\n")
    }

    private static func style(_ style: CleanupStyle) -> String {
        switch style {
        case .casual: texts.styles.casual
        case .normal: texts.styles.normal
        case .formal: texts.styles.formal
        }
    }
}
