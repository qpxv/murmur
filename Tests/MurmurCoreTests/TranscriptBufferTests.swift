import MurmurCore
import Testing

struct TranscriptBufferTests {
    @Test func joinsSegmentsInOrderWithSingleSpaces() {
        var buffer = TranscriptBuffer()
        buffer.append("Hello there.")
        buffer.append("  How are you?\n")
        #expect(buffer.text == "Hello there. How are you?")
    }

    @Test func skipsEmptySegments() {
        var buffer = TranscriptBuffer()
        buffer.append("")
        buffer.append("   ")
        #expect(buffer.isEmpty)
        #expect(buffer.text.isEmpty)
    }
}
