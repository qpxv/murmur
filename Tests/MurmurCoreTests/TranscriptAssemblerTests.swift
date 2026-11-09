import MurmurCore
import Testing

struct TranscriptAssemblerTests {
    @Test func cleanupsFinishingOutOfOrderStillJoinInSpokenOrder() {
        var assembler = TranscriptAssembler()
        let first = assembler.addChunk("open clock code")
        let second = assembler.addChunk("and run the gates")
        assembler.setCleaned("and run the gates.", forChunk: second)
        assembler.setCleaned("Open Claude Code", forChunk: first)
        #expect(assembler.text == "Open Claude Code and run the gates.")
        #expect(assembler.hasCleanedText)
    }

    @Test func chunkWithoutCleanupFallsBackToRaw() {
        var assembler = TranscriptAssembler()
        let first = assembler.addChunk("hello there")
        _ = assembler.addChunk("this one failed")
        assembler.setCleaned("Hello there.", forChunk: first)
        #expect(assembler.text == "Hello there. this one failed")
        #expect(assembler.rawText == "hello there this one failed")
    }

    @Test func emptyCleanupIsIgnored() {
        var assembler = TranscriptAssembler()
        let first = assembler.addChunk("keep me")
        assembler.setCleaned("  ", forChunk: first)
        #expect(assembler.text == "keep me")
        #expect(!assembler.hasCleanedText)
    }

    @Test func cleanupForUnknownChunkIsIgnored() {
        var assembler = TranscriptAssembler()
        assembler.setCleaned("ghost", forChunk: 7)
        #expect(assembler.isEmpty)
        #expect(assembler.text.isEmpty)
    }

    @Test func lastRawWordsComeFromNewestChunk() {
        var assembler = TranscriptAssembler()
        #expect(assembler.lastRawWords(2).isEmpty)
        _ = assembler.addChunk("one two three")
        _ = assembler.addChunk("four five six")
        #expect(assembler.lastRawWords(2) == "five six")
    }
}
