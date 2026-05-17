using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class SortedPropertyTests : AlgebraicTestBase
{
    public SortedPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Sorted_IsIdempotent()
    {
        Gen.Int[-100, 100].Array[1, 8].Sample(arr =>
        {
            var elems = string.Join(", ", arr);
            var r1 = RunAndCapture($"def main():\n    print(sorted([{elems}]))");
            var r2 = RunAndCapture($"def main():\n    print(sorted(sorted([{elems}])))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"sorted != sorted(sorted) for [{elems}]");
        }, iter: 10);
    }

    [Fact]
    public void Sorted_PreservesLength()
    {
        Gen.Int[-100, 100].Array[0, 8].Sample(arr =>
        {
            var elems = string.Join(", ", arr);
            var r = RunAndCapture(
                $"def main():\n    print(len(sorted([{elems}])) == len([{elems}]))");
            if (r != null && r != "True")
                throw new Exception($"sorted changed length for [{elems}]");
        }, iter: 10);
    }
}
