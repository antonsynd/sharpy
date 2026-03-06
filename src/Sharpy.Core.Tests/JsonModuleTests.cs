using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class JsonModuleTests
{
    // ===== Dumps basic types =====

    [Fact]
    public void Dumps_Null_ReturnsNull()
    {
        Json.Dumps(null).Should().Be("null");
    }

    [Fact]
    public void Dumps_True_ReturnsTrue()
    {
        Json.Dumps(true).Should().Be("true");
    }

    [Fact]
    public void Dumps_False_ReturnsFalse()
    {
        Json.Dumps(false).Should().Be("false");
    }

    [Fact]
    public void Dumps_Integer_ReturnsNumber()
    {
        Json.Dumps(42).Should().Be("42");
    }

    [Fact]
    public void Dumps_Long_ReturnsNumber()
    {
        Json.Dumps(9999999999L).Should().Be("9999999999");
    }

    [Fact]
    public void Dumps_Double_ReturnsNumber()
    {
        Json.Dumps(3.14).Should().Be("3.14");
    }

    [Fact]
    public void Dumps_String_ReturnsQuotedString()
    {
        Json.Dumps("hello").Should().Be("\"hello\"");
    }

    [Fact]
    public void Dumps_StringWithEscapes_EscapesCorrectly()
    {
        Json.Dumps("a\"b\\c\n").Should().Be("\"a\\\"b\\\\c\\n\"");
    }

    // ===== Dumps collections =====

    [Fact]
    public void Dumps_EmptyDict_ReturnsBraces()
    {
        var d = new Dict<string, object?>();
        Json.Dumps(d).Should().Be("{}");
    }

    [Fact]
    public void Dumps_Dict_ReturnsObject()
    {
        var d = new Dict<string, object?>();
        d["a"] = 1;
        d["b"] = "hello";
        var result = Json.Dumps(d);
        result.Should().Contain("\"a\": 1");
        result.Should().Contain("\"b\": \"hello\"");
    }

    [Fact]
    public void Dumps_EmptyList_ReturnsBrackets()
    {
        var l = new List<object?>();
        Json.Dumps(l).Should().Be("[]");
    }

    [Fact]
    public void Dumps_List_ReturnsArray()
    {
        var l = new List<object?>();
        l.Append(1);
        l.Append("two");
        l.Append(true);
        l.Append(null);
        Json.Dumps(l).Should().Be("[1, \"two\", true, null]");
    }

    // ===== Dumps options =====

    [Fact]
    public void Dumps_WithIndent_FormatsOutput()
    {
        var d = new Dict<string, object?>();
        d["a"] = 1;
        var result = Json.Dumps(d, indent: 2);
        result.Should().Be("{\n  \"a\": 1\n}");
    }

    [Fact]
    public void Dumps_WithSortKeys_SortsKeys()
    {
        var d = new Dict<string, object?>();
        d["b"] = 2;
        d["a"] = 1;
        var result = Json.Dumps(d, sortKeys: true);
        result.Should().Be("{\"a\": 1, \"b\": 2}");
    }

    [Fact]
    public void Dumps_WithEnsureAscii_EscapesNonAscii()
    {
        var result = Json.Dumps("caf\u00e9", ensureAscii: true);
        result.Should().Be("\"caf\\u00e9\"");
    }

    [Fact]
    public void Dumps_WithoutEnsureAscii_PreservesUnicode()
    {
        var result = Json.Dumps("caf\u00e9", ensureAscii: false);
        result.Should().Be("\"caf\u00e9\"");
    }

    // ===== Dumps errors =====

    [Fact]
    public void Dumps_Infinity_ThrowsValueError()
    {
        var act = () => Json.Dumps(double.PositiveInfinity);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Dumps_NaN_ThrowsValueError()
    {
        var act = () => Json.Dumps(double.NaN);
        act.Should().Throw<ValueError>();
    }

    // ===== Loads basic types =====

    [Fact]
    public void Loads_Null_ReturnsNull()
    {
        Json.Loads("null").Should().BeNull();
    }

    [Fact]
    public void Loads_True_ReturnsTrue()
    {
        Json.Loads("true").Should().Be(true);
    }

    [Fact]
    public void Loads_False_ReturnsFalse()
    {
        Json.Loads("false").Should().Be(false);
    }

    [Fact]
    public void Loads_Integer_ReturnsInt()
    {
        var result = Json.Loads("42");
        result.Should().Be(42);
        result.Should().BeOfType<int>();
    }

    [Fact]
    public void Loads_Float_ReturnsDouble()
    {
        var result = Json.Loads("3.14");
        result.Should().Be(3.14);
        result.Should().BeOfType<double>();
    }

    [Fact]
    public void Loads_LargeInt_ReturnsLong()
    {
        var result = Json.Loads("9999999999");
        result.Should().Be(9999999999L);
        result.Should().BeOfType<long>();
    }

    [Fact]
    public void Loads_String_ReturnsString()
    {
        Json.Loads("\"hello\"").Should().Be("hello");
    }

    [Fact]
    public void Loads_StringWithEscapes_UnescapesCorrectly()
    {
        Json.Loads("\"a\\\"b\\\\c\\n\"").Should().Be("a\"b\\c\n");
    }

    [Fact]
    public void Loads_UnicodeEscape_DecodesCorrectly()
    {
        Json.Loads("\"caf\\u00e9\"").Should().Be("caf\u00e9");
    }

    // ===== Loads collections =====

    [Fact]
    public void Loads_EmptyObject_ReturnsEmptyDict()
    {
        var result = Json.Loads("{}");
        result.Should().BeOfType<Dict<string, object?>>();
    }

    [Fact]
    public void Loads_Object_ReturnsDictWithValues()
    {
        var result = (Dict<string, object?>)Json.Loads("{\"a\": 1, \"b\": \"hello\"}");
        result["a"].Should().Be(1);
        result["b"].Should().Be("hello");
    }

    [Fact]
    public void Loads_EmptyArray_ReturnsEmptyList()
    {
        var result = Json.Loads("[]");
        result.Should().BeOfType<List<object?>>();
    }

    [Fact]
    public void Loads_Array_ReturnsListWithValues()
    {
        var result = (List<object?>)Json.Loads("[1, \"two\", true, null]");
        result[0].Should().Be(1);
        result[1].Should().Be("two");
        result[2].Should().Be(true);
        result[3].Should().BeNull();
    }

    // ===== Loads nested =====

    [Fact]
    public void Loads_NestedObject_ParsesCorrectly()
    {
        var result = (Dict<string, object?>)Json.Loads("{\"a\": {\"b\": [1, 2]}}");
        var inner = (Dict<string, object?>)result["a"];
        var list = (List<object?>)inner["b"];
        list[0].Should().Be(1);
        list[1].Should().Be(2);
    }

    // ===== Loads errors =====

    [Fact]
    public void Loads_InvalidJson_ThrowsJsonDecodeError()
    {
        var act = () => Json.Loads("invalid");
        act.Should().Throw<JsonDecodeError>();
    }

    [Fact]
    public void Loads_ExtraData_ThrowsJsonDecodeError()
    {
        var act = () => Json.Loads("1 2");
        act.Should().Throw<JsonDecodeError>().WithMessage("*Extra data*");
    }

    [Fact]
    public void Loads_UnterminatedString_ThrowsJsonDecodeError()
    {
        var act = () => Json.Loads("\"hello");
        act.Should().Throw<JsonDecodeError>();
    }

    // ===== Round-trip =====

    [Fact]
    public void RoundTrip_NestedStructure_PreservesData()
    {
        var d = new Dict<string, object?>();
        var inner = new List<object?>();
        inner.Append(1);
        inner.Append("two");
        inner.Append(true);
        inner.Append(null);
        d["list"] = inner;
        d["number"] = 42;

        var json = Json.Dumps(d);
        var parsed = (Dict<string, object?>)Json.Loads(json);
        parsed["number"].Should().Be(42);
        var parsedList = (List<object?>)parsed["list"];
        parsedList[0].Should().Be(1);
        parsedList[1].Should().Be("two");
        parsedList[2].Should().Be(true);
        parsedList[3].Should().BeNull();
    }

    // ===== JsonDecodeError properties =====

    [Fact]
    public void JsonDecodeError_HasCorrectProperties()
    {
        try
        {
            Json.Loads("invalid");
        }
        catch (JsonDecodeError ex)
        {
            ex.Msg.Should().Be("Expecting value");
            ex.Doc.Should().Be("invalid");
            ex.Pos.Should().Be(0);
            return;
        }
        Assert.Fail("Expected JsonDecodeError");
    }

    [Fact]
    public void JsonDecodeError_IsSubclassOfValueError()
    {
        var ex = new JsonDecodeError("test", "doc", 0);
        (ex is ValueError).Should().BeTrue();
    }
}
