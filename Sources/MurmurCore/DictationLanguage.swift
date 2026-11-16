/// The language the user speaks, which picks Deepgram's model language.
public enum DictationLanguage: String, Codable, CaseIterable, Sendable {
    case english, german

    public var deepgramCode: String {
        switch self {
        case .english: AppConstants.Deepgram.englishLanguageCode
        case .german: AppConstants.Deepgram.germanLanguageCode
        }
    }

    public var title: String {
        switch self {
        case .english: "English"
        case .german: "German"
        }
    }
}
