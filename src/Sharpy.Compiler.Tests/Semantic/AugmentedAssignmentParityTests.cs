using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Shared;
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

        // Parity is a CODE claim: both spellings refuse through the same SPY0222 seam
        // (InferNullCoalesceType → ReportUnsupportedBinaryOperator), not merely "some error".
        augResult.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            $"augmented `{lType} ??= {rType}` should produce SPY0222");
        binResult.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation,
            $"binary `{lType} ?? {rType}` should produce SPY0222");
    }

    public static IEnumerable<object[]> CompatiblePairs()
    {
        yield return new object[] { "+=", "+", "bytes", "b\"ab\"", "bytes", "b\"cd\"", "b'abcd'" };
        yield return new object[] { "+=", "+", "str", "\"hello\"", "str", "\" world\"", "hello world" };
        yield return new object[] { "+=", "+", "list[int]", "[1, 2]", "list[int]", "[3]", "[1, 2, 3]" };
        yield return new object[] { "<<=", "<<", "int", "1", "int", "3", "8" };
        yield return new object[] { "**=", "**", "int", "2", "int", "10", "1024" };
        yield return new object[] { "/=", "/", "float", "10.0", "int", "2", "5.0" };
        // set |= frozenset — the spec's own cross-kind positive control (collection_types.md
        // "Set augmented assignment"); the pair must stay accepted on BOTH spellings.
        yield return new object[] { "|=", "|", "set[int]", "{1, 2}", "frozenset[int]", "frozenset([3])", "{1, 2, 3}" };
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

    /// <summary>
    /// `@=` is the one augmented operator with no native C# spelling: it lowers through the
    /// receiver's `__matmul__` (`MatMul` method). Deleting `OperatorValidator.ValidateAugmentedAssignment`
    /// (#1631 Task 3) removed that validator's explicit `MatMul` deferral, so this control is
    /// load-bearing: the augmented form must resolve through the same dunder lookup the binary form
    /// does and both must run. The operator is gated behind the experimental `matmul` feature.
    /// </summary>
    [Fact]
    public void MatMulPositiveControlBothAcceptAndMatch()
    {
        const string prelude = @"
class Vec:
    x: int

    def __init__(self, x: int):
        self.x = x

    def __matmul__(self, other: Vec) -> Vec:
        return Vec(self.x * other.x)
";
        var augSource = prelude + @"
def main() -> None:
    a: Vec = Vec(3)
    b: Vec = Vec(4)
    a @= b
    print(a.x)
";
        var binSource = prelude + @"
def main() -> None:
    a: Vec = Vec(3)
    b: Vec = Vec(4)
    a = a @ b
    print(a.x)
";
        var features = FeatureFlags.None.Enable("matmul");
        var augResult = CompileAndExecute(augSource, features: features);
        var binResult = CompileAndExecute(binSource, features: features);

        augResult.Success.Should().BeTrue("augmented `Vec @= Vec` should accept under the matmul feature: "
            + string.Join("; ", augResult.CompilationErrors));
        binResult.Success.Should().BeTrue("binary `Vec @ Vec` should accept under the matmul feature: "
            + string.Join("; ", binResult.CompilationErrors));
        augResult.StandardOutput.Trim().Should().Be("12");
        binResult.StandardOutput.Trim().Should().Be("12");

        // Ungated twin: the same program without the feature is refused by the gate (SPY0331) on
        // both spellings — the control proves the flag, not the operator, is what it toggles.
        CompileAndExecute(augSource).RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);
        CompileAndExecute(binSource).RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.FeatureNotEnabled);
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
