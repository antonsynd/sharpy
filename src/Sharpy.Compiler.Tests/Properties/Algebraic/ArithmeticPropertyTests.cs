using CsCheck;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Algebraic;

[Trait("Category", "Property")]
[Trait("Speed", "Slow")]
public class ArithmeticPropertyTests : AlgebraicTestBase
{
    public ArithmeticPropertyTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void IntAddition_IsCommutative()
    {
        Gen.Int[-1000, 1000].Select(Gen.Int[-1000, 1000]).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print({a} + {b})");
            var r2 = RunAndCapture($"def main():\n    print({b} + {a})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{a} + {b} = {r1} but {b} + {a} = {r2}");
        }, iter: 25);
    }

    [Fact]
    public void IntAddition_HasIdentity()
    {
        Gen.Int[-1000, 1000].Sample(a =>
        {
            var r1 = RunAndCapture($"def main():\n    print({a} + 0)");
            var r2 = RunAndCapture($"def main():\n    print({a})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{a} + 0 = {r1} but {a} = {r2}");
        }, iter: 25);
    }

    [Fact]
    public void IntMultiplication_IsCommutative()
    {
        Gen.Int[-100, 100].Select(Gen.Int[-100, 100]).Sample((a, b) =>
        {
            var r1 = RunAndCapture($"def main():\n    print({a} * {b})");
            var r2 = RunAndCapture($"def main():\n    print({b} * {a})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{a} * {b} = {r1} but {b} * {a} = {r2}");
        }, iter: 25);
    }

    [Fact]
    public void IntMultiplication_HasIdentity()
    {
        Gen.Int[-1000, 1000].Sample(a =>
        {
            var r1 = RunAndCapture($"def main():\n    print({a} * 1)");
            var r2 = RunAndCapture($"def main():\n    print({a})");
            if (r1 != null && r2 != null && r1 != r2)
                throw new Exception($"{a} * 1 = {r1} but {a} = {r2}");
        }, iter: 25);
    }
}
