import Foundation
import MurmurCore

/// Cleans one dictation chunk by chunk while the user is still talking. Segments come in from
/// Deepgram; each closed chunk is sent to Claude right away. `finish()` closes the last chunk,
/// waits a bounded time for the requests still in flight and assembles the result.
public actor CleanupSession {
    private let cleaner: ClaudeCleaner?
    private let vocabulary: [String]
    private let appName: String?

    private var chunker = Chunker()
    private var assembler = TranscriptAssembler()
    private var requests: [Task<Void, Never>] = []
    private var inFlight = 0
    private var finishWaiter: CheckedContinuation<Void, Never>?

    /// `cleaner` is `nil` when cleanup is off or no Claude key is set: chunks then stay raw.
    public init(cleaner: ClaudeCleaner?, vocabulary: [String], appName: String?) {
        self.cleaner = cleaner
        self.vocabulary = vocabulary
        self.appName = appName
    }

    public func add(_ result: DeepgramMessages.FinalResult) {
        guard let chunk = chunker.add(result.transcript, isPause: result.isPause) else { return }
        startChunk(chunk)
    }

    public func finish() async -> DictationTranscript {
        if let rest = chunker.flush() {
            startChunk(rest)
        }
        if inFlight > 0 {
            await waitForRequests()
        }
        requests.forEach { $0.cancel() }
        return DictationTranscript(
            text: assembler.text,
            rawText: assembler.rawText,
            wasCleaned: assembler.hasCleanedText
        )
    }

    public func cancel() {
        requests.forEach { $0.cancel() }
        resumeFinishWaiter()
    }

    private func startChunk(_ raw: String) {
        let previous = assembler.lastRawWords(AppConstants.Cleanup.contextWords)
        let number = assembler.addChunk(raw)
        guard let cleaner else { return }
        let message = CleanupPrompt.userMessage(
            transcript: raw,
            previous: previous,
            vocabulary: vocabulary,
            appName: appName
        )
        inFlight += 1
        requests.append(
            Task {
                let cleaned = await cleaner.clean(message)
                chunkFinished(number, cleaned: cleaned)
            }
        )
    }

    private func chunkFinished(_ number: Int, cleaned: String?) {
        if let cleaned {
            assembler.setCleaned(cleaned, forChunk: number)
        }
        inFlight -= 1
        if inFlight == 0 {
            resumeFinishWaiter()
        }
    }

    // waits for every request, but never longer than the finish timeout: whatever isn't back
    // by then is pasted raw
    private func waitForRequests() async {
        await withCheckedContinuation { continuation in
            finishWaiter = continuation
            Task {
                try? await Task.sleep(for: .seconds(AppConstants.Cleanup.finishTimeout))
                self.resumeFinishWaiter()
            }
        }
    }

    private func resumeFinishWaiter() {
        finishWaiter?.resume()
        finishWaiter = nil
    }
}
