import Foundation

/// What was on screen when a dictation started: the text Claude gets as context, and the
/// unusual names in it that Deepgram boosts as key terms.
public struct ScreenContext: Equatable, Sendable {
    /// The end of the screen text (the newest part of a chat sits at the bottom), capped.
    public let text: String
    /// Names and terms worth boosting, most frequent first, capped.
    public let terms: [String]

    public static let empty = Self(text: "", terms: [])

    private static let longestTerm = 40
    private static let shortestTerm = 2
    private static let sentenceEnds: Set<Character> = [".", "!", "?", ":", "…"]
    private static let wrapping = CharacterSet.punctuationCharacters
        .union(.symbols)
        .subtracting(CharacterSet(charactersIn: "+#"))
    private static let possessives = ["'s", "’s"]
    private static let urlScheme = "://"

    private init(text: String, terms: [String]) {
        self.text = text
        self.terms = terms
    }

    /// `lines` are the texts of the window's elements, top to bottom. `isKnownWord` says whether
    /// a word is in the dictionary of the dictation language: with it, any capitalized word the
    /// dictionary doesn't know counts as a name. Without it, only English capitalized words that
    /// don't start a sentence do (in German every noun is capitalized).
    public init(
        lines: [String],
        vocabulary: [String],
        language: DictationLanguage,
        isKnownWord: ((String) -> Bool)?
    ) {
        let kept =
            lines
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        let joined = kept.joined(separator: "\n")
        let taken = vocabulary.map { $0.lowercased() }
        let rule = NameRule(language: language, isKnownWord: isKnownWord)
        text = String(joined.suffix(AppConstants.Screen.maxCharacters))
        terms = Self.ranked(kept.flatMap { Self.candidates(in: $0, rule: rule) })
            .filter { !taken.contains($0.lowercased()) }
            .prefix(AppConstants.Screen.maxKeyterms)
            .map { $0 }
    }

    // most frequent first; ties keep the order they appeared in. one spelling per word (the first seen)
    private static func ranked(_ words: [String]) -> [String] {
        let counts = words.reduce(into: [String: Int]()) { counts, word in counts[word.lowercased(), default: 0] += 1 }
        let firstSeen = words.reduce(into: [String]()) { seen, word in
            if !seen.contains(where: { $0.caseInsensitiveCompare(word) == .orderedSame }) {
                seen.append(word)
            }
        }
        return firstSeen.enumerated()
            .sorted { lhs, rhs in
                let left = counts[lhs.element.lowercased(), default: 0]
                let right = counts[rhs.element.lowercased(), default: 0]
                return left == right ? lhs.offset < rhs.offset : left > right
            }
            .map(\.element)
    }

    private static func candidates(in line: String, rule: NameRule) -> [String] {
        let tokens = line.split(whereSeparator: \.isWhitespace).map(String.init)
        return tokens.enumerated().compactMap { index, token in
            let startsSentence = index == 0 || tokens[index - 1].last.map(sentenceEnds.contains) == true
            let word = cleaned(token)
            guard (shortestTerm...longestTerm).contains(word.count) else { return nil }
            return WordShape(word).isTerm(startsSentence: startsSentence, rule: rule) ? word : nil
        }
    }

    // "(blogr.ai)," -> "blogr.ai", "https://blogr.ai/pricing" -> "blogr.ai", "Mia's" -> "Mia"
    private static func cleaned(_ token: String) -> String {
        let host = token.range(of: urlScheme).map { token[$0.upperBound...].prefix { $0 != "/" } } ?? Substring(token)
        let trimmed = String(host).trimmingCharacters(in: wrapping)
        let possessive = possessives.first { trimmed.hasSuffix($0) }
        return possessive.map { String(trimmed.dropLast($0.count)) } ?? trimmed
    }
}

/// When a plain capitalized word counts as a name.
private struct NameRule {
    let language: DictationLanguage
    let isKnownWord: ((String) -> Bool)?

    func isName(_ word: String, startsSentence: Bool) -> Bool {
        guard let isKnownWord else { return language == .english && !startsSentence }
        return !isKnownWord(word) && !isKnownWord(word.lowercased())
    }
}

/// How a single word is written, which decides whether it's unusual enough to boost.
private struct WordShape {
    private static let minimumDomainPart = 2

    let word: String

    init(_ word: String) {
        self.word = word
    }

    func isTerm(startsSentence: Bool, rule: NameRule) -> Bool {
        guard word.contains(where: \.isLetter) else { return false }
        if isDomain || hasInnerCapital || mixesLettersAndDigits || isAcronym { return true }
        return isCapitalized && rule.isName(word, startsSentence: startsSentence)
    }

    // blogr.ai, but not e.g or a sentence glued to the next one ("done.Then" has a capital)
    private var isDomain: Bool {
        let parts = word.split(separator: ".", omittingEmptySubsequences: false)
        return parts.count > 1
            && parts.allSatisfy { $0.count >= Self.minimumDomainPart && $0.allSatisfy(isDomainCharacter) }
            && !word.contains(where: \.isUppercase)
    }

    // iPhone, GitHub, McKenzie
    private var hasInnerCapital: Bool {
        word.dropFirst().contains(where: \.isUppercase) && word.contains(where: \.isLowercase)
    }

    // GPT4, M3, 4K, but not 10am or 2nd
    private var mixesLettersAndDigits: Bool {
        word.contains(where: \.isNumber) && word.contains(where: \.isUppercase)
            && word.allSatisfy { $0.isLetter || $0.isNumber }
    }

    // API, AWS
    private var isAcronym: Bool {
        word.allSatisfy { $0.isLetter && $0.isUppercase }
    }

    private var isCapitalized: Bool {
        guard let first = word.first, first.isUppercase else { return false }
        return word.dropFirst().allSatisfy { $0.isLowercase || $0 == "-" }
    }

    private func isDomainCharacter(_ character: Character) -> Bool {
        character.isLetter || character.isNumber || character == "-"
    }
}
