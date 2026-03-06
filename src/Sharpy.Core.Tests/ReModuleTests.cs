using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class ReModuleTests
{
    // ===== Search =====

    [Fact]
    public void Search_FindsMatchAnywhere()
    {
        var m = Re.Search(@"\d+", "abc 123 def");
        m.Should().NotBeNull();
        m!.Group().Should().Be("123");
    }

    [Fact]
    public void Search_NoMatch_ReturnsNull()
    {
        var m = Re.Search(@"\d+", "no digits");
        m.Should().BeNull();
    }

    [Fact]
    public void Search_WithGroups_CapturesGroups()
    {
        var m = Re.Search(@"(\w+)@(\w+)", "user@host");
        m.Should().NotBeNull();
        m!.Group(0).Should().Be("user@host");
        m.Group(1).Should().Be("user");
        m.Group(2).Should().Be("host");
    }

    [Fact]
    public void Search_Groups_ReturnsList()
    {
        var m = Re.Search(@"(\w+)@(\w+)", "user@host");
        var groups = m!.Groups();
        groups[0].Should().Be("user");
        groups[1].Should().Be("host");
    }

    // ===== Match =====

    [Fact]
    public void Match_MatchesAtStart()
    {
        var m = Re.Match("hello", "hello world");
        m.Should().NotBeNull();
        m!.Group().Should().Be("hello");
    }

    [Fact]
    public void Match_NoMatchIfNotAtStart()
    {
        var m = Re.Match("world", "hello world");
        m.Should().BeNull();
    }

    // ===== Fullmatch =====

    [Fact]
    public void Fullmatch_MatchesEntireString()
    {
        var m = Re.Fullmatch("hello", "hello");
        m.Should().NotBeNull();
    }

    [Fact]
    public void Fullmatch_NoMatchIfPartial()
    {
        var m = Re.Fullmatch("hello", "hello world");
        m.Should().BeNull();
    }

    // ===== Findall =====

    [Fact]
    public void Findall_FindsAllMatches()
    {
        var result = Re.Findall(@"\d+", "abc 123 def 456");
        result[0].Should().Be("123");
        result[1].Should().Be("456");
    }

    // ===== Sub =====

    [Fact]
    public void Sub_ReplacesAllOccurrences()
    {
        var result = Re.Sub(@"\d+", "X", "abc 123 def 456");
        result.Should().Be("abc X def X");
    }

    [Fact]
    public void Sub_WithCount_LimitsReplacements()
    {
        var result = Re.Sub(@"\d+", "X", "a1b2c3", count: 2);
        result.Should().Be("aXbXc3");
    }

    // ===== Split =====

    [Fact]
    public void Split_SplitsByPattern()
    {
        var result = Re.Split(@"\s+", "one two three");
        result[0].Should().Be("one");
        result[1].Should().Be("two");
        result[2].Should().Be("three");
    }

    [Fact]
    public void Split_WithMaxsplit_LimitsSplits()
    {
        var result = Re.Split(@"\d+", "a1b2c3", maxsplit: 1);
        result[0].Should().Be("a");
        result[1].Should().Be("b2c3");
    }

    // ===== Named groups =====

    [Fact]
    public void Search_NamedGroups_PythonSyntax()
    {
        var m = Re.Search(@"(?P<name>\w+)@(?P<domain>\w+)", "user@host");
        m.Should().NotBeNull();
        m!.Group("name").Should().Be("user");
        m.Group("domain").Should().Be("host");
    }

    [Fact]
    public void Search_Groupdict_ReturnsNamedGroups()
    {
        var m = Re.Search(@"(?P<name>\w+)@(?P<domain>\w+)", "user@host");
        var gd = m!.Groupdict();
        gd["name"].Should().Be("user");
        gd["domain"].Should().Be("host");
    }

    // ===== Position methods =====

    [Fact]
    public void Match_StartEnd_ReturnPositions()
    {
        var m = Re.Search(@"\d+", "abc 123 def");
        m.Should().NotBeNull();
        m!.Start().Should().Be(4);
        m.End().Should().Be(7);
    }

    [Fact]
    public void Match_Span_ReturnsTuple()
    {
        var m = Re.Search(@"\d+", "abc 123 def");
        var span = m!.Span();
        span.Item1.Should().Be(4);
        span.Item2.Should().Be(7);
    }

    // ===== Compile =====

    [Fact]
    public void Compile_ReturnsReusablePattern()
    {
        var p = Re.Compile(@"\d+");
        p.Findall("abc 123 def 456")[0].Should().Be("123");
        p.Search("abc 123")!.Group().Should().Be("123");
    }

    // ===== Escape =====

    [Fact]
    public void Escape_EscapesSpecialChars()
    {
        var result = Re.Escape("a.b*c");
        result.Should().Contain(@"\.");
        result.Should().Contain(@"\*");
    }

    // ===== Flags =====

    [Fact]
    public void Search_IgnoreCase_MatchesCaseInsensitive()
    {
        var m = Re.Search("hello", "HELLO", Re.IGNORECASE);
        m.Should().NotBeNull();
        m!.Group().Should().Be("HELLO");
    }

    [Fact]
    public void Search_Multiline_MatchesStartOfLine()
    {
        var m = Re.Search(@"^\w+", "hello\nworld", Re.MULTILINE);
        m.Should().NotBeNull();
    }

    // ===== Finditer =====

    [Fact]
    public void Finditer_YieldsMatchObjects()
    {
        var matches = new System.Collections.Generic.List<ReMatch>();
        foreach (var m in Re.Finditer(@"\d+", "a1b2c3"))
        {
            matches.Add(m);
        }
        matches.Count.Should().Be(3);
        matches[0].Group().Should().Be("1");
        matches[1].Group().Should().Be("2");
        matches[2].Group().Should().Be("3");
    }

    // ===== Pattern methods =====

    [Fact]
    public void Pattern_Match_MatchesAtStart()
    {
        var p = Re.Compile("hello");
        p.Match("hello world").Should().NotBeNull();
        p.Match("say hello").Should().BeNull();
    }

    [Fact]
    public void Pattern_Fullmatch_MatchesEntire()
    {
        var p = Re.Compile("hello");
        p.Fullmatch("hello").Should().NotBeNull();
        p.Fullmatch("hello world").Should().BeNull();
    }

    [Fact]
    public void Pattern_Sub_Replaces()
    {
        var p = Re.Compile(@"\d+");
        p.Sub("X", "a1b2c3").Should().Be("aXbXcX");
    }

    [Fact]
    public void Pattern_Split_Splits()
    {
        var p = Re.Compile(@"\s+");
        var result = p.Split("one two three");
        result[0].Should().Be("one");
        result[1].Should().Be("two");
        result[2].Should().Be("three");
    }
}
