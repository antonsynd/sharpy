using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class DictPropertyTests : AlgebraicTestBase
{
    public DictPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DictMerge_HasIdentity()
    {
        Gen.OneOfConst("{\"a\": 1, \"b\": 2}", "{\"c\": 3}", "{}", "{\"x\": 0, \"y\": 0}").Sample(a =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted(({a} | {{}}).items()))");
            var r2 = RunAndCapture($"def main():\n    print(sorted(({a}).items()))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{a} | {{}} = {r1} but {a} = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void DictMerge_IsAssociative()
    {
        Gen.Select(
            Gen.OneOfConst("{\"a\": 1}", "{\"b\": 2, \"c\": 3}", "{}"),
            Gen.OneOfConst("{\"d\": 4}", "{\"b\": 5, \"e\": 6}", "{}"),
            Gen.OneOfConst("{\"f\": 7}", "{\"c\": 8, \"g\": 9}", "{}")
        ).Sample((a, b, c) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(sorted((({a} | {b}) | {c}).items()))");
            var r2 = RunAndCapture($"def main():\n    print(sorted(({a} | ({b} | {c})).items()))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"({a} | {b}) | {c} = {r1} but {a} | ({b} | {c}) = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void DictMerge_SizeUpperBound()
    {
        Gen.Select(
            Gen.OneOfConst("{\"a\": 1, \"b\": 2}", "{\"c\": 3}", "{}"),
            Gen.OneOfConst("{\"a\": 10}", "{\"d\": 4, \"e\": 5}", "{}")
        ).Sample((a, b) =>
        {
            var r = RunAndCapture(
                $"def main():\n    print(len({a} | {b}) <= len({a}) + len({b}))");
            if (r != null && r != "True")
                throw new Exception($"len({a} | {b}) > len({a}) + len({b})");
        }, iter: 10);
    }

    [Fact]
    public void DictMerge_LastWriteWins()
    {
        Gen.Select(
            Gen.OneOfConst("{\"a\": 1, \"b\": 2}", "{\"a\": 100}", "{\"a\": 1, \"b\": 2, \"c\": 3}"),
            Gen.OneOfConst("{\"a\": 99}", "{\"a\": 7, \"b\": 8}", "{\"b\": 50}")
        ).Sample((a, b) =>
        {
            // For every key present in b, the merged value must equal b's value.
            var r = RunAndCapture(
                $"def main():\n    merged = {a} | {b}\n    b = {b}\n    print(all(merged[k] == b[k] for k in b))");
            if (r != null && r != "True")
                throw new Exception($"Last-write-wins violated for {a} | {b}: {r}");
        }, iter: 10);
    }
}
