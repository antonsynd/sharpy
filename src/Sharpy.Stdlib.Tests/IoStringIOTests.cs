using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional StringIO tests not covered by IoModuleTests.cs (15 tests).
/// </summary>
public class IoStringIOTests
{
    [Fact]
    public void EmptyConstructor_GetvalueIsEmpty()
    {
        var sio = new Sharpy.StringIO();

        sio.Getvalue().Should().Be("");
    }

    [Fact]
    public void EmptyConstructor_ReadReturnsEmpty()
    {
        var sio = new Sharpy.StringIO();

        sio.Read().Should().Be("");
    }

    [Fact]
    public void EmptyConstructor_TellIsZero()
    {
        var sio = new Sharpy.StringIO();

        sio.Tell().Should().Be(0);
    }

    [Fact]
    public void InitialContent_TellIsAtStart()
    {
        var sio = new Sharpy.StringIO("hello");

        // Position starts at 0 for initial content
        sio.Tell().Should().Be(0);
    }

    [Fact]
    public void Write_SequentialWrites_Accumulate()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("foo");
        sio.Write("bar");

        sio.Getvalue().Should().Be("foobar");
    }

    [Fact]
    public void Write_ReturnsCharacterCount()
    {
        var sio = new Sharpy.StringIO();

        sio.Write("hello world").Should().Be(11);
    }

    [Fact]
    public void Write_EmptyString_ReturnsZero()
    {
        var sio = new Sharpy.StringIO();

        sio.Write("").Should().Be(0);
    }

    [Fact]
    public void Seek_ToZero_AllowsFullRead()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("hello");
        sio.Seek(0);

        sio.Read().Should().Be("hello");
    }

    [Fact]
    public void Seek_ReturnsNewPosition()
    {
        var sio = new Sharpy.StringIO("hello world");

        sio.Seek(6).Should().Be(6);
    }

    [Fact]
    public void Tell_AfterRead_ReflectsPosition()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Read(3);

        sio.Tell().Should().Be(3);
    }

    [Fact]
    public void Readline_NoNewline_ReadsToEnd()
    {
        var sio = new Sharpy.StringIO("no newline here");

        sio.Readline().Should().Be("no newline here");
    }

    [Fact]
    public void Readline_MultipleCallsAdvanceThroughContent()
    {
        var sio = new Sharpy.StringIO("line1\nline2\nline3");

        sio.Readline().Should().Be("line1\n");
        sio.Readline().Should().Be("line2\n");
        sio.Readline().Should().Be("line3");
        sio.Readline().Should().Be("");
    }

    [Fact]
    public void Truncate_Zero_ClearsContent()
    {
        var sio = new Sharpy.StringIO("hello world");
        sio.Truncate(0);

        sio.Getvalue().Should().Be("");
    }

    [Fact]
    public void Truncate_ReturnsNewSize()
    {
        var sio = new Sharpy.StringIO("hello world");

        sio.Truncate(5).Should().Be(5);
    }

    [Fact]
    public void WriteAfterSeekToMiddle_OverwritesChars()
    {
        var sio = new Sharpy.StringIO("hello world");
        sio.Seek(6);
        sio.Write("earth");

        sio.Getvalue().Should().Be("hello earth");
    }

    [Fact]
    public void Getvalue_RegardlessOfPosition_ReturnsAll()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("full content");
        // Do not seek back to 0
        sio.Tell().Should().Be(12);

        // Getvalue should still return everything
        sio.Getvalue().Should().Be("full content");
    }

    [Fact]
    public void Seek_ToEnd_ReadReturnsEmpty()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Seek(5);

        sio.Read().Should().Be("");
    }

    [Fact]
    public void Read_PartialRead_AdvancesPosition()
    {
        var sio = new Sharpy.StringIO("abcdef");
        sio.Read(2);

        sio.Tell().Should().Be(2);
        sio.Read(2).Should().Be("cd");
        sio.Tell().Should().Be(4);
    }

    [Fact]
    public void Close_GetvalueThrowsValueError()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Close();

        var act = () => sio.Getvalue();
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Close_SeekThrowsValueError()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Close();

        var act = () => sio.Seek(0);
        act.Should().Throw<Sharpy.ValueError>();
    }
}
