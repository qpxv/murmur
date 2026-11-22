import Foundation
import MurmurCore
import os

/// One live Deepgram transcription session for one dictation.
///
/// Audio is queued from the audio thread with `send(_:)` (also before `start(keyterms:)`, it waits
/// for the connection) and streamed in order. Finalized
/// segments come out of `results` as they arrive (never shown to the user, only cleaned and
/// assembled). `finish()` flushes the last words; `results` ends right after.
public actor DeepgramStream {
    private static let unauthorizedStatus = 401

    /// Every finalized segment, in order. Ends when the stream finishes or is cancelled.
    public nonisolated let results: AsyncStream<DeepgramMessages.FinalResult>

    private let apiKey: String
    private let language: DictationLanguage
    private let audio: AsyncStream<Data>
    private let audioQueue: AsyncStream<Data>.Continuation
    private let resultsQueue: AsyncStream<DeepgramMessages.FinalResult>.Continuation
    private let logger = Logger(subsystem: Log.subsystem, category: "deepgram")

    // set by start: the key terms (the screen's names) are only known by then
    private var socket: URLSessionWebSocketTask?
    private var hasHeardSpeech = false
    private var isClosed = false
    private var failure: DictationError?
    private var finalizeWaiter: CheckedContinuation<Void, Never>?
    private var sendTask: Task<Void, Never>?
    private var receiveTask: Task<Void, Never>?

    public init(apiKey: String, language: DictationLanguage) {
        self.apiKey = apiKey
        self.language = language
        (audio, audioQueue) = AsyncStream.makeStream(of: Data.self, bufferingPolicy: .unbounded)
        (results, resultsQueue) = AsyncStream.makeStream(
            of: DeepgramMessages.FinalResult.self,
            bufferingPolicy: .unbounded
        )
    }

    /// Connects with every one of `keyterms` boosted. Does nothing once cancelled.
    public func start(keyterms: [String]) {
        guard !isClosed else { return }
        guard let url = DeepgramMessages.listenURL(vocabulary: keyterms, language: language) else {
            failure = .connectionFailed
            logger.error("couldn't build the deepgram url")
            return
        }
        var request = URLRequest(url: url)
        request.setValue("Token \(apiKey)", forHTTPHeaderField: "Authorization")
        let socket = URLSession.shared.webSocketTask(with: request)
        self.socket = socket
        socket.resume()
        sendTask = Task { await self.sendAudio(over: socket) }
        receiveTask = Task { await self.receiveResults(from: socket) }
    }

    /// Queues audio for sending. Safe to call from the audio thread.
    public nonisolated func send(_ pcm: Data) {
        audioQueue.yield(pcm)
    }

    /// Sends the remaining audio and waits for Deepgram to finalize it. Only throws when the
    /// connection failed before any speech arrived; text that already came through is kept.
    public func finish() async throws(DictationError) {
        audioQueue.finish()
        await sendTask?.value
        if failure == nil, socket != nil {
            await sendText(DeepgramMessages.finalize)
            await waitForFinalize()
            await sendText(DeepgramMessages.closeStream)
        }
        close()
        if !hasHeardSpeech, let failure { throw failure }
    }

    public func cancel() {
        audioQueue.finish()
        close()
    }

    private func close() {
        isClosed = true
        socket?.cancel(with: .normalClosure, reason: nil)
        receiveTask?.cancel()
        resultsQueue.finish()
        resumeFinalizeWaiter()
    }

    private func sendAudio(over socket: URLSessionWebSocketTask) async {
        for await chunk in audio {
            do {
                try await socket.send(.data(chunk))
            } catch {
                fail(error)
                return
            }
        }
    }

    private func sendText(_ text: String) async {
        do {
            try await socket?.send(.string(text))
        } catch {
            fail(error)
        }
    }

    private func receiveResults(from socket: URLSessionWebSocketTask) async {
        while !Task.isCancelled {
            do {
                handle(try await socket.receive())
            } catch {
                fail(error)
                resumeFinalizeWaiter()
                return
            }
        }
    }

    private func handle(_ message: URLSessionWebSocketTask.Message) {
        let data: Data
        switch message {
        case .data(let payload): data = payload
        case .string(let text): data = Data(text.utf8)
        @unknown default: return
        }
        guard let result = DeepgramMessages.finalResult(from: data) else { return }
        if !result.transcript.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            hasHeardSpeech = true
        }
        resultsQueue.yield(result)
        if result.isFromFinalize {
            resumeFinalizeWaiter()
        }
    }

    private func waitForFinalize() async {
        await withCheckedContinuation { continuation in
            finalizeWaiter = continuation
            Task {
                try? await Task.sleep(for: .seconds(AppConstants.Deepgram.finalizeTimeout))
                self.resumeFinalizeWaiter()
            }
        }
    }

    private func resumeFinalizeWaiter() {
        finalizeWaiter?.resume()
        finalizeWaiter = nil
    }

    private func fail(_ error: any Error) {
        // the pending receive always errors once we close the socket ourselves; that's not a failure
        guard failure == nil, !isClosed else { return }
        let status = (socket?.response as? HTTPURLResponse)?.statusCode
        failure = status == Self.unauthorizedStatus ? .invalidDeepgramKey : .connectionFailed
        logger.error(
            "deepgram connection failed (status \(status ?? 0)): \(error.localizedDescription, privacy: .public)"
        )
    }
}
