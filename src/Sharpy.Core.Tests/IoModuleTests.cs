using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class IoModuleTests
{
    [Fact]
    public void Write_ReturnsLengthWritten()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("hello").Should().Be(5);
    }

    [Fact]
    public void WriteRead_Cycle()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("hello world");
        sio.Seek(0);
        sio.Read().Should().Be("hello world");
    }

    [Fact]
    public void Read_WithCount()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("hello world");
        sio.Seek(0);
        sio.Read(5).Should().Be("hello");
        sio.Read(1).Should().Be(" ");
        sio.Read().Should().Be("world");
    }

    [Fact]
    public void Readline_ReadsUntilNewline()
    {
        var sio = new Sharpy.StringIO("line1\nline2\nline3");
        sio.Readline().Should().Be("line1\n");
        sio.Readline().Should().Be("line2\n");
        sio.Readline().Should().Be("line3");
    }

    [Fact]
    public void Readline_AtEnd_ReturnsEmpty()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Read();
        sio.Readline().Should().Be("");
    }

    [Fact]
    public void Seek_SetsPosition()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Seek(2).Should().Be(2);
        sio.Read(1).Should().Be("l");
    }

    [Fact]
    public void Tell_ReturnsCurrentPosition()
    {
        var sio = new Sharpy.StringIO();
        sio.Tell().Should().Be(0);
        sio.Write("hello");
        sio.Tell().Should().Be(5);
    }

    [Fact]
    public void Getvalue_ReturnsEntireContent()
    {
        var sio = new Sharpy.StringIO();
        sio.Write("hello");
        sio.Write(" world");
        sio.Getvalue().Should().Be("hello world");
    }

    [Fact]
    public void Truncate_AtCurrentPosition()
    {
        var sio = new Sharpy.StringIO("hello world");
        sio.Seek(5);
        sio.Truncate();
        sio.Getvalue().Should().Be("hello");
    }

    [Fact]
    public void Truncate_AtSpecificSize()
    {
        var sio = new Sharpy.StringIO("hello world");
        sio.Truncate(3);
        sio.Getvalue().Should().Be("hel");
    }

    [Fact]
    public void Close_PreventsOperations()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Close();

        var act = () => sio.Read();
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void Dispose_ClosesStream()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Dispose();

        var act = () => sio.Write("x");
        act.Should().Throw<Sharpy.ValueError>();
    }

    [Fact]
    public void InitialContent_IsReadable()
    {
        var sio = new Sharpy.StringIO("initial");
        sio.Read().Should().Be("initial");
    }

    [Fact]
    public void Write_OverwritesAtPosition()
    {
        var sio = new Sharpy.StringIO("hello");
        sio.Seek(0);
        sio.Write("HE");
        sio.Getvalue().Should().Be("HEllo");
    }

    [Fact]
    public void Seek_NegativeThrows()
    {
        var sio = new Sharpy.StringIO();
        var act = () => sio.Seek(-1);
        act.Should().Throw<Sharpy.ValueError>();
    }
}
