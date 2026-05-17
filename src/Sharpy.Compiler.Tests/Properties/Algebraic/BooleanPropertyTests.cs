using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("HeavyCompilation")]
public class BooleanPropertyTests : AlgebraicTestBase
{
    public BooleanPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DoubleNegation()
    {
        Gen.Bool.Sample(b =>
        {
            var val = b ? "True" : "False";
            var r1 = RunAndCapture($"def main():\n    print(not not {val})");
            var r2 = RunAndCapture($"def main():\n    print({val})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"not not {val} = {r1} but {val} = {r2}");
        }, iter: 10);
    }

    [Fact]
    public void DeMorgan_NotAndToOrNot()
    {
        Gen.Bool.Select(Gen.Bool).Sample((a, b) =>
        {
            var va = a ? "True" : "False";
            var vb = b ? "True" : "False";
            var r1 = RunAndCapture($"def main():\n    print(not ({va} and {vb}))");
            var r2 = RunAndCapture($"def main():\n    print(not {va} or not {vb})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"not ({va} and {vb}) = {r1} but (not {va} or not {vb}) = {r2}");
        }, iter: 10);
    }
}
