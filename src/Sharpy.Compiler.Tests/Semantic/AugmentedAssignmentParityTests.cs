using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Metamorphic matrix: for every operator in AssignmentOperatorToBinaryOperator x a roster of
/// incompatible type pairs, `x op= y` and `x = x op y` must produce the same diagnostic code
/// (SPY0222) and both refuse. Positive controls must both accept and print the same stdout.
/// Guards #1631.
/// </summary>
[Collection("HeavyCompilation")]
public class AugmentedAssignmentParityTests : IntegrationTestBase
{
    public AugmentedAssignmentParityTests(ITestOutputHelper output) : base(output) { }

    public static IEnumerable<object[]> IncompatiblePairs()
    {
        var operators = new (string aug, string bin)[]
        {
            ("+=", "+"), ("-=", "-"), ("*=", "*"), ("/=", "/"),
            ("//=", "//"), ("%=", "%"), ("**=", "**"),
            ("&=", "&"), ("|=", "|"), ("^=", "^"),
            ("<<=", "<<"), (">>=", ">>"),
        };

        // (float, bytes) and (bool, bytes) are universally incompatible with all operators.
        // (int, bytes) and (bytes, int) are compatible for * (bytes repetition), tested separately.
        var pairs = new (string lType, string lInit, string rType, string rInit)[]
        {
            ("float", "3.14", "bytes", "b\"ab\""),
            ("bool", "True", "bytes", "b\"ab\""),
        };

        foreach (var (aug, bin) in operators)
        {
            foreach (var (lType, lInit, rType, rInit) in pairs)
            {
                yield return new object[] { aug, bin, lType, lInit, rType, rInit };
            }
        }
    }

    public static IEnumerable<object[]> NonMultiplyIncompatiblePairs()
    {
        // These are compatible for * (repetition) but incompatible for all other operators
        var nonMultiply = new (string aug, string bin)[]
        {
            ("+=", "+"), ("-=", "-"), ("/=", "/"),
            ("//=", "//"), ("%=", "%"), ("**=", "**"),
            ("&=", "&"), ("|=", "|"), ("^=", "^"),
            ("<<=", "<<"), (">>=", ">>"),
        };

        var pairs = new (string lType, string lInit, string rType, string rInit)[]
        {
            ("int", "1", "bytes", "b\"ab\""),
            ("bytes", "b\"ab\"", "int", "1"),
            ("str", "\"hello\"", "int", "42"),
            ("int", "1", "str", "\"hello\""),
            ("list[int]", "[1]", "int", "42"),
        };

        foreach (var (aug, bin) in nonMultiply)
        {
            foreach (var (lType, lInit, rType, rInit) in pairs)
            {
                yield return new object[] { aug, bin, lType, lInit, rType, rInit };
            }
        }
    }

    public static IEnumerable<object[]> NullCoalesceIncompatiblePairs()
    {
        yield return new object[] { "int?", "None", "str", "\"hello\"" };
        yield return new object[] { "int?", "None", "bytes", "b\"ab\"" };
    }

    [Theory]
    [MemberData(nameof(IncompatiblePairs))]
    [MemberData(nameof(NonMultiplyIncompatiblePairs))]
    public void AugmentedAndBinaryBothRefuse(string aug, string bin, string lType, string lInit, string rType, string rInit)
    {
        var augSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x {aug} y
";

        var binSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x = x {bin} y
";

        var augResult = CompileAndExecute(augSource);
        var binResult = CompileAndExecute(binSource);

        augResult.Success.Should().BeFalse($"augmented `{lType} {aug} {rType}` should refuse");
        binResult.Success.Should().BeFalse($"binary `{lType} {bin} {rType}` should refuse");

        augResult.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            $"augmented `{lType} {aug} {rType}` should produce SPY0222");
        binResult.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            $"binary `{lType} {bin} {rType}` should produce SPY0222");
    }

    [Theory]
    [MemberData(nameof(NullCoalesceIncompatiblePairs))]
    public void NullCoalesceAugmentedAndBinaryBothRefuse(string lType, string lInit, string rType, string rInit)
    {
        var augSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x ??= y
";

        var binSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x = x ?? y
";

        var augResult = CompileAndExecute(augSource);
        var binResult = CompileAndExecute(binSource);

        augResult.Success.Should().BeFalse($"augmented `{lType} ??= {rType}` should refuse");
        binResult.Success.Should().BeFalse($"binary `{lType} ?? {rType}` should refuse");
    }

    public static IEnumerable<object[]> CompatiblePairs()
    {
        yield return new object[] { "+=", "+", "bytes", "b\"ab\"", "bytes", "b\"cd\"", "b'abcd'" };
        yield return new object[] { "+=", "+", "str", "\"hello\"", "str", "\" world\"", "hello world" };
        yield return new object[] { "+=", "+", "list[int]", "[1, 2]", "list[int]", "[3]", "[1, 2, 3]" };
        yield return new object[] { "<<=", "<<", "int", "1", "int", "3", "8" };
        yield return new object[] { "**=", "**", "int", "2", "int", "10", "1024" };
        yield return new object[] { "/=", "/", "float", "10.0", "int", "2", "5.0" };
    }

    [Theory]
    [MemberData(nameof(CompatiblePairs))]
    public void PositiveControlsBothAcceptAndMatch(string aug, string bin, string lType, string lInit, string rType, string rInit, string expectedOutput)
    {
        var augSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x {aug} y
    print(x)
";

        var binSource = $@"
def main():
    x: {lType} = {lInit}
    y: {rType} = {rInit}
    x = x {bin} y
    print(x)
";

        var augResult = CompileAndExecute(augSource);
        var binResult = CompileAndExecute(binSource);

        augResult.Success.Should().BeTrue($"augmented `{lType} {aug} {rType}` should accept");
        binResult.Success.Should().BeTrue($"binary `{lType} {bin} {rType}` should accept");

        augResult.StandardOutput.Trim().Should().Be(expectedOutput,
            $"augmented `{lType} {aug} {rType}` output");
        binResult.StandardOutput.Trim().Should().Be(expectedOutput,
            $"binary `{lType} {bin} {rType}` output");
    }

    [Fact]
    public void NullCoalescePositiveControl()
    {
        var source = @"
def main():
    x: int? = None
    y: int = 42
    x ??= y
    print(x)
";
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue("Optional ??= T should accept");
        result.StandardOutput.Trim().Should().Be("42");
    }
}
