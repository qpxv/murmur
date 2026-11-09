import Foundation

/// Deepgram live-streaming wire format: the connection URL and the messages both ways.
public enum DeepgramMessages {
    public static let finalize = #"{"type":"Finalize"}"#
    public static let closeStream = #"{"type":"CloseStream"}"#

    /// The listen URL for 16 kHz mono PCM, with every vocabulary term boosted as a key term.
    public static func listenURL(vocabulary: [String]) -> URL? {
        var components = URLComponents(string: AppConstants.Deepgram.listenURL)
        let fixed: [URLQueryItem] = [
            URLQueryItem(name: "model", value: AppConstants.Deepgram.model),
            URLQueryItem(name: "language", value: AppConstants.Deepgram.language),
            URLQueryItem(name: "encoding", value: "linear16"),
            URLQueryItem(name: "sample_rate", value: String(Int(AppConstants.Audio.sampleRate))),
            URLQueryItem(name: "channels", value: String(AppConstants.Audio.channelCount)),
            URLQueryItem(name: "smart_format", value: "true"),
            URLQueryItem(name: "punctuate", value: "true"),
        ]
        let keyterms =
            vocabulary
            .map { $0.trimmingCharacters(in: .whitespaces) }
            .filter { !$0.isEmpty }
            .map { URLQueryItem(name: "keyterm", value: $0) }
        components?.queryItems = fixed + keyterms
        return components?.url
    }

    /// One transcript message. Only finalized results are returned; everything else
    /// (metadata, speech-started events, interim results) is `nil`.
    public static func finalResult(from data: Data) -> FinalResult? {
        guard let message = try? JSONDecoder().decode(ResultsMessage.self, from: data),
            message.type == "Results",
            message.isFinal
        else { return nil }
        let transcript = message.channel?.alternatives.first?.transcript ?? ""
        return FinalResult(
            transcript: transcript,
            isPause: message.isSpeechFinal,
            isFromFinalize: message.isFromFinalize
        )
    }

    public struct FinalResult: Equatable, Sendable {
        public let transcript: String
        /// The speaker paused after this segment: a natural place to close a cleanup chunk.
        public let isPause: Bool
        /// The answer to our Finalize message: everything said so far has now arrived.
        public let isFromFinalize: Bool

        public init(transcript: String, isPause: Bool, isFromFinalize: Bool) {
            self.transcript = transcript
            self.isPause = isPause
            self.isFromFinalize = isFromFinalize
        }
    }
}

// deepgram leaves flags out instead of sending false, so missing means false
private struct ResultsMessage: Decodable {
    let type: String
    let isFinal: Bool
    let isSpeechFinal: Bool
    let isFromFinalize: Bool
    let channel: ResultsChannel?

    private enum CodingKeys: String, CodingKey {
        case type
        case isFinal = "is_final"
        case isSpeechFinal = "speech_final"
        case isFromFinalize = "from_finalize"
        case channel
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        type = try container.decode(String.self, forKey: .type)
        isFinal = try container.decodeIfPresent(Bool.self, forKey: .isFinal) ?? false
        isSpeechFinal = try container.decodeIfPresent(Bool.self, forKey: .isSpeechFinal) ?? false
        isFromFinalize = try container.decodeIfPresent(Bool.self, forKey: .isFromFinalize) ?? false
        channel = try container.decodeIfPresent(ResultsChannel.self, forKey: .channel)
    }
}

private struct ResultsChannel: Decodable {
    let alternatives: [ResultsAlternative]
}

private struct ResultsAlternative: Decodable {
    let transcript: String
}
