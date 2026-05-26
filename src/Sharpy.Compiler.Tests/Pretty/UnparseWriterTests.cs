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
}
