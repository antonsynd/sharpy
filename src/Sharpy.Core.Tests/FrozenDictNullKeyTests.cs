using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests
{
    public class FrozenDictNullKeyTests
    {
        [Fact]
        public void NullKey_FromDict()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            d["a"] = 2;

            var fd = new FrozenDict<string?, int>(d);
            fd[null!].Should().Be(1);
            fd["a"].Should().Be(2);
            fd.Count.Should().Be(2);
        }

        [Fact]
        public void NullKey_ContainsKey()
        {
            var d = new Dict<string?, int>();
            d[null!] = 1;
            var fd = new FrozenDict<string?, int>(d);

            fd.ContainsKey(null!).Should().BeTrue();
            fd.Contains(null!).Should().BeTrue();
        }

        [Fact]
        public void NullKey_ContainsKey_Absent()
        {
            var fd = new FrozenDict<string?, int>();

            fd.ContainsKey(null!).Should().BeFalse();
        }

        [Fact]
        public void NullKey_Get()
        {
            var d = new Dict<string?, int>();
            d[null!] = 42;
            var fd = new FrozenDict<string?, int>(d);

            fd.Get(null!).Should().Be(42);
        }

        [Fact]
        public void NullKey_Get_Absent_Default()
        {
            var fd = new FrozenDict<string?, int>();

            fd.Get(null!, 99).Should().Be(99);
        }

        [Fact]
        public void NullKey_TryGetValue()
        {
            var d = new Dict<string?, int>();
            d[null!] = 10;
            var fd = new FrozenDict<string?, int>(d);

            fd.TryGetValue(null!, out var val).Should().BeTrue();
            val.Should().Be(10);
        }

        [Fact]
        public void NullKey_Indexer_ThrowsKeyError_WhenAbsent()
        {
            var fd = new FrozenDict<string?, int>();

            fd.Invoking(x => { var _ = x[null!]; }).Should().Throw<KeyError>();
        }

        [Fact]
        public void NullKey_Merge()
        {
            var a = new Dict<string?, int>();
            a["a"] = 1;
            var fa = new FrozenDict<string?, int>(a);

            var b = new Dict<string?, int>();
            b[null!] = 2;
            var fb = new FrozenDict<string?, int>(b);

            var merged = fa | fb;
            merged[null!].Should().Be(2);
            merged["a"].Should().Be(1);
        }

        [Fact]
        public void NullKey_Equals()
        {
            var d1 = new Dict<string?, int>();
            d1[null!] = 1;
            var f1 = new FrozenDict<string?, int>(d1);

            var d2 = new Dict<string?, int>();
            d2[null!] = 1;
            var f2 = new FrozenDict<string?, int>(d2);

            f1.Equals(f2).Should().BeTrue();
        }

        [Fact]
        public void NullKey_FromKeyValuePairs()
        {
            var pairs = new List<KeyValuePair<string?, int>>
            {
                new KeyValuePair<string?, int>(null, 1),
                new KeyValuePair<string?, int>("a", 2),
            };
            var fd = new FrozenDict<string?, int>(pairs);

            fd[null!].Should().Be(1);
            fd.Count.Should().Be(2);
        }
    }
}
