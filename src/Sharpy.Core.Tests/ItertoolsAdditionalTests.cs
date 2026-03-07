using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Sharpy.Tests
{
    public class ItertoolsAdditionalTests
    {
        // --- Count ---

        [Fact]
        public void Count_DefaultStartAndStep()
        {
            var result = new System.Collections.Generic.List<long>();
            foreach (long n in Itertools.Count())
            {
                result.Add(n);
                if (result.Count == 5)
                    break;
            }

            Assert.Equal(new long[] { 0, 1, 2, 3, 4 }, result);
        }

        [Fact]
        public void Count_CustomStartAndStep()
        {
            var result = new System.Collections.Generic.List<long>();
            foreach (long n in Itertools.Count(10, 3))
            {
                result.Add(n);
                if (result.Count == 4)
                    break;
            }

            Assert.Equal(new long[] { 10, 13, 16, 19 }, result);
        }

        [Fact]
        public void Count_NegativeStep()
        {
            var result = new System.Collections.Generic.List<long>();
            foreach (long n in Itertools.Count(5, -1))
            {
                result.Add(n);
                if (result.Count == 4)
                    break;
            }

            Assert.Equal(new long[] { 5, 4, 3, 2 }, result);
        }

        // --- Accumulate ---

        [Fact]
        public void Accumulate_RunningSum()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Accumulate(new[] { 1, 2, 3, 4, 5 }, (a, b) => a + b))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 3, 6, 10, 15 }, result);
        }

        [Fact]
        public void Accumulate_RunningProduct()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Accumulate(new[] { 1, 2, 3, 4 }, (a, b) => a * b))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 2, 6, 24 }, result);
        }

        [Fact]
        public void Accumulate_WithInitial()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Accumulate(new[] { 1, 2, 3 }, (a, b) => a + b, 100))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 100, 101, 103, 106 }, result);
        }

        [Fact]
        public void Accumulate_EmptyIterable()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Accumulate(Array.Empty<int>(), (a, b) => a + b))
            {
                result.Add(n);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void Accumulate_SingleElement()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Accumulate(new[] { 42 }, (a, b) => a + b))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 42 }, result);
        }

        // --- Dropwhile ---

        [Fact]
        public void Dropwhile_Basic()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Dropwhile(x => x < 5, new[] { 1, 4, 6, 4, 1 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 6, 4, 1 }, result);
        }

        [Fact]
        public void Dropwhile_NoneDropped()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Dropwhile(x => x > 100, new[] { 1, 2, 3 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        [Fact]
        public void Dropwhile_AllDropped()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Dropwhile(x => x < 100, new[] { 1, 2, 3 }))
            {
                result.Add(n);
            }

            Assert.Empty(result);
        }

        // --- Takewhile ---

        [Fact]
        public void Takewhile_Basic()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Takewhile(x => x < 5, new[] { 1, 4, 6, 4, 1 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 4 }, result);
        }

        [Fact]
        public void Takewhile_AllTaken()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Takewhile(x => x < 100, new[] { 1, 2, 3 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        [Fact]
        public void Takewhile_NoneTaken()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Takewhile(x => x > 100, new[] { 1, 2, 3 }))
            {
                result.Add(n);
            }

            Assert.Empty(result);
        }

        // --- Compress ---

        [Fact]
        public void Compress_Basic()
        {
            var result = new System.Collections.Generic.List<string>();
            foreach (string s in Itertools.Compress(
                new[] { "A", "B", "C", "D", "E", "F" },
                new[] { true, false, true, false, true, true }))
            {
                result.Add(s);
            }

            Assert.Equal(new[] { "A", "C", "E", "F" }, result);
        }

        [Fact]
        public void Compress_ShorterSelectors()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Compress(new[] { 1, 2, 3, 4, 5 }, new[] { true, true }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 2 }, result);
        }

        // --- Filterfalse ---

        [Fact]
        public void Filterfalse_Basic()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Filterfalse(x => x % 2 == 0, new[] { 1, 2, 3, 4, 5, 6 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 3, 5 }, result);
        }

        [Fact]
        public void Filterfalse_NoneFiltered()
        {
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Filterfalse(x => x > 100, new[] { 1, 2, 3 }))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 1, 2, 3 }, result);
        }

        // --- Starmap ---

        [Fact]
        public void Starmap_Basic()
        {
            var pairs = new[] { (2, 5), (3, 2), (10, 3) };
            var result = new System.Collections.Generic.List<double>();
            foreach (double n in Itertools.Starmap<int, int, double>((a, b) => System.Math.Pow(a, b), pairs))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 32.0, 9.0, 1000.0 }, result);
        }

        [Fact]
        public void Starmap_Addition()
        {
            var pairs = new[] { (1, 10), (2, 20), (3, 30) };
            var result = new System.Collections.Generic.List<int>();
            foreach (int n in Itertools.Starmap<int, int, int>((a, b) => a + b, pairs))
            {
                result.Add(n);
            }

            Assert.Equal(new[] { 11, 22, 33 }, result);
        }

        // --- ZipLongest ---

        [Fact]
        public void ZipLongest_EvenLengths()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.ZipLongest(new IEnumerable<int>[] { new[] { 1, 2, 3 }, new[] { 4, 5, 6 } }, 0))
            {
                result.Add(arr);
            }

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { 1, 4 }, result[0]);
            Assert.Equal(new[] { 2, 5 }, result[1]);
            Assert.Equal(new[] { 3, 6 }, result[2]);
        }

        [Fact]
        public void ZipLongest_UnevenLengths()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.ZipLongest(new IEnumerable<int>[] { new[] { 1, 2, 3 }, new[] { 4 } }, -1))
            {
                result.Add(arr);
            }

            Assert.Equal(3, result.Count);
            Assert.Equal(new[] { 1, 4 }, result[0]);
            Assert.Equal(new[] { 2, -1 }, result[1]);
            Assert.Equal(new[] { 3, -1 }, result[2]);
        }

        // --- Product ---

        [Fact]
        public void Product_TwoIterables()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.Product(new[] { 1, 2 }, new[] { 3, 4 }))
            {
                result.Add(arr);
            }

            Assert.Equal(4, result.Count);
            Assert.Equal(new[] { 1, 3 }, result[0]);
            Assert.Equal(new[] { 1, 4 }, result[1]);
            Assert.Equal(new[] { 2, 3 }, result[2]);
            Assert.Equal(new[] { 2, 4 }, result[3]);
        }

        [Fact]
        public void Product_WithRepeat()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.Product(new[] { 0, 1 }, new[] { 0, 1 }))
            {
                result.Add(arr);
            }

            Assert.Equal(4, result.Count);
            Assert.Equal(new[] { 0, 0 }, result[0]);
            Assert.Equal(new[] { 0, 1 }, result[1]);
            Assert.Equal(new[] { 1, 0 }, result[2]);
            Assert.Equal(new[] { 1, 1 }, result[3]);
        }

        [Fact]
        public void Product_EmptyIterable()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.Product(new[] { 1, 2 }, Array.Empty<int>()))
            {
                result.Add(arr);
            }

            Assert.Empty(result);
        }

        // --- Groupby ---

        [Fact]
        public void Groupby_WithKeyFunc()
        {
            var data = new[] { "aa", "ab", "ba", "bb", "bc" };
            var groups = new System.Collections.Generic.List<(char, System.Collections.Generic.List<string>)>();

            foreach (var (key, group) in Itertools.Groupby(data, s => s[0]))
            {
                var items = new System.Collections.Generic.List<string>();
                foreach (string item in group)
                {
                    items.Add(item);
                }

                groups.Add((key, items));
            }

            Assert.Equal(2, groups.Count);
            Assert.Equal('a', groups[0].Item1);
            Assert.Equal(new[] { "aa", "ab" }, groups[0].Item2);
            Assert.Equal('b', groups[1].Item1);
            Assert.Equal(new[] { "ba", "bb", "bc" }, groups[1].Item2);
        }

        [Fact]
        public void Groupby_ConsecutiveIdentical()
        {
            var data = new[] { 1, 1, 2, 2, 2, 3 };
            var groups = new System.Collections.Generic.List<(int, int)>();

            foreach (var (key, group) in Itertools.Groupby<int, int>(data, x => x))
            {
                int count = 0;
                foreach (int _ in group)
                    count++;
                groups.Add((key, count));
            }

            Assert.Equal(3, groups.Count);
            Assert.Equal((1, 2), groups[0]);
            Assert.Equal((2, 3), groups[1]);
            Assert.Equal((3, 1), groups[2]);
        }

        // --- CombinationsWithReplacement ---

        [Fact]
        public void CombinationsWithReplacement_Basic()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.CombinationsWithReplacement(new[] { 1, 2, 3 }, 2))
            {
                result.Add(arr);
            }

            // (1,1), (1,2), (1,3), (2,2), (2,3), (3,3)
            Assert.Equal(6, result.Count);
            Assert.Equal(new[] { 1, 1 }, result[0]);
            Assert.Equal(new[] { 1, 2 }, result[1]);
            Assert.Equal(new[] { 1, 3 }, result[2]);
            Assert.Equal(new[] { 2, 2 }, result[3]);
            Assert.Equal(new[] { 2, 3 }, result[4]);
            Assert.Equal(new[] { 3, 3 }, result[5]);
        }

        [Fact]
        public void CombinationsWithReplacement_RZero()
        {
            var result = new System.Collections.Generic.List<int[]>();
            foreach (int[] arr in Itertools.CombinationsWithReplacement(new[] { 1, 2 }, 0))
            {
                result.Add(arr);
            }

            Assert.Single(result); // single empty tuple
            Assert.Empty(result[0]);
        }

        [Fact]
        public void CombinationsWithReplacement_NegativeR_Throws()
        {
            Assert.Throws<ValueError>(() => Itertools.CombinationsWithReplacement(new[] { 1 }, -1));
        }

        // --- Pairwise ---

        [Fact]
        public void Pairwise_Basic()
        {
            var result = new System.Collections.Generic.List<(int, int)>();
            foreach (var pair in Itertools.Pairwise(new[] { 1, 2, 3, 4, 5 }))
            {
                result.Add(pair);
            }

            Assert.Equal(4, result.Count);
            Assert.Equal((1, 2), result[0]);
            Assert.Equal((2, 3), result[1]);
            Assert.Equal((3, 4), result[2]);
            Assert.Equal((4, 5), result[3]);
        }

        [Fact]
        public void Pairwise_SingleElement()
        {
            var result = new System.Collections.Generic.List<(int, int)>();
            foreach (var pair in Itertools.Pairwise(new[] { 1 }))
            {
                result.Add(pair);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void Pairwise_Empty()
        {
            var result = new System.Collections.Generic.List<(int, int)>();
            foreach (var pair in Itertools.Pairwise(Array.Empty<int>()))
            {
                result.Add(pair);
            }

            Assert.Empty(result);
        }

        [Fact]
        public void Pairwise_Strings()
        {
            var result = new System.Collections.Generic.List<(string, string)>();
            foreach (var pair in Itertools.Pairwise(new[] { "A", "B", "C", "D" }))
            {
                result.Add(pair);
            }

            Assert.Equal(3, result.Count);
            Assert.Equal(("A", "B"), result[0]);
            Assert.Equal(("B", "C"), result[1]);
            Assert.Equal(("C", "D"), result[2]);
        }
    }
}
