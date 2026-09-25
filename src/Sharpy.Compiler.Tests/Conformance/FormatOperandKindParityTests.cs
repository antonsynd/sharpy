extern alias SharpyRT;

using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using FormatOperandKind = SharpyRT::Sharpy.FormatOperandKind;
using PyFormat = SharpyRT::Sharpy.PyFormat;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The format operand kind has ONE table, Core's <c>PyFormat.KindOf(Type)</c> (#1988 regression,
/// plan-bf0244 verify; R-BX: the static and runtime twins are the same code). The compiler's static
/// projection <see cref="TypeChecker.FormatOperandOfClr"/> must give every statically-known CLR type
/// the kind the runtime gives its values — before the fix it restated the table arm by arm, and the
/// two diverged when Core's integral arm was the eight primitives while a
/// <c>System.Numerics.BigInteger</c> fell to the IFormattable arm.
/// </summary>
[Collection("HeavyCompilation")]
public class FormatOperandKindParityTests : IntegrationTestBase
{
    public FormatOperandKindParityTests(ITestOutputHelper output) : base(output) { }

    private enum Color { Red }

    /// <summary>
    /// Concrete CLR types a hole can statically hold, each paired with the kind it must take —
    /// anchored to literals, so a table that drifts on both sides at once still reddens.
    /// </summary>
    private static readonly (Type Type, FormatOperandKind Expected)[] Roster =
    {
        (typeof(bool), FormatOperandKind.Bool),
        (typeof(int), FormatOperandKind.Integral),
        (typeof(long), FormatOperandKind.Integral),
        (typeof(short), FormatOperandKind.Integral),
        (typeof(byte), FormatOperandKind.Integral),
        (typeof(sbyte), FormatOperandKind.Integral),
        (typeof(uint), FormatOperandKind.Integral),
        (typeof(ulong), FormatOperandKind.Integral),
        (typeof(ushort), FormatOperandKind.Integral),
        (typeof(System.Numerics.BigInteger), FormatOperandKind.Integral),
        (typeof(Int128), FormatOperandKind.Integral),
        (typeof(UInt128), FormatOperandKind.Integral),
        (typeof(nint), FormatOperandKind.Integral),
        (typeof(nuint), FormatOperandKind.Integral),
        (typeof(double), FormatOperandKind.Float),
        (typeof(float), FormatOperandKind.Float),
        (typeof(decimal), FormatOperandKind.Float),
        (typeof(Half), FormatOperandKind.Float),
        (typeof(string), FormatOperandKind.Str),
        (typeof(char), FormatOperandKind.Str),
        (typeof(Color), FormatOperandKind.Str),
        (typeof(DayOfWeek), FormatOperandKind.Str),
        (typeof(SharpyRT::Sharpy.Complex), FormatOperandKind.Complex),
        (typeof(Guid), FormatOperandKind.Formattable),
        (typeof(TimeSpan), FormatOperandKind.Formattable),
        (typeof(DateTime), FormatOperandKind.Formattable),
        (typeof(System.Numerics.Complex), FormatOperandKind.Formattable),
        (typeof(SharpyRT::Sharpy.List<int>), FormatOperandKind.NoFormat),
        (typeof(SharpyRT::Sharpy.Dict<string, int>), FormatOperandKind.NoFormat),
        (typeof(SharpyRT::Sharpy.Set<int>), FormatOperandKind.NoFormat),
        (typeof(SharpyRT::Sharpy.Bytes), FormatOperandKind.NoFormat),
        (typeof(SharpyRT::Sharpy.Optional<int>), FormatOperandKind.NoFormat),
        (typeof(ValueTuple<int, int>), FormatOperandKind.NoFormat),
    };

    [Fact]
    [Trait("Category", "Conformance")]
    public void StaticProjection_IsCoresKindTable_ForEveryConcreteType()
    {
        var failures = new List<string>();
        foreach (var (type, expected) in Roster)
        {
            var runtime = PyFormat.KindOf(type);
            var projected = TypeChecker.FormatOperandOfClr(type);
            if (runtime != expected)
                failures.Add($"{type.Name}: Core KindOf(Type) = {runtime}, expected {expected}");
            if (projected.Kind != runtime)
                failures.Add($"{type.Name}: compiler projection = {projected.Kind}, Core KindOf(Type) = {runtime}");
            if (projected.PyTypeName != PyFormat.FormatOperandTypeName(type))
                failures.Add($"{type.Name}: compiler names it '{projected.PyTypeName}', Core '{PyFormat.FormatOperandTypeName(type)}'");
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The one thing the compiler adds: a static type whose values may be of another runtime class is
    /// not statically known (never refused early). The positive control — a concrete no-format type
    /// in the roster above — is refused, so this is not vacuous.
    /// </summary>
    [Theory]
    [Trait("Category", "Conformance")]
    [InlineData(typeof(object))]
    [InlineData(typeof(ValueType))]
    [InlineData(typeof(IComparable))]
    [InlineData(typeof(System.IO.Stream))]
    [InlineData(typeof(int?))]
    public void StaticProjection_OfAnOpenType_IsUnknown(Type type)
    {
        Assert.Equal(FormatOperandKind.Unknown, TypeChecker.FormatOperandOfClr(type).Kind);
    }

    /// <summary>
    /// End to end through the f-string route: a <c>BigInteger</c> hole is an <c>int</c> statically
    /// (<c>d</c> compiles and prints python's digits, <c>s</c> is SPY0609 naming <c>int</c>) and a
    /// <c>Guid</c> hole owns its spec (not refused statically; the spec Guid rejects is a catchable
    /// ValueError at runtime).
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void FString_ClrIntegerAndFormattableHoles_TakeCoresKind()
    {
        const string prelude = "from System import Guid\nfrom System.Numerics import BigInteger\n\n\n"
            + "def main() -> None:\n"
            + "    b: BigInteger = BigInteger.parse(\"1000000000000000000000000000000\")\n"
            + "    g: Guid = Guid.Empty\n";

        // python3 -c "print(format(10**30, 'd'))" => 1000000000000000000000000000000
        // python3 -c "print(format(10**30, ','))" => 1,000,000,000,000,000,000,000,000,000,000
        var ok = CompileAndExecute(prelude
            + "    print(f\"[{b:d}]\")\n    print(f\"[{b:,}]\")\n"
            + "    try:\n        print(f\"[{g:>40}]\")\n    except ValueError as e:\n        print(e)\n",
            executionTimeoutMs: 15_000);
        Assert.True(ok.Success, string.Join("; ", ok.CompilationErrors) + ok.StandardError);
        Assert.Equal(
            "[1000000000000000000000000000000]\n[1,000,000,000,000,000,000,000,000,000,000]\n"
            + "Invalid format specifier '>40' for object of type 'Guid'",
            ok.StandardOutput.TrimEnd().Replace("\r\n", "\n"));

        // python3 -c "format(10**30, 's')" => ValueError: Unknown format code 's' for object of type 'int'
        var refused = CompileAndExecute(prelude + "    print(f\"[{b:s}]\")\n    print(g)\n", executionTimeoutMs: 15_000);
        var spy0609 = refused.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification).ToList();
        Assert.Single(spy0609);
        Assert.Equal("Unknown format code 's' for object of type 'int'", spy0609[0].Message);
    }

    /// <summary>
    /// #2014: the kind table's name column says <c>decimal</c> for <c>System.Decimal</c> — it is
    /// formatted by the float rules, but python's twin (<c>decimal.Decimal</c>) has its own grammar
    /// (the <c>decimal-format-grammar</c> deviation), so naming it <c>float</c> named a type the value
    /// is not. Every consumer of the one name moves together: the static SPY0609 (literal spec), the
    /// runtime ValueError (spec in a variable) and the subscript TypeError. <c>float32</c> and the
    /// integer widths keep <c>float</c>/<c>int</c> — they have python twins under those names — and
    /// <c>.2f</c> is the accepted control.
    /// </summary>
    [Theory]
    [Trait("Category", "Conformance")]
    [InlineData("decimal", "decimal(1.5)", "d", "Unknown format code 'd' for object of type 'decimal'")]
    [InlineData("decimal", "decimal(1.5)", "s", "Unknown format code 's' for object of type 'decimal'")]
    [InlineData("decimal", "decimal(1.5)", "#c", "Unknown format code 'c' for object of type 'decimal'")]
    [InlineData("decimal", "decimal(1.5)", ".2f", null)]
    [InlineData("float32", "float32(1.5)", "d", "Unknown format code 'd' for object of type 'float'")]
    [InlineData("int8", "int8(1)", "s", "Unknown format code 's' for object of type 'int'")]
    public void FormatRefusal_NamesTheOperandType_StaticAndRuntime(string type, string init, string spec, string? refusal)
    {
        string decl = $"def main() -> None:\n    v: {type} = {init}\n";

        var runtime = CompileAndExecute(decl
            + $"    spec: str = \"{spec}\"\n"
            + "    try:\n        print(format(v, spec))\n    except ValueError as e:\n        print(\"ValueError:\", e)\n",
            executionTimeoutMs: 15_000);
        Assert.True(runtime.Success, string.Join("; ", runtime.CompilationErrors) + runtime.StandardError);
        Assert.Equal(refusal is null ? "1.50" : "ValueError: " + refusal, runtime.StandardOutput.TrimEnd());

        var literal = CompileAndExecute(decl + $"    print(format(v, \"{spec}\"))\n", executionTimeoutMs: 15_000);
        var spy0609 = literal.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification)
            .Select(d => d.Message)
            .ToList();
        if (refusal is null)
        {
            Assert.True(literal.Success, string.Join("; ", literal.CompilationErrors));
            Assert.Equal("1.50", literal.StandardOutput.TrimEnd());
        }
        else
        {
            Assert.Equal(new[] { refusal }, spy0609);
        }
    }

    /// <summary>#2014: the subscript message reads the same name column.</summary>
    [Theory]
    [Trait("Category", "Conformance")]
    [InlineData("decimal", "decimal(1.5)", "'decimal' object is not subscriptable")]
    [InlineData("float32", "float32(1.5)", "'float' object is not subscriptable")]
    [InlineData("int8", "int8(1)", "'int' object is not subscriptable")]
    public void FieldSubscript_NamesTheOperandType(string type, string init, string message)
    {
        var result = CompileAndExecute($"def main() -> None:\n    v: {type} = {init}\n"
            + "    try:\n        print(\"{0[0]}\".format(v))\n    except TypeError as e:\n        print(\"TypeError:\", e)\n",
            executionTimeoutMs: 15_000);
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors) + result.StandardError);
        Assert.Equal("TypeError: " + message, result.StandardOutput.TrimEnd());
    }
}
