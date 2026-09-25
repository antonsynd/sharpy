using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// '@static' on a member whose FIRST parameter is <c>self</c> is SPY0322 (#2026; decorators.md,
/// static_methods.md): member {method, property getter, property setter, event add/remove} × host
/// {class, struct, interface} × use {called on an instance, called on the type, never called, body
/// reads <c>self</c>}. The arm lives in <c>DecoratorValidator</c> (ValidationPipeline, after the
/// TypeChecker), so it must fire whether or not the checker already refused the use.
///
/// <para><b>Direction (measured on the prior commit's binary).</b> method: instance → CS0176 behind
/// SPY0908, type → SPY0220 (the checker counts <c>self</c> as a formal), never → RAN, body → CS0026
/// behind SPY0908 (struct/class) / RAN (interface); property getter: instance → RAN (printed 3), type →
/// CS0120 behind SPY0908, never/body → RAN; property setter: instance/never/body → RAN in class and
/// struct, type → SPY0290; interface setter and every interface event cell → CS0102/CS0535 behind
/// SPY0908 with or without the decorator (#2067); class/struct event: instance/never/body → RAN, type
/// → CS0120 behind SPY0908. Every "RAN" cell is a worked → refused move the spec mandates.</para>
///
/// <para><b>Predicate controls.</b> The predicate is the TypeChecker's — first parameter NAMED
/// <c>self</c>, type annotation ignored — so a typed <c>self: int</c> first is refused too, while a
/// <c>self</c> that is not first is not this rule's business (it is #2051's CS1501/CS0026 behind
/// SPY0908). No decorator, and '@static' without <c>self</c>, run.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class StaticDecoratorSelfMatrixTests : IntegrationTestBase
{
    public StaticDecoratorSelfMatrixTests(ITestOutputHelper output) : base(output) { }

    public enum Member { Method, PropertyGet, PropertySet, EventAccessors }

    public enum Host { Class, Struct, Interface }

    public enum Use { OnInstance, OnType, NeverCalled, BodyReadsSelf }

    private const string Message =
        "'@static' cannot be applied to a member whose first parameter is 'self' — remove '@static' or drop 'self'";

    private static string Program(Member member, Host host, Use use)
    {
        var reads = use == Use.BodyReadsSelf;
        var header = host switch
        {
            Host.Class => "class C:\n",
            Host.Struct => "struct C:\n",
            _ => "interface C:\n",
        };
        var body = host == Host.Interface ? "" : "    _n: int = 3\n\n";
        string call;
        switch (member)
        {
            case Member.Method:
                body += "    @static\n    def add(self, x: int) -> int:\n        return " + (reads ? "x + self.k()" : "x") + "\n\n";
                call = use switch
                {
                    Use.OnInstance => "print(c.add(1))",
                    Use.OnType => "print(C.add(0, 1))",
                    _ => "print(0)",
                };
                break;
            case Member.PropertyGet:
                body += "    @static\n    property get size(self) -> int:\n        return " + (reads ? "self.k()" : "3") + "\n\n";
                call = use switch
                {
                    Use.OnInstance => "print(c.size)",
                    Use.OnType => "print(C.size)",
                    _ => "print(0)",
                };
                break;
            case Member.PropertySet:
                body += "    property get size(self) -> int:\n        return 3\n\n";
                body += "    @static\n    property set size(self, v: int):\n        " + (reads ? "self.k()" : "pass") + "\n\n";
                call = use switch
                {
                    Use.OnInstance => "c.size = 4\n    print(0)",
                    Use.OnType => "C.size = 4\n    print(0)",
                    _ => "print(0)",
                };
                break;
            default:
                body += "    @static\n    event add e(self, h: Handler):\n        " + (reads ? "self.k()" : "pass") + "\n\n";
                body += "    @static\n    event remove e(self, h: Handler):\n        pass\n\n";
                call = use switch
                {
                    Use.OnInstance => "c.e += f\n    print(0)",
                    Use.OnType => "C.e += f\n    print(0)",
                    _ => "print(0)",
                };
                break;
        }
        body += "    def k(self) -> int:\n        return 1\n";

        var implementer = host == Host.Interface ? "class D(C):\n    pass\n\n" : "";
        var construct = host == Host.Interface ? "D()" : "C()";
        return "delegate Handler() -> None\n\ndef f() -> None:\n    pass\n\n"
            + header + body + "\n" + implementer
            + $"def main() -> None:\n    c: C = {construct}\n    {call}\n";
    }

    /// <summary>1-based lines of every '@static' decorator in <paramref name="source"/>.</summary>
    private static List<int> StaticDecoratorLines(string source)
        => source.Split('\n')
            .Select((line, i) => (line, i))
            .Where(p => p.line.Trim() == "@static")
            .Select(p => p.i + 1)
            .ToList();

    public static IEnumerable<object[]> Cells()
        => from member in Enum.GetValues<Member>()
           from host in Enum.GetValues<Host>()
           from use in Enum.GetValues<Use>()
           select new object[] { member, host, use };

    [Theory]
    [MemberData(nameof(Cells))]
    public void StaticWithSelf_IsRefusedAtEveryDecorator(Member member, Host host, Use use)
    {
        var source = Program(member, host, use);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{member}×{host}×{use}]\n{source}");
        var refusals = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Semantic.InvalidDecoratorUsage)
            .ToList();
        refusals.Should().OnlyContain(d => d.Message == Message,
            $"[{member}×{host}×{use}] {string.Join(" | ", result.CompilationErrors)}");
        refusals.Select(d => d.Line).Should().BeEquivalentTo(
            StaticDecoratorLines(source).Select(l => (int?)l),
            $"[{member}×{host}×{use}] one SPY0322 at each '@static' decorator. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{member}×{host}×{use}] the refusal is semantic, not CS0176/CS0120/CS0026 behind SPY0908");
    }

    // ── Predicate controls ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void TypedSelfFirst_IsRefused_TheCheckersPredicateIsTheName()
    {
        // The TypeChecker makes a method an instance method when its first parameter is NAMED self,
        // whatever its annotation (TypeChecker.Definitions.cs), so the refusal follows that rule.
        // Prior commit: SPY0222/SPY0220 at the call. (Other "has self" spellings diverge — #2051.)
        var source = "class C:\n    @static\n    def add(self: int, x: int) -> int:\n        return x\n\n"
            + "def main() -> None:\n    print(0)\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().ContainSingle(
            d => d.Code == DiagnosticCodes.Semantic.InvalidDecoratorUsage && d.Message == Message && d.Line == 2,
            string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void SelfNotFirst_IsNotThisRule()
    {
        // `self` in second position does not make the method an instance method under the checker's
        // predicate, so SPY0322 must stay silent. The program is still broken (CS0026 behind SPY0908,
        // #2051) — asserted, so this absence is not vacuous: the pipeline did reach a verdict. The
        // positive control is every Cells() row, which differs from this program only in the order.
        var source = "class C:\n    @static\n    def add(x: int, self: int) -> int:\n        return x\n\n"
            + "def main() -> None:\n    print(C.add(1, 2))\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.InvalidDecoratorUsage,
            string.Join(" | ", result.CompilationErrors));
        result.Success.Should().BeFalse("#2051 — self-not-first under @static is still CS0026/CS1501 behind SPY0908");
    }

    [Theory]
    [InlineData("class C:\n    def add(self, x: int) -> int:\n        return x\n\ndef main() -> None:\n    print(C().add(1))\n")]
    [InlineData("class C:\n    @static\n    def add(x: int) -> int:\n        return x\n\ndef main() -> None:\n    print(C.add(1))\n")]
    [InlineData("struct C:\n    @static\n    def add(x: int) -> int:\n        return x\n\ndef main() -> None:\n    print(C.add(1))\n")]
    public void NoDecoratorOrNoSelf_Runs(string source)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("1\n");
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        // 4 member kinds × 3 hosts × 4 uses.
        Cells().Should().HaveCount(48);
    }
}
