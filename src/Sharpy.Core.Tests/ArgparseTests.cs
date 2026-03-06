using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class ArgparseTests
{
    // ===== Positional arguments =====

    [Fact]
    public void ParseArgs_PositionalArg_Parsed()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("name");
        var ns = p.ParseArgs(new List<string>(new[] { "hello" }));
        ns.Get("name").Should().Be("hello");
    }

    [Fact]
    public void ParseArgs_MultiplePositionals_ParsedInOrder()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("first");
        p.AddArgument("second");
        var ns = p.ParseArgs(new List<string>(new[] { "a", "b" }));
        ns.Get("first").Should().Be("a");
        ns.Get("second").Should().Be("b");
    }

    // ===== Optional arguments =====

    [Fact]
    public void ParseArgs_LongFlag_Parsed()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--name");
        var ns = p.ParseArgs(new List<string>(new[] { "--name", "world" }));
        ns.Get("name").Should().Be("world");
    }

    [Fact]
    public void ParseArgs_ShortAndLongFlag_Parsed()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument(shortFlag: "-n", longFlag: "--name");
        var ns = p.ParseArgs(new List<string>(new[] { "-n", "world" }));
        ns.Get("name").Should().Be("world");
    }

    [Fact]
    public void ParseArgs_DefaultValue_Used()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--count", @default: 5);
        var ns = p.ParseArgs(new List<string>());
        ns.Get("count").Should().Be(5);
    }

    // ===== Actions =====

    [Fact]
    public void ParseArgs_StoreTrue_SetsTrue()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument(shortFlag: "-v", longFlag: "--verbose", action: "store_true");
        var ns = p.ParseArgs(new List<string>(new[] { "-v" }));
        ns.Get("verbose").Should().Be(true);
    }

    [Fact]
    public void ParseArgs_StoreTrue_DefaultFalse()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument(shortFlag: "-v", longFlag: "--verbose", action: "store_true");
        var ns = p.ParseArgs(new List<string>());
        ns.Get("verbose").Should().Be(false);
    }

    [Fact]
    public void ParseArgs_StoreFalse_SetsFalse()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--no-debug", action: "store_false");
        var ns = p.ParseArgs(new List<string>(new[] { "--no-debug" }));
        ns.Get("no_debug").Should().Be(false);
    }

    [Fact]
    public void ParseArgs_Count_IncrementsCount()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument(shortFlag: "-v", longFlag: "--verbose", action: "count");
        var ns = p.ParseArgs(new List<string>(new[] { "-v", "-v", "-v" }));
        ns.Get("verbose").Should().Be(3);
    }

    // ===== Type conversion =====

    [Fact]
    public void ParseArgs_TypeInt_ConvertsToInt()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--count", type: s => (object)int.Parse(s));
        var ns = p.ParseArgs(new List<string>(new[] { "--count", "5" }));
        ns.Get("count").Should().Be(5);
    }

    // ===== Mixed positional and optional =====

    [Fact]
    public void ParseArgs_MixedArgs_ParsedCorrectly()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("filename");
        p.AddArgument(shortFlag: "-v", longFlag: "--verbose", action: "store_true");
        p.AddArgument("--count", @default: 1, type: s => (object)int.Parse(s));
        var ns = p.ParseArgs(new List<string>(new[] { "myfile.txt", "-v", "--count", "3" }));
        ns.Get("filename").Should().Be("myfile.txt");
        ns.Get("verbose").Should().Be(true);
        ns.Get("count").Should().Be(3);
    }

    // ===== Namespace =====

    [Fact]
    public void Namespace_Contains_ReturnsTrueForExisting()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("name");
        var ns = p.ParseArgs(new List<string>(new[] { "hello" }));
        ns.Contains("name").Should().BeTrue();
        ns.Contains("missing").Should().BeFalse();
    }

    [Fact]
    public void Namespace_Indexer_Works()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("name");
        var ns = p.ParseArgs(new List<string>(new[] { "hello" }));
        ns["name"].Should().Be("hello");
    }

    [Fact]
    public void Namespace_GetTyped_Works()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--count", type: s => (object)int.Parse(s));
        var ns = p.ParseArgs(new List<string>(new[] { "--count", "5" }));
        ns.Get<int>("count").Should().Be(5);
    }

    [Fact]
    public void Namespace_ToString_MatchesPythonFormat()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("name");
        p.AddArgument(shortFlag: "-v", longFlag: "--verbose", action: "store_true");
        var ns = p.ParseArgs(new List<string>(new[] { "hello", "-v" }));
        var str = ns.ToString();
        str.Should().StartWith("Namespace(");
        str.Should().EndWith(")");
        str.Should().Contain("name='hello'");
        str.Should().Contain("verbose=True");
    }

    [Fact]
    public void Namespace_Get_ThrowsAttributeError()
    {
        var p = new ArgumentParser(description: "test");
        var ns = p.ParseArgs(new List<string>());
        var act = () => ns.Get("nonexistent");
        act.Should().Throw<AttributeError>();
    }

    // ===== Append action =====

    [Fact]
    public void ParseArgs_Append_CollectsValues()
    {
        var p = new ArgumentParser(description: "test");
        p.AddArgument("--item", action: "append");
        var ns = p.ParseArgs(new List<string>(new[] { "--item", "a", "--item", "b" }));
        var items = (List<object?>)ns.Get("item");
        items[0].Should().Be("a");
        items[1].Should().Be("b");
    }
}
