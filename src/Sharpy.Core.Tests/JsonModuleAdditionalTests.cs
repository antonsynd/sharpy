using System;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional Json tests not covered by JsonModuleTests.cs (92 tests).
/// Focuses on gaps: numeric boundaries, whitespace-only input,
/// additional collection types, and pretty-print array formatting.
/// </summary>
public class JsonModuleAdditionalTests
{
    // --- Loads numeric boundaries ---

    [Fact]
    public void Loads_IntMaxValue_ReturnsInt()
    {
        // 2147483647 fits in int
        object? result = Sharpy.Json.Loads("2147483647");
        Assert.IsType<int>(result);
        Assert.Equal(2147483647, result);
    }

    [Fact]
    public void Loads_IntMaxValuePlusOne_ReturnsLong()
    {
        // 2147483648 doesn't fit in int, should be long
        object? result = Sharpy.Json.Loads("2147483648");
        Assert.IsType<long>(result);
        Assert.Equal(2147483648L, result);
    }

    [Fact]
    public void Loads_NegativeFloat_ReturnsDouble()
    {
        object? result = Sharpy.Json.Loads("-3.14");
        Assert.IsType<double>(result);
        ((double)result!).Should().BeApproximately(-3.14, 1e-10);
    }

    [Fact]
    public void Loads_NegativeScientific_ReturnsDouble()
    {
        object? result = Sharpy.Json.Loads("-1.5e2");
        Assert.IsType<double>(result);
        Assert.Equal(-150.0, result);
    }

    // --- Loads whitespace edge cases ---

    [Fact]
    public void Loads_WhitespaceOnly_ThrowsJSONDecodeError()
    {
        Assert.Throws<Sharpy.JSONDecodeError>(() => Sharpy.Json.Loads("   "));
    }

    [Fact]
    public void Loads_WithLeadingAndTrailingWhitespace_ParsesObject()
    {
        object? result = Sharpy.Json.Loads("  {\"key\": 1}  ");
        var dict = Assert.IsType<Sharpy.Dict<string, object?>>(result);
        Assert.Equal(1, dict["key"]);
    }

    // --- Dumps: additional collection types ---

    [Fact]
    public void Dumps_SetOfInt_ReturnsJsonArray()
    {
        var s = new Sharpy.Set<int>();
        s.Add(42);
        string result = Sharpy.Json.Dumps(s);
        result.Should().Be("[42]");
    }

    [Fact]
    public void Dumps_ListOfString_ReturnsJsonArray()
    {
        var l = new Sharpy.List<string>();
        l.Append("hello");
        l.Append("world");
        string result = Sharpy.Json.Dumps(l);
        result.Should().Be("[\"hello\", \"world\"]");
    }

    // --- Dumps: pretty-print for arrays ---

    [Fact]
    public void Dumps_ListWithIndent_PrettyPrints()
    {
        var l = new Sharpy.List<object?>();
        l.Append(1);
        l.Append(2);
        l.Append(3);
        string result = Sharpy.Json.Dumps(l, indent: 2);
        string expected = "[\n  1,\n  2,\n  3\n]";
        result.Should().Be(expected);
    }

    [Fact]
    public void Dumps_NestedListWithIndent_PrettyPrints()
    {
        var outer = new Sharpy.Dict<string, object?>();
        var inner = new Sharpy.List<object?>();
        inner.Append("a");
        inner.Append("b");
        outer["items"] = inner;
        string result = Sharpy.Json.Dumps(outer, indent: 2);
        result.Should().Contain("\n");
        result.Should().Contain("\"items\"");
        result.Should().Contain("\"a\"");
    }

    // --- Round-trips for additional types ---

    [Fact]
    public void RoundTrip_LargeInteger_PreservesValue()
    {
        long value = 9876543210L;
        string json = Sharpy.Json.Dumps(value);
        object? parsed = Sharpy.Json.Loads(json);
        parsed.Should().Be(value);
    }

    [Fact]
    public void RoundTrip_NegativeInt_PreservesValue()
    {
        string json = Sharpy.Json.Dumps(-999);
        object? parsed = Sharpy.Json.Loads(json);
        parsed.Should().Be(-999);
    }

    [Fact]
    public void RoundTrip_EmptyString_PreservesValue()
    {
        string json = Sharpy.Json.Dumps("");
        object? parsed = Sharpy.Json.Loads(json);
        parsed.Should().Be("");
    }
}
