import AppKit
import MurmurCore

/// The system dictionary, so `ScreenContext` can tell a name from an ordinary word.
enum Spelling {
    /// A dictionary check for `language`, or `nil` when the Mac has no dictionary for it.
    @MainActor
    static func knownWordCheck(for language: DictationLanguage) -> ((String) -> Bool)? {
        let checker = NSSpellChecker.shared
        // the spell checker takes the same iso codes deepgram does
        let code = language.deepgramCode
        guard checker.availableLanguages.contains(where: { $0.hasPrefix(code) }) else { return nil }
        return { word in
            let misspelled = checker.checkSpelling(
                of: word,
                startingAt: 0,
                language: code,
                wrap: false,
                inSpellDocumentWithTag: 0,
                wordCount: nil
            )
            return misspelled.location == NSNotFound
        }
    }
}
