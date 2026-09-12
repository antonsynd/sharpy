using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class DictNullKeyTests
    {
        [Fact]
        public void NullKey_IndexerSet_ThenGet()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["b"] = 2;

            d[null!].Should().Be(1);
            d["b"].Should().Be(2);
            d.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_ContainsKey()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.ContainsKey(null!).Should().BeTrue();
            d.Contains(null!).Should().BeTrue();
        }

        [Fact]
        public void NullKey_ContainsKey_Absent()
        {
            var d = new Dict<string?, int>();

            d.ContainsKey(null!).Should().BeFalse();
            d.Contains(null!).Should().BeFalse();
        }

        [Fact]
        public void NullKey_TryGetValue()
        {
            var d = new Dict<string?, int>();
            d[null!] = 42;

            d.TryGetValue(null!, out var val).Should().BeTrue();
            val.Should().Be(42);
        }

        [Fact]
        public void NullKey_TryGetValue_Absent()
        {
            var d = new Dict<string?, int>();

            d.TryGetValue(null!, out _).Should().BeFalse();
        }

        [Fact]
        public void NullKey_Get_Optional()
        {
            var d = new Dict<string?, int>();
            d[null!] = 10;

            d.Get(null!).IsSome.Should().BeTrue();
        }

        [Fact]
        public void NullKey_Get_WithDefault()
        {
            var d = new Dict<string?, int>();
            d[null!] = 10;

            d.Get(null!, 99).Should().Be(10);
        }

        [Fact]
        public void NullKey_Get_Absent_WithDefault()
        {
            var d = new Dict<string?, int>();

            d.Get(null!, 99).Should().Be(99);
        }

        [Fact]
        public void NullKey_Pop()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            d.Pop(null!).Should().Be(1);
            d.Count.Should().Be(1);
            d.ContainsKey(null!).Should().BeFalse();
        }

        [Fact]
        public void NullKey_Pop_Absent_Throws()
        {
            var d = new Dict<string?, int>();

            d.Invoking(x => x.Pop(null!)).Should().Throw<KeyError>();
        }

        [Fact]
        public void NullKey_Pop_WithDefault()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.Pop(null!, 99).Should().Be(1);
        }

        [Fact]
        public void NullKey_Pop_Absent_WithDefault()
        {
            var d = new Dict<string?, int>();

            d.Pop(null!, 99).Should().Be(99);
        }

        [Fact]
        public void NullKey_SetDefault_Absent()
        {
            var d = new Dict<string?, int>();

            d.SetDefault(null!, 42).Should().Be(42);
            d[null!].Should().Be(42);
        }

        [Fact]
        public void NullKey_SetDefault_Present()
        {
            var d = new Dict<string?, int>();
            d[null!] = 10;

            d.SetDefault(null!, 42).Should().Be(10);
        }

        [Fact]
        public void NullKey_Remove()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.Remove(null!);
            d.Count.Should().Be(0);
        }

        [Fact]
        public void NullKey_Remove_Absent_Throws()
        {
            var d = new Dict<string?, int>();

            d.Invoking(x => x.Remove(null!)).Should().Throw<KeyError>();
        }

        [Fact]
        public void NullKey_Clear()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            d.Clear();
            d.Count.Should().Be(0);
            d.ContainsKey(null!).Should().BeFalse();
        }

        [Fact]
        public void NullKey_Copy()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            var copy = d.Copy();
            copy[null!].Should().Be(1);
            copy["a"].Should().Be(2);
            copy.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_Update_FromDict()
        {
            var d = new Dict<string?, int>();
            d["a"] = 1;

            var other = new Dict<string?, int>();
            other[null!] = 10;

            d.Update(other);
            d[null!].Should().Be(10);
            d.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_Fromkeys()
        {
            var keys = new List<string?>(new string?[] { null, "a", "b" });
            var d = Dict<string?, int>.Fromkeys(keys, 0);

            d[null!].Should().Be(0);
            d["a"].Should().Be(0);
            d.Count.Should().Be(3);
        }

        [Fact]
        public void NullKey_Equals()
        {
            var a = new Dict<string?, int>();
            a[null!] = 1;
            a["a"] = 2;

            var b = new Dict<string?, int>();
            b[null!] = 1;
            b["a"] = 2;

            a.Equals(b).Should().BeTrue();
        }

        [Fact]
        public void NullKey_Equals_DifferentValue()
        {
            var a = new Dict<string?, int>();
            a[null!] = 1;

            var b = new Dict<string?, int>();
            b[null!] = 2;

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void NullKey_Merge()
        {
            var a = new Dict<string?, int>();
            a["a"] = 1;

            var b = new Dict<string?, int>();
            b[null!] = 2;

            var merged = a | b;
            merged[null!].Should().Be(2);
            merged["a"].Should().Be(1);
        }

        [Fact]
        public void NullKey_ToString_PrintsNone()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.ToString().Should().Contain("None: 1");
        }

        [Fact]
        public void NullKey_Count_IncludesSlot()
        {
            var d = new Dict<string?, int>();
            d.Count.Should().Be(0);

            d[null!] = 1;
            d.Count.Should().Be(1);

            d["a"] = 2;
            d.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_Truthiness()
        {
            var d = new Dict<string?, int>();
            (d ? true : false).Should().BeFalse();

            d[null!] = 1;
            (d ? true : false).Should().BeTrue();
        }

        [Fact]
        public void NullKey_Enumerator_IsValueType()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.GetEnumerator().GetType().IsValueType.Should().BeTrue();
        }

        [Fact]
        public void NullKey_KeysView_IncludesNull()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            var keys = d.Keys();
            keys.Count.Should().Be(2);
            keys.Contains(null!).Should().BeTrue();
        }

        [Fact]
        public void NullKey_ValuesView_IncludesNullValue()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            var values = d.Values();
            values.Count.Should().Be(2);
            values.Contains(1).Should().BeTrue();
        }

        [Fact]
        public void NullKey_ItemsView()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            var items = d.Items();
            items.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_ToDictionary_Throws()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            d.Invoking(x => x.ToDictionary()).Should().Throw<TypeError>();
        }

        [Fact]
        public void NullKey_PopItem_WhenLast()
        {
            var d = new Dict<string?, int>();
            d["a"] = 1;
            d[null!] = 2;

            var (key, value) = d.PopItem();
            key.Should().BeNull();
            value.Should().Be(2);
        }

        [Fact]
        public void NullKey_IDict_Indexer()
        {
            var d = new Dict<string?, int>();
            d[null!] = 42;

            IDict idict = d;
            ((int)idict[null!]!).Should().Be(42);
        }

        [Fact]
        public void NullKey_IDict_Contains()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;

            IDict idict = d;
            idict.Contains(null!).Should().BeTrue();
        }

        [Fact]
        public void NullKey_Constructor_FromMapping()
        {
            var source = new Dict<string?, int>();
            source[null!] = 1;
            source["a"] = 2;

            var d = new Dict<string?, int>(source);
            d[null!].Should().Be(1);
            d.Count.Should().Be(2);
        }
    }
}
