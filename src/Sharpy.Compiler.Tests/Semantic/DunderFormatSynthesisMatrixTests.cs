using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// <c>def __format__(self, spec: str) -> str</c> synthesizes <c>System.IFormattable</c> (#2009, R-CB):
/// the body MOVES into <c>ToString(string? spec, IFormatProvider? formatProvider = null)</c>, SPY1001
/// announces the interface, and every format route reaches the user's code with python's spec.
///
/// <para><b>Cells.</b> host {class, struct, generic class, subclass <c>@override</c>,
/// <c>super().__format__</c>} × spec {empty, non-empty, one the body refuses with ValueError} × route
/// {f-string hole, <c>format()</c>, <c>str.format</c>, t-string} = 60, one program per host. Expected
/// values are python3's (3.12, the same program with the struct as a class and without
/// <c>@override</c>; the t-string route has no python twin — Sharpy's <c>str(template)</c> formats each
/// hole — and must agree with the other three). Plus: <c>__str__</c> beside <c>__format__</c> (an
/// overload, not a collision), the explicit <c>IFormattable</c>/two-argument <c>to_string</c> spelling
/// beside <c>__format__</c> (SPY0522, not CS0111), a one-argument <c>to_string</c> control, and the
/// parameter-type refusal.</para>
///
/// <para><b>Direction (prior commit's binary).</b> Every <c>__format__</c> program was SPY0414 (unknown
/// dunder) plus one SPY0609 per literal non-empty spec ("unsupported format string passed to
/// F.__format__") → runs. The collision programs were SPY0414 → SPY0522; <c>spec: int</c> SPY0414 →
/// SPY0320.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class DunderFormatSynthesisMatrixTests : IntegrationTestBase
{
    public DunderFormatSynthesisMatrixTests(ITestOutputHelper output) : base(output) { }

    public enum Host { Class, Struct, GenericClass, SubclassOverride, SuperCall }

    private const string Raise = "        if spec == \"bad\":\n            raise ValueError(\"bad spec\")\n";

    private static (string Declarations, string Construct, string Tag, string Suffix) HostShape(Host host) => host switch
    {
        Host.Class => ("class F:\n    def __format__(self, spec: str) -> str:\n" + Raise
            + "        return \"F<\" + spec + \">\"\n", "F()", "F", ""),
        Host.Struct => ("struct F:\n    n: int\n\n    def __format__(self, spec: str) -> str:\n" + Raise
            + "        return \"S<\" + spec + \">\" + str(self.n)\n", "F(1)", "S", "1"),
        Host.GenericClass => ("class F[T]:\n    v: T\n\n    def __init__(self, v: T) -> None:\n        self.v = v\n\n"
            + "    def __format__(self, spec: str) -> str:\n" + Raise
            + "        return \"G<\" + spec + \">\" + str(self.v)\n", "F[int](5)", "G", "5"),
        Host.SubclassOverride => ("class B:\n    def __format__(self, spec: str) -> str:\n        return \"B<\" + spec + \">\"\n\n"
            + "class F(B):\n    @override\n    def __format__(self, spec: str) -> str:\n" + Raise
            + "        return \"D<\" + spec + \">\"\n", "F()", "D", ""),
        _ => ("class B:\n    def __format__(self, spec: str) -> str:\n" + Raise
            + "        return \"B<\" + spec + \">\"\n\n"
            + "class F(B):\n    @override\n    def __format__(self, spec: str) -> str:\n"
            + "        return \"D\" + super().__format__(spec)\n", "F()", "DB", ""),
    };

    /// <summary>(spec, the four route expressions) — f-string hole, format(), str.format, t-string.</summary>
    private static readonly (string Spec, string[] Routes)[] SpecRoutes =
    {
        ("", new[] { "f\"{x}\"", "format(x, \"\")", "\"{}\".format(x)", "str(t\"{x}\")" }),
        (">5", new[] { "f\"{x:>5}\"", "format(x, \">5\")", "\"{:>5}\".format(x)", "str(t\"{x:>5}\")" }),
        ("bad", new[] { "f\"{x:bad}\"", "format(x, \"bad\")", "\"{:bad}\".format(x)", "str(t\"{x:bad}\")" }),
    };

    private static string Program(Host host)
    {
        var (decls, construct, _, _) = HostShape(host);
        var lines = new List<string> { $"    x = {construct}" };
        foreach (var (_, routes) in SpecRoutes)
        {
            foreach (var route in routes)
                lines.Add($"    try:\n        print({route})\n    except ValueError as err:\n        print(\"ValueError:\", err)");
        }
        return decls + "\ndef main() -> None:\n" + string.Join("\n", lines) + "\n";
    }

    /// <summary>
    /// python3 3.12 oracle, per host, one line per (spec, route): the class twin of each program
    /// prints <c>F&lt;&gt;</c>/<c>F&lt;&gt;5&gt;</c>/<c>ValueError: bad spec</c> (and S…1, G…5, D…,
    /// DB… for the other hosts) on format(), str.format and the f-string hole alike.
    /// </summary>
    private static string Expected(Host host)
    {
        var (_, _, tag, suffix) = HostShape(host);
        var sb = new System.Text.StringBuilder();
        foreach (var (spec, routes) in SpecRoutes)
        {
            var line = spec == "bad" ? "ValueError: bad spec" : $"{tag}<{spec}>{suffix}";
            foreach (var _ in routes)
                sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    public static IEnumerable<object[]> Hosts() => Enum.GetValues<Host>().Select(h => new object[] { h });

    [Theory]
    [MemberData(nameof(Hosts))]
    public void DunderFormat_ReachesEveryRoute_WithPythonsSpec(Host host)
    {
        var source = Program(host);
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Validation.UnknownDunderMethod
                || d.Code == DiagnosticCodes.SemanticOverflow.InvalidFormatSpecification
                || d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue($"[{host}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(Expected(host), $"[{host}]\n{source}");

        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Info.ImplicitInterfaceSynthesis
            && d.Message.Contains("'System.IFormattable' via '__format__'"), $"[{host}] SPY1001 announces the synthesis");

        // The MOVE: F's __format__ body is IFormattable's two-argument ToString, and F lists the interface.
        var f = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>().Single(t => t.Identifier.Text == "F");
        f.BaseList!.Types.Select(t => t.Type.ToString()).Should().Contain(t => t.EndsWith("IFormattable", StringComparison.Ordinal));
        var toString = f.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "ToString");
        toString.ParameterList.Parameters.Should().HaveCount(2);
        toString.ParameterList.Parameters[1].Type!.ToString().Should().EndWith("IFormatProvider?");
        var expectedDispatch = host switch
        {
            Host.Struct => (SyntaxKind?)null,
            Host.SubclassOverride or Host.SuperCall => SyntaxKind.OverrideKeyword,
            _ => SyntaxKind.VirtualKeyword,
        };
        var dispatch = toString.Modifiers.Select(m => (SyntaxKind?)m.Kind())
            .SingleOrDefault(k => k is SyntaxKind.VirtualKeyword or SyntaxKind.OverrideKeyword);
        dispatch.Should().Be(expectedDispatch, $"[{host}] {toString.Modifiers}");
    }

    [Fact]
    public void StrBesideFormat_IsAnOverload_NotACollision()
    {
        // python3: str(x) -> str, f"{x}" -> fmt<>, f"{x!s}" -> str, format(x, "q") -> fmt<q>.
        var source = "class F:\n    def __str__(self) -> str:\n        return \"str\"\n\n"
            + "    def __format__(self, spec: str) -> str:\n        return \"fmt<\" + spec + \">\"\n\n"
            + "def main() -> None:\n    x = F()\n    print(str(x))\n    print(f\"{x}\")\n    print(f\"{x!s}\")\n    print(format(x, \"q\"))\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("str\nfmt<>\nstr\nfmt<q>\n");
    }

    public static IEnumerable<object[]> CollisionCells() => new[]
    {
        // The explicit CLR spelling of the same protocol beside the dunder: two ToString(string?,
        // IFormatProvider?) members (CS0111 without the refusal).
        new object[] { "explicit_iformattable",
            "from System import IFormattable, IFormatProvider\n\n"
            + "class F(IFormattable):\n    def to_string(self, fmt: str, provider: IFormatProvider) -> str:\n        return \"F<\" + fmt + \">\"\n\n"
            + "    def __format__(self, spec: str) -> str:\n        return \"dunder\"\n\n"
            + "def main() -> None:\n    print(f\"{F():x}\")\n" },
        new object[] { "two_argument_to_string",
            "class F:\n    def to_string(self, a: str, b: str) -> str:\n        return a\n\n"
            + "    def __format__(self, spec: str) -> str:\n        return \"dunder\"\n\n"
            + "def main() -> None:\n    print(f\"{F():x}\")\n" },
    };

    [Theory]
    [MemberData(nameof(CollisionCells))]
    public void SecondSpellingOfTheFormatMember_IsSpy0522(string name, string source)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{name}]\n{source}");
        result.RawDiagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.CodeGen.MemberNameCollision
            && d.Message.StartsWith("Name collision: 'to_string' compiles to 'ToString', which conflicts with the System.IFormattable member '__format__' synthesizes", StringComparison.Ordinal),
            $"[{name}] {string.Join(" | ", result.CompilationErrors)}");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void OneArgumentToString_BesideFormat_IsAnOverload()
    {
        // Positive control for the collision predicate: same C# name, other arity.
        var source = "class F:\n    def to_string(self, a: str) -> str:\n        return a\n\n"
            + "    def __format__(self, spec: str) -> str:\n        return \"dunder<\" + spec + \">\"\n\n"
            + "def main() -> None:\n    print(f\"{F():x}\")\n    print(F().to_string(\"one\"))\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("dunder<x>\none\n");
    }

    [Fact]
    public void SpecParameterMustBeStr()
    {
        var source = "class F:\n    def __format__(self, spec: int) -> str:\n        return \"x\"\n\n"
            + "def main() -> None:\n    print(f\"{F()}\")\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().ContainSingle(d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod
            && d.Message == "Parameter 'spec' of '__format__' on 'F' must be 'str', got 'int'.",
            string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void SpecNamedLikeTheProviderParameter_StillRuns()
    {
        // The emitted provider parameter steps aside for a spec parameter spelled `formatProvider`.
        var source = "class F:\n    def __format__(self, formatProvider: str) -> str:\n        return \"p<\" + formatProvider + \">\"\n\n"
            + "def main() -> None:\n    print(f\"{F():z}\")\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("p<z>\n");
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        // 5 hosts × 3 specs × 4 routes = 60 run cells.
        Hosts().Should().HaveCount(5);
        SpecRoutes.Should().HaveCount(3);
        SpecRoutes.Should().OnlyContain(s => s.Routes.Length == 4);
        CollisionCells().Should().HaveCount(2);
    }
}
