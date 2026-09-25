using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Interface member names and the underscore convention (#2033, R-CA): interface members are public
/// in .NET, so a bare <c>_m</c>/<c>__m</c> interface member is SPY0707 at the declaration, and a
/// backtick-escaped <c>`_m`</c>/<c>`__m`</c> is a literal — public, verbatim — on the interface and on
/// every implementer (the escape means "no convention" on EVERY host, ruling 2).
///
/// <para><b>Cells.</b> spelling {<c>_m</c>, <c>__m</c>, <c>`_m`</c>, <c>`__m`</c>} × member {method,
/// property, event} × implementing host {class, struct} × use {declared only, default body, implemented
/// and called through the interface, inner call from the implementer, outside call on the implementer}
/// × interface nesting {top-level, nested in a class} = 240 programs. Bare → exactly one SPY0707, at
/// the interface member's name; escaped → runs and prints 7, and the implementer's member is emitted
/// <c>public</c>. Uses are spelled like the declaration (escaped declaration → escaped uses; a bare
/// use of an escaped member is #2046).</para>
///
/// <para><b>Direction (measured on the prior commit's binary, all 240 cells).</b> Every cell failed:
/// 120 CS0737 behind SPY0908 (the implementer's <c>_m</c> emitted protected), 96 SPY0283 (outside and
/// through-interface calls refused by the convention), 8 CS0621 (<c>__m</c> in a class → private
/// virtual), 16 CS0535 (event default bodies, #2067) — measured with bare uses; the escaped-use twins
/// of the escaped cells are listed in the commit body. Bare cells: SPY0908/SPY0283 → SPY0707. Escaped
/// cells: → run (the relaxation ruling 2 names), except the 8 escaped event default-body cells, which
/// stay CS0535 behind SPY0908 until #2067 (pinned below so its fix is visible).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class InterfaceMemberNameMatrixTests : IntegrationTestBase
{
    public InterfaceMemberNameMatrixTests(ITestOutputHelper output) : base(output) { }

    public enum Spelling { Protected, Private, EscapedProtected, EscapedPrivate }

    public enum Member { Method, Property, Event }

    public enum Host { Class, Struct }

    public enum Use { DeclaredOnly, DefaultBody, Implemented, InnerCall, OutsideCall }

    public enum Nesting { TopLevel, Nested }

    private static bool IsEscaped(Spelling s) => s is Spelling.EscapedProtected or Spelling.EscapedPrivate;

    private static string Name(Spelling s) => s is Spelling.Protected or Spelling.EscapedProtected ? "_m" : "__m";

    private static string Declared(Spelling s) => IsEscaped(s) ? $"`{Name(s)}`" : Name(s);

    /// <summary>Indents every non-blank line of <paramref name="text"/> by <paramref name="prefix"/>.</summary>
    private static string Indent(string text, string prefix)
        => string.Join("\n", text.Split('\n').Select(l => l.Trim().Length == 0 ? l : prefix + l));

    public static string Program(Spelling spelling, Member member, Host host, Use use, Nesting nesting)
    {
        var decl = Declared(spelling);
        // Every use is spelled like the declaration: an escaped declaration referenced bare emits the
        // mangled name (CS1061 behind SPY0908) — #2046, the closed #478 cell, not this matrix's axis.
        var name = decl;
        var defaultBody = use == Use.DefaultBody;
        var iname = nesting == Nesting.Nested ? "O.I" : "I";

        var interfaceMember = member switch
        {
            Member.Method => defaultBody ? $"def {decl}(self) -> int:\n    return 7\n" : $"def {decl}(self) -> int: ...\n",
            Member.Property => defaultBody
                ? $"property get {decl}(self) -> int:\n    return 7\n"
                : $"property get {decl}(self) -> int: ...\n",
            _ => defaultBody
                ? $"event add {decl}(self, h: Handler):\n    pass\n\nevent remove {decl}(self, h: Handler):\n    pass\n"
                : $"event {decl}: Handler\n",
        };
        var implementation = member switch
        {
            Member.Method => $"    def {decl}(self) -> int:\n        return 7\n\n",
            Member.Property => $"    property get {decl}(self) -> int:\n        return 7\n\n",
            _ => $"    event {decl}: Handler\n\n",
        };
        string UseOn(string receiver) => member switch
        {
            Member.Method => $"print({receiver}.{name}())",
            Member.Property => $"print({receiver}.{name})",
            _ => $"{receiver}.{name} += f\n    print(7)",
        };

        var head = "delegate Handler() -> None\n\ndef f() -> None:\n    pass\n\n";
        head += nesting == Nesting.Nested
            ? "class O:\n    interface I:\n" + Indent(interfaceMember, "        ") + "\n"
            : "interface I:\n" + Indent(interfaceMember, "    ") + "\n";

        var body = defaultBody ? "" : implementation;
        if (use == Use.InnerCall)
        {
            body += member switch
            {
                Member.Method => $"    def run(self) -> None:\n        print(self.{name}())\n\n",
                Member.Property => $"    def run(self) -> None:\n        print(self.{name})\n\n",
                _ => $"    def run(self) -> None:\n        self.{name} += f\n        print(7)\n\n",
            };
        }
        if (body.Length == 0)
            body = "    pass\n\n";

        var main = use switch
        {
            Use.DeclaredOnly => "    c = C()\n    print(7)\n",
            Use.DefaultBody or Use.Implemented => $"    i: {iname} = C()\n    " + UseOn("i") + "\n",
            Use.InnerCall => "    C().run()\n",
            _ => "    c = C()\n    " + UseOn("c") + "\n",
        };
        return head + $"{(host == Host.Class ? "class" : "struct")} C({iname}):\n" + body + "def main() -> None:\n" + main;
    }

    public static IEnumerable<object[]> Cells()
        => from spelling in Enum.GetValues<Spelling>()
           from member in Enum.GetValues<Member>()
           from host in Enum.GetValues<Host>()
           from use in Enum.GetValues<Use>()
           from nesting in Enum.GetValues<Nesting>()
           select new object[] { spelling, member, host, use, nesting };

    /// <summary>
    /// An interface event with default accessor bodies drops them in emission (#2067) — CS0535 with
    /// or without an underscore. Pinned rather than skipped: fixing #2067 turns these red.
    /// </summary>
    private static bool IsKnownDefaultEventDefect(Spelling spelling, Member member, Use use)
        => IsEscaped(spelling) && member == Member.Event && use == Use.DefaultBody;

    [Theory]
    [MemberData(nameof(Cells))]
    public void Cell(Spelling spelling, Member member, Host host, Use use, Nesting nesting)
    {
        var source = Program(spelling, member, host, use, nesting);
        var result = CompileAndExecute(source);
        var label = $"[{spelling}×{member}×{host}×{use}×{nesting}]";

        if (!IsEscaped(spelling))
        {
            result.Success.Should().BeFalse($"{label}\n{source}");
            var refusals = result.RawDiagnostics
                .Where(d => d.Code == DiagnosticCodes.ValidationOverflow.InterfaceMemberUnderscoreName)
                .ToList();
            refusals.Should().ContainSingle($"{label} one SPY0707, at the interface declaration. "
                + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            var kind = member.ToString().ToLowerInvariant();
            refusals[0].Message.Should().StartWith($"Interface {kind} '{Name(spelling)}' in 'I' is named like a "
                + (spelling == Spelling.Private ? "private" : "protected") + " member");
            refusals[0].Message.Should().EndWith($"backtick-escape the name (`{Name(spelling)}`) to keep the spelling as a public member.");
            refusals[0].Line.Should().Be(nesting == Nesting.Nested ? 8 : 7, $"{label} at the member's name");
            result.RawDiagnostics.Should().NotContain(
                d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
                $"{label} the refusal is semantic, not CS0737/CS0621 behind SPY0908");
            return;
        }

        if (IsKnownDefaultEventDefect(spelling, member, use))
        {
            result.Success.Should().BeFalse($"{label} #2067 still open?\n{source}");
            result.CompilationErrors.Should().Contain(e => e.Contains("CS0535"),
                $"{label} #2067: the default event accessors are dropped");
            return;
        }

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.ValidationOverflow.InterfaceMemberUnderscoreName,
            $"{label} an escaped name is a literal");
        result.Success.Should().BeTrue($"{label} {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("7\n", $"{label}\n{source}");

        if (use != Use.DefaultBody)
        {
            var implementer = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot()
                .DescendantNodes().OfType<TypeDeclarationSyntax>().Single(t => t.Identifier.Text == "C");
            AccessOfVerbatim(implementer, Name(spelling)).Should().Be(SyntaxKind.PublicKeyword,
                $"{label} the escaped member is public verbatim on the implementer\n{implementer}");
        }
    }

    // ── Mixed escape: the two spellings emit different C# names — refused by name ─────────────

    public static IEnumerable<object[]> MixedEscapeCells() => new[]
    {
        new object[] { "interface_escaped_impl_bare",
            "interface I:\n    def `m`(self) -> int: ...\n\nclass C(I):\n    def m(self) -> int:\n        return 7\n\n"
            + "def main() -> None:\n    i: I = C()\n    print(i.m())\n",
            DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "Class 'C' does not implement interface method 'I.m': it is declared as `m` but implemented as m; implement it with the same spelling" },
        new object[] { "interface_bare_impl_escaped",
            "interface I:\n    def m(self) -> int: ...\n\nclass C(I):\n    def `m`(self) -> int:\n        return 7\n\n"
            + "def main() -> None:\n    i: I = C()\n    print(i.m())\n",
            DiagnosticCodes.Semantic.InterfaceMethodNotImplemented,
            "Class 'C' does not implement interface method 'I.m': it is declared as m but implemented as `m`; implement it with the same spelling" },
        new object[] { "base_escaped_override_bare",
            "class B:\n    @virtual\n    def `_m`(self) -> int:\n        return 1\n\n"
            + "class D(B):\n    @override\n    def _m(self) -> int:\n        return 7\n\n"
            + "def main() -> None:\n    print(D()._m())\n",
            DiagnosticCodes.Semantic.InvalidOverride,
            "Method _m overrides 'B._m', which is declared as `_m`; implement it with the same spelling" },
        new object[] { "base_bare_override_escaped",
            "class B:\n    @virtual\n    def _m(self) -> int:\n        return 1\n\n"
            + "class D(B):\n    @override\n    def `_m`(self) -> int:\n        return 7\n\n    def run(self) -> int:\n        return self._m()\n\n"
            + "def main() -> None:\n    print(D().run())\n",
            DiagnosticCodes.Semantic.InvalidOverride,
            "Method `_m` overrides 'B._m', which is declared as _m; implement it with the same spelling" },
    };

    [Theory]
    [MemberData(nameof(MixedEscapeCells))]
    public void MixedEscape_IsRefusedByName(string name, string source, string code, string message)
    {
        // Prior commit: CS0535 (interface) / CS0115 (override) behind SPY0908.
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{name}]\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == code && d.Message == message,
            $"[{name}] {string.Join(" | ", result.CompilationErrors)}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{name}]");
    }

    [Fact]
    public void MixedEscape_ThatEmitsTheSameCSharpName_Runs()
    {
        // Control for the predicate: `Run` escaped and bare both emit `Run`, so the implementation
        // binds and nothing is refused (prior commit: runs, prints 7).
        var source = "interface I:\n    def `Run`(self) -> int: ...\n\nclass C(I):\n    def Run(self) -> int:\n        return 7\n\n"
            + "def main() -> None:\n    i: I = C()\n    print(i.Run())\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.Should().Be("7\n");
    }

    // ── The escape means "no convention" on every host (ruling 2) ─────────────────────────────

    public static IEnumerable<object[]> PlainHostEscapeCells() => new[]
    {
        // Prior commit: SPY0283 for all three — the relaxation ruling 2 names.
        new object[] { "class_method", "class C:\n    def `_m`(self) -> int:\n        return 7\n\ndef main() -> None:\n    print(C().`_m`())\n" },
        new object[] { "struct_method", "struct C:\n    def `_m`(self) -> int:\n        return 7\n\ndef main() -> None:\n    print(C().`_m`())\n" },
        new object[] { "class_property", "class C:\n    property get `__m`(self) -> int:\n        return 7\n\ndef main() -> None:\n    print(C().`__m`)\n" },
    };

    [Theory]
    [MemberData(nameof(PlainHostEscapeCells))]
    public void EscapedUnderscoreMember_OnAPlainHost_IsPublic(string name, string source)
    {
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"[{name}] {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("7\n", $"[{name}]");
    }

    [Fact]
    public void BareUnderscoreMember_OnAPlainHost_IsStillRefusedOutside()
    {
        // Positive control for the relaxation: the bare spelling keeps its convention.
        var source = "class C:\n    def _m(self) -> int:\n        return 7\n\ndef main() -> None:\n    print(C()._m())\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.AccessViolation,
            string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void EscapedMember_InheritedFromABase_IsStillRefusedThroughTheSubclass()
    {
        // Known residual (#2074): AccessValidator looks only at the receiver type's own members, so
        // the base's escape (like a base's @public) is not seen through a subclass receiver. Pinned
        // so the #2074 fix is a visible direction change. Prior commit: SPY0283 (unchanged).
        var source = "class B:\n    def `_m`(self) -> int:\n        return 7\n\nclass D(B):\n    pass\n\n"
            + "def main() -> None:\n    print(D().`_m`())\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.AccessViolation,
            string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void NestedInterface_DeclaringADunder_IsSpy0413LikeItsTopLevelTwin()
    {
        // The nested walk (#1461 class) — positive control that SignatureValidator now reaches a
        // nested interface at all. Prior commit: CS0535 behind SPY0908.
        var source = "class O:\n    interface I:\n        def __len__(self) -> int: ...\n\n"
            + "class C(O.I):\n    def __len__(self) -> int:\n        return 7\n\n"
            + "def main() -> None:\n    print(len(C()))\n";
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Validation.DunderInUserInterface,
            string.Join(" | ", result.CompilationErrors));
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        Cells().Should().HaveCount(4 * 3 * 2 * 5 * 2);
        MixedEscapeCells().Should().HaveCount(4);
        PlainHostEscapeCells().Should().HaveCount(3);
    }

    /// <summary>The access keyword of the one direct member of <paramref name="type"/> spelled <paramref name="name"/>.</summary>
    private static SyntaxKind AccessOfVerbatim(TypeDeclarationSyntax type, string name)
    {
        var member = type.Members.Single(m => m switch
        {
            MethodDeclarationSyntax md => md.Identifier.Text == name,
            PropertyDeclarationSyntax pd => pd.Identifier.Text == name,
            EventDeclarationSyntax ed => ed.Identifier.Text == name,
            EventFieldDeclarationSyntax ef => ef.Declaration.Variables.Any(v => v.Identifier.Text == name),
            _ => false,
        });
        return member.Modifiers.Select(m => m.Kind()).Single(k => k is SyntaxKind.PublicKeyword
            or SyntaxKind.PrivateKeyword or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword);
    }
}
