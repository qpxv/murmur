import Foundation
import MurmurCore
import Testing

struct ScreenContextTests {
    private func terms(
        _ lines: [String],
        vocabulary: [String] = [],
        language: DictationLanguage = .english,
        isKnownWord: ((String) -> Bool)? = nil
    ) -> [String] {
        ScreenContext(lines: lines, vocabulary: vocabulary, language: language, isKnownWord: isKnownWord).terms
    }

    @Test func findsDomainsEvenInsideURLsAndPunctuation() {
        #expect(
            terms(["check (blogr.ai), it's live", "https://noah-site.dev/pricing"]) == ["blogr.ai", "noah-site.dev"]
        )
    }

    @Test func ignoresAbbreviationsAndVersionNumbers() {
        #expect(terms(["e.g. we ship v1.2.3 at 10am on the 2nd"]).isEmpty)
    }

    @Test func findsInnerCapitalsAcronymsAndModelNames() {
        #expect(terms(["we use GitHub on an iPhone with the API and GPT4"]) == ["GitHub", "iPhone", "API", "GPT4"])
    }

    @Test func englishCapitalMidSentenceIsANameButSentenceStartIsNot() {
        #expect(terms(["Thanks, ask Xaver. Then we see"]) == ["Xaver"])
    }

    @Test func germanWithoutDictionarySkipsPlainCapitals() {
        #expect(terms(["Das Haus von Xaver ist auf blogr.ai"], language: .german) == ["blogr.ai"])
    }

    @Test func withDictionaryOnlyUnknownCapitalsCountEvenAtSentenceStart() {
        let known: Set<String> = ["das", "haus", "von", "ist"]
        let found = terms(["Xaver schreibt", "Das Haus von Lena"], language: .german) { known.contains($0) }
        #expect(found == ["Xaver", "Lena"])
    }

    @Test func dropsPossessiveEnding() {
        #expect(terms(["that is Mia's idea"]) == ["Mia"])
    }

    @Test func leavesOutVocabularyIgnoringCase() {
        #expect(terms(["open the Claude app and blogr.ai"], vocabulary: ["claude"]) == ["blogr.ai"])
    }

    @Test func mostFrequentFirstKeepingFirstSpelling() {
        let found = terms(["hi to Noah and Lena", "hi to Lena", "hi to LENA"])
        #expect(found == ["Lena", "Noah"])
    }

    @Test func capsTheNumberOfTerms() {
        let lines = (0..<AppConstants.Screen.maxKeyterms + 5).map { "see site\($0).dev" }
        #expect(terms(lines).count == AppConstants.Screen.maxKeyterms)
    }

    @Test func keepsTheEndOfLongTextAndDropsBlankLines() {
        let long = String(repeating: "a", count: AppConstants.Screen.maxCharacters)
        let screen = ScreenContext(
            lines: [long, "  ", " newest "],
            vocabulary: [],
            language: .english,
            isKnownWord: nil
        )
        #expect(screen.text.count == AppConstants.Screen.maxCharacters)
        #expect(screen.text.hasSuffix("a\nnewest"))
    }

    @Test func emptyScreenHasNoTextOrTerms() {
        let screen = ScreenContext(lines: [], vocabulary: [], language: .english, isKnownWord: nil)
        #expect(screen == .empty)
    }
}
