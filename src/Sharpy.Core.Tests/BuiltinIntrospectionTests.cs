using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for builtin introspection functions: Len, Id, Hash, Ord, Chr.
/// Repr is covered in ReprTests.cs. Type/Isinstance/Issubclass have their own files.
/// </summary>
public class BuiltinIntrospection_Tests
{
    // ── Len ──

    [Fact]
    public void Len_EmptyList_ReturnsZero()
    {
        Len(new List<int>()).Should().Be(0);
    }

    [Fact]
    public void Len_ListWithElements_ReturnsCount()
    {
        Len(new List<int> { 1, 2, 3 }).Should().Be(3);
    }

    [Fact]
    public void Len_EmptyString_ReturnsZero()
    {
        Len("").Should().Be(0);
    }

    [Fact]
    public void Len_NonEmptyString_ReturnsLength()
    {
        Len("hello").Should().Be(5);
    }

    [Fact]
    public void Len_Dictionary_ReturnsCount()
    {
        var d = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        Len(d).Should().Be(2);
    }

    [Fact]
    public void Len_Array_ReturnsLength()
    {
        var arr = new int[] { 10, 20, 30, 40 };
        Len(arr).Should().Be(4);
    }

    [Fact]
    public void Len_ISized_UsesCount()
    {
        // ISized protocol should be respected by Len via reflection path
        var sized = new FakeSizedCollection(7);
        // Len uses ISized via ICollection<T> interface reflection
        Len(sized).Should().Be(7);
    }

    [Fact]
    public void Len_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Len((object)null!))
            .Should().Throw<TypeError>();
    }

    // ── Id ──

    [Fact]
    public void Id_Object_ReturnsNonZeroOrAnyInteger()
    {
        // id() returns runtime identity — we can only verify it returns some integer
        // and that it's consistent for the same object during its lifetime
        var obj = new object();
        var id1 = Id(obj);
        var id2 = Id(obj);
        id1.Should().Be(id2); // same object, same id
    }

    [Fact]
    public void Id_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Id(null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Id_String_ReturnsSomeInteger()
    {
        // Just verify it completes without exception and returns an int
        var s = "hello";
        var id = Id(s);
        id.GetType().Should().Be(typeof(int));
    }

    // ── Hash ──

    [Fact]
    public void Hash_Integer_IsConsistent()
    {
        Hash(42).Should().Be(Hash(42));
    }

    [Fact]
    public void Hash_String_IsConsistent()
    {
        // Same string content — in .NET, strings with same content share hash
        Hash("hello").Should().Be(Hash("hello"));
    }

    [Fact]
    public void Hash_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Hash(null!))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Hash_DifferentPrimitives_ReturnsDifferentValues()
    {
        // Different integers should (usually) have different hashes
        Hash(1).Should().NotBe(Hash(2));
    }

    // ── Ord ──

    [Fact]
    public void Ord_LowercaseA_Returns97()
    {
        Ord("a").Should().Be(97);
    }

    [Fact]
    public void Ord_UppercaseA_Returns65()
    {
        Ord("A").Should().Be(65);
    }

    [Fact]
    public void Ord_Newline_Returns10()
    {
        Ord("\n").Should().Be(10);
    }

    [Fact]
    public void Ord_Space_Returns32()
    {
        Ord(" ").Should().Be(32);
    }

    [Fact]
    public void Ord_EmptyString_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Ord(""))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Ord_MultiCharString_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Ord("ab"))
            .Should().Throw<TypeError>();
    }

    [Fact]
    public void Ord_Null_ThrowsTypeError()
    {
        FluentActions.Invoking(() => Ord(null!))
            .Should().Throw<TypeError>();
    }

    // ── Chr ──

    [Fact]
    public void Chr_97_ReturnsLowercaseA()
    {
        Chr(97).Should().Be("a");
    }

    [Fact]
    public void Chr_65_ReturnsUppercaseA()
    {
        Chr(65).Should().Be("A");
    }

    [Fact]
    public void Chr_Zero_ReturnsNullChar()
    {
        Chr(0).Should().Be("\0");
    }

    [Fact]
    public void Chr_NegativeOne_ThrowsValueError()
    {
        FluentActions.Invoking(() => Chr(-1))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Chr_0x110000_ThrowsValueError()
    {
        // 0x110000 is just above the valid Unicode range (max is 0x10FFFF)
        FluentActions.Invoking(() => Chr(0x110000))
            .Should().Throw<ValueError>();
    }

    [Fact]
    public void Chr_0x10FFFF_ReturnsHighCodePoint()
    {
        // Maximum valid Unicode code point — should not throw
        var result = Chr(0x10FFFF);
        result.Should().NotBeNull();
    }

    // ── Repr — additional cases not in ReprTests.cs ──

    [Fact]
    public void Repr_Integer_ReturnsSameAsStr()
    {
        Repr(42).Should().Be("42");
    }

    [Fact]
    public void Repr_SimpleString_WrapsInSingleQuotes()
    {
        Repr("hello").Should().Be("'hello'");
    }

    [Fact]
    public void Repr_Null_ReturnsNone()
    {
        Repr(null).Should().Be("None");
    }

    // ── Helpers ──

    /// <summary>
    /// A fake collection that implements ICollection&lt;T&gt; so Len() can discover Count via reflection.
    /// </summary>
    private sealed class FakeSizedCollection : System.Collections.Generic.ICollection<int>
    {
        private readonly int _count;
        public FakeSizedCollection(int count) => _count = count;
        public int Count => _count;
        public bool IsReadOnly => true;
        public void Add(int item) => throw new System.NotSupportedException();
        public void Clear() => throw new System.NotSupportedException();
        public bool Contains(int item) => false;
        public void CopyTo(int[] array, int arrayIndex) { }
        public bool Remove(int item) => throw new System.NotSupportedException();
        public System.Collections.Generic.IEnumerator<int> GetEnumerator() => System.Linq.Enumerable.Empty<int>().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
