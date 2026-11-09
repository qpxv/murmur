import Foundation
import MurmurCore
import Testing

struct SettingsTests {
    @Test func roundTripsThroughJSON() throws {
        let settings = Settings(
            isMicBoostEnabled: false,
            inputDeviceUID: "BuiltInMicrophoneDevice",
            vocabulary: ["Murmur"],
            overlayPosition: .topRight
        )
        let data = try JSONEncoder().encode(settings)
        #expect(try JSONDecoder().decode(Settings.self, from: data) == settings)
    }

    @Test func missingKeysFallBackToDefaults() throws {
        let saved = Data(#"{"isMicBoostEnabled": false}"#.utf8)
        let settings = try JSONDecoder().decode(Settings.self, from: saved)
        #expect(settings.isMicBoostEnabled == false)
        #expect(settings.vocabulary == Settings.defaults.vocabulary)
        #expect(settings.overlayPosition == Settings.defaults.overlayPosition)
    }
}
