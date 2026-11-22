/// What Claude knows about a whole dictation, sent along with each of its chunks.
public struct CleanupContext: Equatable, Sendable {
    /// Terms that must be spelled exactly like this.
    public var vocabulary: [String]
    /// The app the user dictates into.
    public var appName: String?
    /// The front window's text from `ScreenContext`, empty when not read.
    public var screen: String

    public init(vocabulary: [String], appName: String?, screen: String) {
        self.vocabulary = vocabulary
        self.appName = appName
        self.screen = screen
    }
}
