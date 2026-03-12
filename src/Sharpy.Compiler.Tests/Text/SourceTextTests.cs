using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Text;

public class SourceTextTests
{
    [Fact]
    public void Constructor_ValidText_SetsProperties()
    {
        var text = "hello\nworld";
        var sourceText = new SourceText(text);

        Assert.Equal(11, sourceText.Length);
        Assert.Null(sourceText.FilePath);
    }

    [Fact]
    public void Constructor_WithFilePath_SetsFilePath()
    {
        var text = "hello";
        var sourceText = new SourceText(text, "/path/to/file.spy");

        Assert.Equal("/path/to/file.spy", sourceText.FilePath);
    }

    [Fact]
    public void Constructor_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SourceText(null!));
    }

    [Fact]
    public void Indexer_ValidPosition_ReturnsCharacter()
    {
        var sourceText = new SourceText("hello");

        Assert.Equal('h', sourceText[0]);
        Assert.Equal('o', sourceText[4]);
    }

    [Fact]
    public void GetText_ValidSpan_ReturnsSubstring()
    {
        var sourceText = new SourceText("hello world");
        var span = new TextSpan(6, 5);

        Assert.Equal("world", sourceText.GetText(span));
    }

    [Fact]
    public void GetText_SpanOutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        var sourceText = new SourceText("hello");
        var span = new TextSpan(3, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetText(span));
    }

    [Fact]
    public void LineCount_SingleLine_ReturnsOne()
    {
        var sourceText = new SourceText("hello");

        Assert.Equal(1, sourceText.LineCount);
    }

    [Fact]
    public void LineCount_MultipleLines_ReturnsCorrectCount()
    {
        var sourceText = new SourceText("line1\nline2\nline3");

        Assert.Equal(3, sourceText.LineCount);
    }

    [Fact]
    public void LineCount_EndsWithNewline_IncludesEmptyLine()
    {
        var sourceText = new SourceText("line1\nline2\n");

        Assert.Equal(3, sourceText.LineCount);
    }

    [Fact]
    public void GetLineNumber_FirstLine_ReturnsOne()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal(1, sourceText.GetLineNumber(0));
        Assert.Equal(1, sourceText.GetLineNumber(4));
    }

    [Fact]
    public void GetLineNumber_SecondLine_ReturnsTwo()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal(2, sourceText.GetLineNumber(6));
        Assert.Equal(2, sourceText.GetLineNumber(10));
    }

    [Fact]
    public void GetLineNumber_PositionAtNewline_ReturnsPreviousLine()
    {
        var sourceText = new SourceText("hello\nworld");

        // Position 5 is the '\n' character, which is still part of line 1
        Assert.Equal(1, sourceText.GetLineNumber(5));
    }

    [Fact]
    public void GetColumnNumber_FirstCharacter_ReturnsOne()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal(1, sourceText.GetColumnNumber(0)); // 'h' on line 1
        Assert.Equal(1, sourceText.GetColumnNumber(6)); // 'w' on line 2
    }

    [Fact]
    public void GetColumnNumber_MiddleOfLine_ReturnsCorrectColumn()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal(3, sourceText.GetColumnNumber(2)); // 'l' on line 1
        Assert.Equal(4, sourceText.GetColumnNumber(9)); // 'l' on line 2
    }

    [Fact]
    public void GetLineAndColumn_ReturnsCorrectValues()
    {
        var sourceText = new SourceText("hello\nworld");

        var (line, column) = sourceText.GetLineAndColumn(9);

        Assert.Equal(2, line);
        Assert.Equal(4, column);
    }

    [Fact]
    public void GetPosition_ValidLineAndColumn_ReturnsCorrectPosition()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal(0, sourceText.GetPosition(1, 1)); // First character
        Assert.Equal(6, sourceText.GetPosition(2, 1)); // 'w'
        Assert.Equal(9, sourceText.GetPosition(2, 4)); // 'l'
    }

    [Fact]
    public void GetPosition_InvalidLine_ThrowsArgumentOutOfRangeException()
    {
        var sourceText = new SourceText("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetPosition(0, 1)); // Line 0
        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetPosition(2, 1)); // Line 2 doesn't exist
    }

    [Fact]
    public void GetPosition_InvalidColumn_ThrowsArgumentOutOfRangeException()
    {
        var sourceText = new SourceText("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetPosition(1, 0)); // Column 0
    }

    [Fact]
    public void GetLineText_FirstLine_ReturnsLineWithoutNewline()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal("hello", sourceText.GetLineText(1));
    }

    [Fact]
    public void GetLineText_LastLine_ReturnsLineContent()
    {
        var sourceText = new SourceText("hello\nworld");

        Assert.Equal("world", sourceText.GetLineText(2));
    }

    [Fact]
    public void GetLineText_InvalidLineNumber_ThrowsArgumentOutOfRangeException()
    {
        var sourceText = new SourceText("hello");

        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetLineText(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => sourceText.GetLineText(2));
    }

    [Fact]
    public void LineEndings_WindowsCRLF_HandledCorrectly()
    {
        var sourceText = new SourceText("hello\r\nworld");

        Assert.Equal(2, sourceText.LineCount);
        Assert.Equal("hello", sourceText.GetLineText(1));
        Assert.Equal("world", sourceText.GetLineText(2));
        Assert.Equal(2, sourceText.GetLineNumber(7)); // 'w' is on line 2
    }

    [Fact]
    public void LineEndings_MacCR_HandledCorrectly()
    {
        var sourceText = new SourceText("hello\rworld");

        Assert.Equal(2, sourceText.LineCount);
        Assert.Equal("hello", sourceText.GetLineText(1));
        Assert.Equal("world", sourceText.GetLineText(2));
    }

    [Fact]
    public void LineEndings_MixedLineEndings_HandledCorrectly()
    {
        var sourceText = new SourceText("line1\nline2\r\nline3\rline4");

        Assert.Equal(4, sourceText.LineCount);
        Assert.Equal("line1", sourceText.GetLineText(1));
        Assert.Equal("line2", sourceText.GetLineText(2));
        Assert.Equal("line3", sourceText.GetLineText(3));
        Assert.Equal("line4", sourceText.GetLineText(4));
    }

    [Fact]
    public void ToString_ReturnsOriginalText()
    {
        var text = "hello\nworld";
        var sourceText = new SourceText(text);

        Assert.Equal(text, sourceText.ToString());
    }

    [Fact]
    public void EmptyText_HandledCorrectly()
    {
        var sourceText = new SourceText("");

        Assert.Equal(0, sourceText.Length);
        Assert.Equal(1, sourceText.LineCount);
        Assert.Equal("", sourceText.GetLineText(1));
    }

    [Fact]
    public void GetLineNumber_AtEndOfFile_ReturnsLastLine()
    {
        var sourceText = new SourceText("hello");

        // Position at the end (after 'o') is still on line 1
        Assert.Equal(1, sourceText.GetLineNumber(5));
    }

    [Fact]
    public void RoundTrip_LineColumnToPositionAndBack()
    {
        var sourceText = new SourceText("def foo():\n    pass\n    return 42");

        // Test several positions
        for (int pos = 0; pos < sourceText.Length; pos++)
        {
            var (line, column) = sourceText.GetLineAndColumn(pos);
            int roundTripped = sourceText.GetPosition(line, column);
            Assert.Equal(pos, roundTripped);
        }
    }

    [Fact]
    public void WithChanges_SingleInsertion_InsertsText()
    {
        var sourceText = new SourceText("helloworld");
        var changes = new[] { new TextChange(new TextSpan(5, 0), " ") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("hello world", result.ToString());
    }

    [Fact]
    public void WithChanges_SingleDeletion_RemovesText()
    {
        var sourceText = new SourceText("hello world");
        var changes = new[] { new TextChange(new TextSpan(5, 1), "") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("helloworld", result.ToString());
    }

    [Fact]
    public void WithChanges_SingleReplacement_ReplacesText()
    {
        var sourceText = new SourceText("hello world");
        var changes = new[] { new TextChange(new TextSpan(6, 5), "there") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("hello there", result.ToString());
    }

    [Fact]
    public void WithChanges_MultipleNonOverlapping_AppliesAll()
    {
        var sourceText = new SourceText("aaa bbb ccc");
        var changes = new[]
        {
            new TextChange(new TextSpan(0, 3), "xxx"),
            new TextChange(new TextSpan(8, 3), "zzz"),
        };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("xxx bbb zzz", result.ToString());
    }

    [Fact]
    public void WithChanges_EmptyChanges_ReturnsEquivalentText()
    {
        var sourceText = new SourceText("hello", "/test.spy");
        var changes = Array.Empty<TextChange>();

        var result = sourceText.WithChanges(changes);

        Assert.Equal("hello", result.ToString());
        Assert.Equal("/test.spy", result.FilePath);
    }

    [Fact]
    public void WithChanges_AtDocumentStart_AppliesCorrectly()
    {
        var sourceText = new SourceText("hello");
        var changes = new[] { new TextChange(new TextSpan(0, 0), "say ") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("say hello", result.ToString());
    }

    [Fact]
    public void WithChanges_AtDocumentEnd_AppliesCorrectly()
    {
        var sourceText = new SourceText("hello");
        var changes = new[] { new TextChange(new TextSpan(5, 0), " world") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("hello world", result.ToString());
    }

    [Fact]
    public void WithChanges_InsertNewline_UpdatesLineCount()
    {
        var sourceText = new SourceText("helloworld");
        var changes = new[] { new TextChange(new TextSpan(5, 0), "\n") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("hello\nworld", result.ToString());
        Assert.Equal(2, result.LineCount);
    }

    [Fact]
    public void WithChanges_DeleteNewline_UpdatesLineCount()
    {
        var sourceText = new SourceText("hello\nworld");
        var changes = new[] { new TextChange(new TextSpan(5, 1), "") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("helloworld", result.ToString());
        Assert.Equal(1, result.LineCount);
    }

    [Fact]
    public void WithChanges_PreservesFilePath()
    {
        var sourceText = new SourceText("hello", "/path/to/file.spy");
        var changes = new[] { new TextChange(new TextSpan(0, 5), "world") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("/path/to/file.spy", result.FilePath);
    }

    [Fact]
    public void WithChanges_GetLineAndColumn_CorrectAfterChanges()
    {
        var sourceText = new SourceText("line1\nline2");
        // Insert a new line between the existing lines
        var changes = new[] { new TextChange(new TextSpan(6, 0), "new\n") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("line1\nnew\nline2", result.ToString());
        Assert.Equal(3, result.LineCount);
        // 'l' in "line2" is now at position 10
        var (line, column) = result.GetLineAndColumn(10);
        Assert.Equal(3, line);
        Assert.Equal(1, column);
    }

    [Fact]
    public void WithChanges_ReplacementWithDifferentLength_UpdatesCorrectly()
    {
        var sourceText = new SourceText("ab\ncd\nef");
        // Replace "cd" with "longer text"
        var changes = new[] { new TextChange(new TextSpan(3, 2), "longer text") };

        var result = sourceText.WithChanges(changes);

        Assert.Equal("ab\nlonger text\nef", result.ToString());
        Assert.Equal(3, result.LineCount);
        Assert.Equal("longer text", result.GetLineText(2));
    }
}
