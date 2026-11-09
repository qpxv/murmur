import MurmurCore
import Testing

struct ChunkerTests {
    @Test func pauseBeforeMinimumKeepsCollecting() {
        var chunker = Chunker(minWords: 5, maxWords: 20)
        #expect(chunker.add("one two three", isPause: true) == nil)
        #expect(chunker.add("four five", isPause: false) == nil)
    }

    @Test func pauseAfterMinimumClosesChunk() {
        var chunker = Chunker(minWords: 5, maxWords: 20)
        _ = chunker.add("one two three", isPause: false)
        #expect(chunker.add("four five six", isPause: true) == "one two three four five six")
        #expect(chunker.flush() == nil)
    }

    @Test func hardCapClosesWithoutPause() {
        var chunker = Chunker(minWords: 5, maxWords: 8)
        _ = chunker.add("one two three four five", isPause: false)
        #expect(chunker.add("six seven eight", isPause: false) == "one two three four five six seven eight")
    }

    @Test func flushReturnsLeftoverOnce() {
        var chunker = Chunker(minWords: 5, maxWords: 20)
        _ = chunker.add("almost done", isPause: false)
        #expect(chunker.flush() == "almost done")
        #expect(chunker.flush() == nil)
    }

    @Test func emptySegmentsAddNothing() {
        var chunker = Chunker(minWords: 1, maxWords: 20)
        #expect(chunker.add("   ", isPause: true) == nil)
        #expect(chunker.flush() == nil)
    }

    @Test func firstWordsTakesALeadingSlice() {
        #expect(Chunker.firstWords(of: "through the text again and see", count: 3) == "through the text")
        #expect(Chunker.firstWords(of: "short", count: 3) == "short")
        #expect(Chunker.firstWords(of: "", count: 3).isEmpty)
    }

    @Test func countsWordsAcrossAnyWhitespace() {
        #expect(Chunker.wordCount(of: " Claude  Code\nrocks ") == 3)
        #expect(Chunker.wordCount(of: "") == 0)
    }
}
