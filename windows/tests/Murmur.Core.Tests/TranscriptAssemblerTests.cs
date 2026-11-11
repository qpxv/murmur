
namespace Murmur.Core.Tests;

public class TranscriptAssemblerTests
{
    [Fact]
    public void Cleanups_finishing_out_of_order_still_join_in_spoken_order()
    {
        var assembler = new TranscriptAssembler();
        var first = assembler.AddChunk("open clock code");
        var second = assembler.AddChunk("and run the gates");
        assembler.SetCleaned("and run the gates.", second);
        assembler.SetCleaned("Open Claude Code", first);
        Assert.Equal("Open Claude Code and run the gates.", assembler.Text);
        Assert.True(assembler.HasCleanedText);
    }

    [Fact]
    public void Chunk_without_cleanup_falls_back_to_raw()
    {
        var assembler = new TranscriptAssembler();
        var first = assembler.AddChunk("hello there");
        assembler.AddChunk("this one failed");
        assembler.SetCleaned("Hello there.", first);
        Assert.Equal("Hello there. this one failed", assembler.Text);
        Assert.Equal("hello there this one failed", assembler.RawText);
    }

    [Fact]
    public void Empty_cleanup_is_ignored()
    {
        var assembler = new TranscriptAssembler();
        var first = assembler.AddChunk("keep me");
        assembler.SetCleaned("  ", first);
        Assert.Equal("keep me", assembler.Text);
        Assert.False(assembler.HasCleanedText);
    }

    [Fact]
    public void Cleanup_for_unknown_chunk_is_ignored()
    {
        var assembler = new TranscriptAssembler();
        assembler.SetCleaned("ghost", 7);
        Assert.True(assembler.IsEmpty);
        Assert.Equal("", assembler.Text);
    }

    [Fact]
    public void Last_raw_words_come_from_newest_chunk()
    {
        var assembler = new TranscriptAssembler();
        Assert.Equal("", assembler.LastRawWords(2));
        assembler.AddChunk("one two three");
        assembler.AddChunk("four five six");
        Assert.Equal("five six", assembler.LastRawWords(2));
    }
}
