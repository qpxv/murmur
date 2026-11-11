using Kind = Murmur.Core.TextInputRule.ControlKind;

namespace Murmur.Core.Tests;

public class TextInputRuleTests
{
    [Theory]
    [InlineData(Kind.Edit)]
    [InlineData(Kind.Document)]
    public void Edit_and_document_controls_are_inputs(Kind kind) =>
        Assert.True(TextInputRule.IsTextInput(kind, isValueReadOnly: null, hasTextPattern: false));

    [Fact]
    public void Writable_value_on_a_custom_control_is_input() =>
        Assert.True(TextInputRule.IsTextInput(Kind.Other, isValueReadOnly: false, hasTextPattern: false));

    [Fact]
    public void Terminal_with_only_a_text_pattern_is_input() =>
        Assert.True(TextInputRule.IsTextInput(Kind.Other, isValueReadOnly: null, hasTextPattern: true));

    [Fact]
    public void Read_only_document_like_a_web_page_is_not_input() =>
        Assert.False(TextInputRule.IsTextInput(Kind.Document, isValueReadOnly: true, hasTextPattern: true));

    [Fact]
    public void Buttons_and_static_text_are_not_inputs()
    {
        Assert.False(TextInputRule.IsTextInput(Kind.Other, isValueReadOnly: null, hasTextPattern: false));
        Assert.False(TextInputRule.IsTextInput(Kind.Other, isValueReadOnly: true, hasTextPattern: false));
    }

    [Fact]
    public void Nothing_focused_is_not_input() =>
        Assert.False(TextInputRule.IsTextInput(null, isValueReadOnly: null, hasTextPattern: false));

    [Fact]
    public void Paste_landed_when_field_grew() => Assert.True(TextInputRule.DidPasteLand(0, 42));

    [Fact]
    public void Paste_replacing_a_selection_still_counts_when_length_changed() => Assert.True(TextInputRule.DidPasteLand(80, 30));

    [Fact]
    public void Unchanged_field_means_the_paste_was_dropped() => Assert.False(TextInputRule.DidPasteLand(12, 12));

    [Fact]
    public void Field_without_length_counts_as_landed()
    {
        Assert.True(TextInputRule.DidPasteLand(null, 5));
        Assert.True(TextInputRule.DidPasteLand(5, null));
    }
}
