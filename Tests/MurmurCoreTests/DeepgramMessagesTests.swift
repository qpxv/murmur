import Foundation
import MurmurCore
import Testing

struct DeepgramMessagesTests {
    @Test func listenURLCarriesAudioFormatAndModel() throws {
        let url = try #require(DeepgramMessages.listenURL(vocabulary: []))
        let items = try #require(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems)
        #expect(url.host() == "api.deepgram.com")
        #expect(items.contains(URLQueryItem(name: "model", value: "nova-3")))
        #expect(items.contains(URLQueryItem(name: "encoding", value: "linear16")))
        #expect(items.contains(URLQueryItem(name: "sample_rate", value: "16000")))
        #expect(items.contains(URLQueryItem(name: "channels", value: "1")))
    }

    @Test func eachVocabularyTermBecomesItsOwnKeyterm() throws {
        let url = try #require(DeepgramMessages.listenURL(vocabulary: ["Claude Code", " cmux ", ""]))
        let items = try #require(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems)
        let keyterms = items.filter { $0.name == "keyterm" }.map(\.value)
        #expect(keyterms == ["Claude Code", "cmux"])
        #expect(url.absoluteString.contains("keyterm=Claude%20Code"))
    }

    @Test func parsesFinalResult() {
        let json = """
            {"type":"Results","is_final":true,"speech_final":true,
             "channel":{"alternatives":[{"transcript":"open Claude Code","confidence":0.98}]}}
            """
        let result = DeepgramMessages.finalResult(from: Data(json.utf8))
        #expect(result?.transcript == "open Claude Code")
        #expect(result?.isPause == true)
        #expect(result?.isFromFinalize == false)
    }

    @Test func finalWithoutSpeechFinalIsNotAPause() {
        let json = """
            {"type":"Results","is_final":true,"channel":{"alternatives":[{"transcript":"and then"}]}}
            """
        #expect(DeepgramMessages.finalResult(from: Data(json.utf8))?.isPause == false)
    }

    @Test func recognizesFinalizeAnswer() {
        let json = """
            {"type":"Results","is_final":true,"from_finalize":true,
             "channel":{"alternatives":[{"transcript":""}]}}
            """
        let result = DeepgramMessages.finalResult(from: Data(json.utf8))
        #expect(result == DeepgramMessages.FinalResult(transcript: "", isPause: false, isFromFinalize: true))
    }

    @Test func ignoresInterimResults() {
        let json = """
            {"type":"Results","is_final":false,"channel":{"alternatives":[{"transcript":"open clo"}]}}
            """
        #expect(DeepgramMessages.finalResult(from: Data(json.utf8)) == nil)
    }

    @Test func ignoresOtherMessageTypesAndGarbage() {
        let metadata = #"{"type":"Metadata","request_id":"abc"}"#
        #expect(DeepgramMessages.finalResult(from: Data(metadata.utf8)) == nil)
        #expect(DeepgramMessages.finalResult(from: Data("not json".utf8)) == nil)
    }
}
