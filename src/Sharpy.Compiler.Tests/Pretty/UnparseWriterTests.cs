using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;

namespace Sharpy.Compiler.Tests.PrettyTests;

public class UnparseWriterTests
{
    private static UnparseWriter CreateWriter(string indent = "    ", string lineEnding = "\n") =>
        new(new UnparseOptions { IndentString = indent, LineEnding = lineEnding });

    [Fact]
    public void Write_NoIndent_AppendsDirectly()
    {
        var writer = CreateWriter();
        writer.Write("hello");
        writer.ToString().Should().Be("hello");
    }

    [Fact]
    public void WriteLine_AppendsLineEnding()
    {
        var writer = CreateWriter();
        writer.WriteLine("hello");
        writer.ToString().Should().Be("hello\n");
    }

    [Fact]
    public void Indent_AddsIndentation()
    {
        var writer = CreateWriter();
        writer.WriteLine("line1");
        writer.Indent();
        writer.WriteLine("line2");
        writer.Dedent();
        writer.WriteLine("line3");

        writer.ToString().Should().Be("line1\n    line2\nline3\n");
    }

    [Fact]
    public void NestedIndent_AccumulatesLevels()
    {
        var writer = CreateWriter();
        writer.Indent();
        writer.Indent();
        writer.WriteLine("deep");
        writer.Dedent();
        writer.WriteLine("shallow");

        writer.ToString().Should().Be("        deep\n    shallow\n");
    }

    [Fact]
    public void CustomIndent_UsesSpecifiedString()
    {
        var writer = CreateWriter(indent: "\t");
        writer.Indent();
        writer.Write("hello");

        writer.ToString().Should().Be("\thello");
    }

    [Fact]
    public void CustomLineEnding_UsesSpecifiedString()
    {
        var writer = CreateWriter(lineEnding: "\r\n");
        writer.WriteLine("hello");

        writer.ToString().Should().Be("hello\r\n");
    }

    [Fact]
    public void Length_TracksWrittenCharacters()
    {
        var writer = CreateWriter();
        writer.Write("abc");
        writer.Length.Should().Be(3);

        writer.WriteLine();
        writer.Length.Should().Be(4); // abc + \n
    }

    [Fact]
    public void EmptyWriteLine_OnlyAppendsLineEnding()
    {
        var writer = CreateWriter();
        writer.WriteLine();
        writer.ToString().Should().Be("\n");
    }

    // ---- EnsureBlankLines (P50, R-ET #2239) ----

    [Theory]
    [InlineData(0, 1, "a\n\n")]
    [InlineData(0, 2, "a\n\n\n")]
    [InlineData(1, 1, "a\n\n")]
    [InlineData(1, 2, "a\n\n\n")]
    [InlineData(2, 1, "a\n\n\n")]
    [InlineData(2, 2, "a\n\n\n")]
    public void EnsureBlankLines_TopsUpTheTrailingBlankLinesToTheCount_NeverAddsPastIt(int existing, int count, string expected)
    {
        var writer = CreateWriter();
        writer.WriteLine("a");
        for (var i = 0; i < existing; i++)
            writer.WriteLine();

        writer.EnsureBlankLines(count);

        writer.ToString().Should().Be(expected);
        writer.AtLineStart.Should().BeTrue();
    }

    [Fact]
    public void EnsureBlankLines_OnAnEmptyBufferOrMidLine_WritesNothing()
    {
        var empty = CreateWriter();
        empty.EnsureBlankLines(2);
        empty.ToString().Should().BeEmpty("no blank line is ever written at the start of the output");

        var midLine = CreateWriter();
        midLine.Write("a");
        midLine.EnsureBlankLines(1);
        midLine.ToString().Should().Be("a", "a blank-line run is only ever written at a line start");
    }

    /// <summary>
    /// A block expression's last line break also ends its host's line, and the host's terminator is
    /// absorbed (#2227). A blank line ensured in between is a real line — never the absorbed terminator —
    /// and the terminator stays absorbed, so exactly one blank line separates the block from what follows.
    /// </summary>
    [Fact]
    public void EnsureBlankLines_AfterABlockExpression_IsARealLine_AndTheHostTerminatorStaysAbsorbed()
    {
        var writer = CreateWriter();
        writer.WriteLine("s = match n:");
        writer.Indent();
        writer.WriteLine("case _: 1");
        writer.Dedent();
        writer.EndLineByBlockExpression();

        writer.EnsureBlankLines(1);
        writer.WriteLine();
        writer.Write("x");

        writer.ToString().Should().Be("s = match n:\n    case _: 1\n\nx");
    }

    [Fact]
    public void EnsureBlankLines_CountsAndWritesTheWritersLineEnding()
    {
        var writer = CreateWriter(lineEnding: "\r\n");
        writer.WriteLine("a");
        writer.WriteLine();

        writer.EnsureBlankLines(2);

        writer.ToString().Should().Be("a\r\n\r\n\r\n");
    }
}
