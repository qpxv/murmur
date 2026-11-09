/// The sections of the main window's sidebar, in display order.
enum Pane: String, CaseIterable, Identifiable {
    case home
    case history
    case vocabulary
    case cleanup
    case audio
    case overlay
    case keys

    var id: String { rawValue }

    var title: String {
        switch self {
        case .home: "Home"
        case .history: "History"
        case .vocabulary: "Vocabulary"
        case .cleanup: "Cleanup"
        case .audio: "Audio"
        case .overlay: "Shortcut & Overlay"
        case .keys: "Keys & Permissions"
        }
    }

    var symbol: String {
        switch self {
        case .home: "house"
        case .history: "clock.arrow.circlepath"
        case .vocabulary: "character.book.closed"
        case .cleanup: "wand.and.sparkles"
        case .audio: "mic"
        case .overlay: "capsule.on.rectangle"
        case .keys: "key"
        }
    }
}
