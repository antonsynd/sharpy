using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class ListPropertyTests : AlgebraicTestBase
{
    public ListPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ListConcat_HasIdentity()
    {
        Gen.OneOfConst("[1, 2, 3]", "[10]", "[]", "[0, 0]").Sample(xs =>
        {
            var r1 = RunAndCapture($"def main():\n    print({xs} + [])");
            var r2 = RunAndCapture($"def main():\n    print({xs})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{xs} + [] = {r1} but {xs} = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void ListConcat_LengthIsAdditive()
    {
        Gen.Select(
            Gen.OneOfConst("[1, 2]", "[3]", "[]", "[0, 0, 0]"),
            Gen.OneOfConst("[4, 5]", "[6]", "[]", "[7]")
        ).Sample((a, b) =>
        {
            var r = RunAndCapture(
                $"def main():\n    print(len({a} + {b}) == len({a}) + len({b}))");
            if (r != null && r != "True")
                throw new Exception($"len({a} + {b}) != len({a}) + len({b})");
        }, iter: 10);
    }

    [Fact]
    public void ListConcat_IsAssociative()
    {
        Gen.Select(
            Gen.OneOfConst("[1]", "[2, 3]", "[]"),
            Gen.OneOfConst("[4]", "[5, 6]", "[]"),
            Gen.OneOfConst("[7]", "[8, 9]", "[]")
        ).Sample((a, b, c) =>
        {
            var r1 = RunAndCapture($"def main():\n    print(({a} + {b}) + {c})");
            var r2 = RunAndCapture($"def main():\n    print({a} + ({b} + {c}))");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"({a} + {b}) + {c} = {r1} but {a} + ({b} + {c}) = {r2}");
        }, iter: 10);
    }
}
