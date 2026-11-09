import Foundation
import MurmurCore
import Testing

struct SettingsTests {
    @Test func roundTripsThroughJSON() throws {
        var settings = Settings.defaults
        settings.isMicBoostEnabled = false
        settings.inputDeviceUID = "BuiltInMicrophoneDevice"
        settings.vocabulary = ["Murmur"]
        settings.overlayPosition = .topRight
        settings.isCleanupEnabled = false
        settings.cleanup.style = .casual
        settings.cleanup.removesStutters = false
        settings.typingWordsPerMinute = 65
        settings.isHistoryEnabled = false
        let data = try JSONEncoder().encode(settings)
        #expect(try JSONDecoder().decode(Settings.self, from: data) == settings)
    }

    @Test func missingKeysFallBackToDefaults() throws {
        let saved = Data(#"{"isMicBoostEnabled": false}"#.utf8)
        let settings = try JSONDecoder().decode(Settings.self, from: saved)
        var expected = Settings.defaults
        expected.isMicBoostEnabled = false
        #expect(settings == expected)
    }

    @Test func addsTrimmedNewTerm() {
        var settings = Settings.defaults
        settings.vocabulary = ["Claude"]
        let wasAdded = settings.addVocabularyTerm("  Murmur \n")
        #expect(wasAdded)
        #expect(settings.vocabulary == ["Claude", "Murmur"])
    }

    @Test func rejectsDuplicateIgnoringCase() {
        var settings = Settings.defaults
        settings.vocabulary = ["Claude Code"]
        let wasAdded = settings.addVocabularyTerm("claude code")
        #expect(!wasAdded)
        #expect(settings.vocabulary == ["Claude Code"])
    }

    @Test func rejectsBlankTerm() {
        var settings = Settings.defaults
        settings.vocabulary = []
        let wasAdded = settings.addVocabularyTerm("   ")
        #expect(!wasAdded)
        #expect(settings.vocabulary.isEmpty)
    }
}
