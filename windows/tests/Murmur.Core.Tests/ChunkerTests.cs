
namespace Murmur.Core.Tests;

public class ChunkerTests
{
    [Fact]
    public void Pause_before_minimum_keeps_collecting()
    {
        var chunker = new Chunker(minWords: 5, maxWords: 20);
        Assert.Null(chunker.Add("one two three", isPause: true));
        Assert.Null(chunker.Add("four five", isPause: false));
    }

    [Fact]
    public void Pause_after_minimum_closes_chunk()
    {
        var chunker = new Chunker(minWords: 5, maxWords: 20);
        chunker.Add("one two three", isPause: false);
        Assert.Equal("one two three four five six", chunker.Add("four five six", isPause: true));
        Assert.Null(chunker.Flush());
    }

    [Fact]
    public void Hard_cap_closes_without_pause()
    {
        var chunker = new Chunker(minWords: 5, maxWords: 8);
        chunker.Add("one two three four five", isPause: false);
        Assert.Equal("one two three four five six seven eight", chunker.Add("six seven eight", isPause: false));
    }

    [Fact]
    public void Flush_returns_leftover_once()
    {
        var chunker = new Chunker(minWords: 5, maxWords: 20);
        chunker.Add("almost done", isPause: false);
        Assert.Equal("almost done", chunker.Flush());
        Assert.Null(chunker.Flush());
    }

    [Fact]
    public void Empty_segments_add_nothing()
    {
        var chunker = new Chunker(minWords: 1, maxWords: 20);
        Assert.Null(chunker.Add("   ", isPause: true));
        Assert.Null(chunker.Flush());
    }

    [Fact]
    public void First_words_takes_a_leading_slice()
    {
        Assert.Equal("through the text", Chunker.FirstWords("through the text again and see", 3));
        Assert.Equal("short", Chunker.FirstWords("short", 3));
        Assert.Equal("", Chunker.FirstWords("", 3));
    }

    [Fact]
    public void Counts_words_across_any_whitespace()
    {
        Assert.Equal(3, Chunker.WordCount(" Claude  Code\nrocks "));
        Assert.Equal(0, Chunker.WordCount(""));
    }

    [Fact]
    public void Default_sizes_come_from_the_shared_config()
    {
        Assert.Equal(40, AppConstants.Cleanup.MinChunkWords);
        Assert.Equal(150, AppConstants.Cleanup.MaxChunkWords);
    }
}
