using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class PprintTests
{
    // ----- Basic scalar formatting -----

    [Fact]
    public void Pformat_Integer_ReturnsDigits()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(42).Should().Be("42");
    }

    [Fact]
    public void Pformat_String_ReturnsPythonRepr()
    {
        var pp = new PrettyPrinter();
        pp.Pformat("hello").Should().Be("'hello'");
    }

    [Fact]
    public void Pformat_Null_ReturnsNone()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(null).Should().Be("None");
    }

    [Fact]
    public void Pformat_True_ReturnsTrue()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(true).Should().Be("True");
    }

    [Fact]
    public void Pformat_False_ReturnsFalse()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(false).Should().Be("False");
    }

    [Fact]
    public void Pformat_Double_IncludesDecimalPoint()
    {
        var pp = new PrettyPrinter();
        var result = pp.Pformat(3.14);
        result.Should().Contain(".");
        result.Should().Be("3.14");
    }

    [Fact]
    public void Pformat_WholeDouble_AppendsDotZero()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(5.0).Should().Be("5.0");
    }

    // ----- Dict formatting -----

    [Fact]
    public void Pformat_SimpleDict_FormatsEntries()
    {
        var pp = new PrettyPrinter();
        var dict = new Dict<string, int>();
        dict["a"] = 1;
        dict["b"] = 2;
        pp.Pformat(dict).Should().Be("{'a': 1, 'b': 2}");
    }

    [Fact]
    public void Pformat_Dict_SortsKeysByDefault()
    {
        var pp = new PrettyPrinter();
        var dict = new Dict<string, int>();
        dict["b"] = 2;
        dict["a"] = 1;
        dict["c"] = 3;
        pp.Pformat(dict).Should().Be("{'a': 1, 'b': 2, 'c': 3}");
    }

    [Fact]
    public void Pformat_Dict_SortDictsFalse_PreservesInsertionOrder()
    {
        var pp = new PrettyPrinter(sortDicts: false);
        var dict = new Dict<string, int>();
        dict["b"] = 2;
        dict["a"] = 1;
        dict["c"] = 3;
        pp.Pformat(dict).Should().Be("{'b': 2, 'a': 1, 'c': 3}");
    }

    [Fact]
    public void Pformat_EmptyDict_ReturnsBraces()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(new Dict<string, int>()).Should().Be("{}");
    }

    // ----- List formatting -----

    [Fact]
    public void Pformat_ShortList_SingleLine()
    {
        var pp = new PrettyPrinter();
        var list = new List<int>(new[] { 1, 2, 3 });
        pp.Pformat(list).Should().Be("[1, 2, 3]");
    }

    [Fact]
    public void Pformat_EmptyList_ReturnsBrackets()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(new List<int>()).Should().Be("[]");
    }

    [Fact]
    public void Pformat_LongList_WrapsAcrossLines()
    {
        var pp = new PrettyPrinter(width: 20);
        var list = new List<int>(new[] { 100, 200, 300, 400, 500, 600 });
        var result = pp.Pformat(list);
        result.Should().Contain("\n");
    }

    [Fact]
    public void Pformat_CompactList_PacksMultiplePerLine()
    {
        var compact = new PrettyPrinter(width: 20, compact: true);
        var list = new List<int>(new[] { 100, 200, 300, 400, 500, 600 });
        var result = compact.Pformat(list);
        result.Should().Contain("\n");
        // Compact packing should fit more than one item on at least one line
        result.Should().Contain(", ");
    }

    // ----- Set formatting -----

    [Fact]
    public void Pformat_Set_FormatsWithBraces()
    {
        var pp = new PrettyPrinter();
        var set = new Set<int>(new[] { 1, 2, 3 });
        pp.Pformat(set).Should().Be("{1, 2, 3}");
    }

    [Fact]
    public void Pformat_EmptySet_ReturnsSetCall()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(new Set<int>()).Should().Be("set()");
    }

    // ----- Tuple formatting -----

    [Fact]
    public void Pformat_Tuple_FormatsWithParens()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(System.ValueTuple.Create(1, 2, 3)).Should().Be("(1, 2, 3)");
    }

    [Fact]
    public void Pformat_SingleElementTuple_HasTrailingComma()
    {
        var pp = new PrettyPrinter();
        pp.Pformat(System.ValueTuple.Create(1)).Should().Be("(1,)");
    }

    // ----- Depth limiting -----

    [Fact]
    public void Pformat_DepthOne_TruncatesInnerCollections()
    {
        var pp = new PrettyPrinter(depth: 1);
        var inner = new List<int>(new[] { 1, 2 });
        var outer = new List<object>(new object[] { inner });
        pp.Pformat(outer).Should().Be("[...]");
    }

    // ----- Circular reference detection -----

    [Fact]
    public void Isrecursive_SelfReferencingList_ReturnsTrue()
    {
        var list = new List<object>();
        list.Append(list);
        var pp = new PrettyPrinter();
        pp.Isrecursive(list).Should().BeTrue();
    }

    [Fact]
    public void Isreadable_SelfReferencingList_ReturnsFalse()
    {
        var list = new List<object>();
        list.Append(list);
        var pp = new PrettyPrinter();
        pp.Isreadable(list).Should().BeFalse();
    }

    [Fact]
    public void Pformat_SelfReferencingList_ShowsRecursionMarker()
    {
        var list = new List<object>();
        list.Append(list);
        var pp = new PrettyPrinter();
        pp.Pformat(list).Should().Contain("<Recursion on list with id=");
    }

    [Fact]
    public void Isrecursive_NonRecursiveList_ReturnsFalse()
    {
        var list = new List<int>(new[] { 1, 2, 3 });
        var pp = new PrettyPrinter();
        pp.Isrecursive(list).Should().BeFalse();
    }

    [Fact]
    public void Isreadable_SimpleList_ReturnsTrue()
    {
        var list = new List<int>(new[] { 1, 2, 3 });
        var pp = new PrettyPrinter();
        pp.Isreadable(list).Should().BeTrue();
    }

    // ----- Constructor parameter validation -----

    [Fact]
    public void Constructor_NegativeIndent_Throws()
    {
        System.Action act = () => new PrettyPrinter(indent: -1);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Constructor_ZeroWidth_Throws()
    {
        System.Action act = () => new PrettyPrinter(width: 0);
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Pformat_CustomIndentAndWidth_WrapsWithIndent()
    {
        var pp = new PrettyPrinter(indent: 4, width: 10);
        var list = new List<int>(new[] { 1, 2, 3, 4, 5 });
        var result = pp.Pformat(list);
        result.Should().Contain("\n");
        // indent of 4 spaces should appear before wrapped items
        result.Should().Contain("\n    ");
    }

    // ----- Module-level functions -----

    [Fact]
    public void Module_Pformat_Integer_Works()
    {
        PprintModule.Pformat(42).Should().Be("42");
    }

    [Fact]
    public void Module_Pformat_Dict_SortsKeys()
    {
        var dict = new Dict<string, int>();
        dict["b"] = 2;
        dict["a"] = 1;
        PprintModule.Pformat(dict).Should().Be("{'a': 1, 'b': 2}");
    }

    [Fact]
    public void Module_Isrecursive_SelfReferencing_ReturnsTrue()
    {
        var list = new List<object>();
        list.Append(list);
        PprintModule.Isrecursive(list).Should().BeTrue();
    }

    [Fact]
    public void Module_Isreadable_Simple_ReturnsTrue()
    {
        PprintModule.Isreadable(42).Should().BeTrue();
    }
}
