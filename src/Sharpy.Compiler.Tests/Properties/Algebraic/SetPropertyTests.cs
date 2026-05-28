using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class SetPropertyTests : AlgebraicTestBase
{
    public SetPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SetUnion_IsIdempotent()
    {
        Gen.OneOfConst("{1, 2, 3}", "{10}", "{0, 0}").Sample(xs =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted({xs} | {xs}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted({xs}))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"sorted({xs} | {xs}) = {r1} but sorted({xs}) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void SetUnion_IsCommutative()
    {
        Gen.Select(
            Gen.OneOfConst("{1, 2}", "{3}", "{4, 5, 6}"),
            Gen.OneOfConst("{2, 3}", "{7}", "{1, 8}")
        ).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted({a} | {b}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted({b} | {a}))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"sorted({a} | {b}) = {r1} but sorted({b} | {a}) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void SetUnion_IsAssociative()
    {
        Gen.Select(
            Gen.OneOfConst("{1}", "{2, 3}", "{4}"),
            Gen.OneOfConst("{5}", "{6, 7}", "{2}"),
            Gen.OneOfConst("{8}", "{1, 9}", "{3}")
        ).Sample((a, b, c) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted(({a} | {b}) | {c}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted({a} | ({b} | {c})))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception(
                    $"sorted(({a} | {b}) | {c}) = {r1} but sorted({a} | ({b} | {c})) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void SetDifference_RemovesElements()
    {
        Gen.Select(
            Gen.OneOfConst("{1, 2}", "{3}", "{4, 5, 6}"),
            Gen.OneOfConst("{2, 3}", "{7}", "{1, 8}")
        ).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted({a} - {b}))");
            var r2 = RunAndCapture(
                $"def main():\n    print(sorted([x for x in {a} if x not in {b}]))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception(
                    $"sorted({a} - {b}) = {r1} but sorted([x for x in {a} if x not in {b}]) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void SetIntersection_IsCommutative()
    {
        Gen.Select(
            Gen.OneOfConst("{1, 2}", "{3}", "{4, 5, 6}"),
            Gen.OneOfConst("{2, 3}", "{7}", "{1, 8}")
        ).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted({a} & {b}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted({b} & {a}))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"sorted({a} & {b}) = {r1} but sorted({b} & {a}) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void SetSymmetricDifference_IsCommutative()
    {
        Gen.Select(
            Gen.OneOfConst("{1, 2}", "{3}", "{4, 5, 6}"),
            Gen.OneOfConst("{2, 3}", "{7}", "{1, 8}")
        ).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted({a} ^ {b}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted({b} ^ {a}))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"sorted({a} ^ {b}) = {r1} but sorted({b} ^ {a}) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void DeMorgans_ForSets()
    {
        Gen.Select(
            Gen.OneOfConst("{1}", "{2, 3}", "{4}"),
            Gen.OneOfConst("{5}", "{6, 7}", "{2}"),
            Gen.OneOfConst("{8}", "{1, 9}", "{3}")
        ).Sample((a, b, c) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted(({a} | {b}) - {c}))");
            var r2 = RunAndCapture($"def main():\n    print(sorted(({a} - {c}) | ({b} - {c})))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception(
                    $"sorted(({a} | {b}) - {c}) = {r1} but sorted(({a} - {c}) | ({b} - {c})) = {r2}");
        }, iter: 10);
    }
}
