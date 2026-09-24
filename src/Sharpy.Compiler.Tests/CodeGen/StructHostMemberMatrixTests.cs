using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Host kind × member × access spelling (#1937): a member's access is classified ONCE, in semantic
/// analysis, with the host axis (<c>MemberClassification.ClassifyAccess</c> — a struct is sealed, so
/// the <c>_name</c> convention's <c>protected</c> is <c>private</c> there), and every emitter site
/// reads the symbol fact. Before, four emitter sites re-derived access from the NAME with no host
/// axis, so a struct's <c>_x</c>, <c>def _m</c>, <c>property get _p</c>, nested <c>class _Inner</c>
/// and <c>event _e</c> all emitted <c>protected</c> — CS0666 behind SPY0908 — and a dataclass's
/// <c>_x</c> property emitted a hard-coded <c>public</c> (#2012).
///
/// <para><b>Cells.</b> One program per host {class, struct, dataclass, frozen dataclass} × spelling
/// {<c>_name</c> convention, <c>@private</c>, <c>@public</c>, <c>@protected</c>}; each program carries
/// the six spelled member kinds {field without default, field with default, method, function-style
/// property getter, nested class, event} plus three unspelled controls {<c>x = 3</c> public,
/// <c>const K</c> public, and — in class/struct — <c>__z</c> private}. Every program EXECUTES and
/// prints each member's value; every member's emitted access is asserted as a <see cref="SyntaxKind"/>
/// on its parsed C# declaration node, never as a substring. <c>@protected</c> in a struct is REFUSED
/// by <c>DecoratorValidator</c> (SPY0415, the <c>@virtual</c>-on-struct code) — one diagnostic per
/// decorated member. The union-case host (fields hard-code <c>public</c>, no decorators) is one extra
/// cell; three <c>AccessValidator</c> cells pin that <c>obj._x</c> from outside a class, struct or
/// dataclass is refused with the same message as before the change (the direction control).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class StructHostMemberMatrixTests : IntegrationTestBase
{
    public StructHostMemberMatrixTests(ITestOutputHelper output) : base(output) { }

    public enum Host { Class, Struct, Dataclass, FrozenDataclass }

    public enum Spelling { Convention, Private, Public, Protected }

    /// <summary>The six member kinds the spelling axis ranges over, by their base name.</summary>
    private static readonly string[] SpelledMembers = { "f1", "f2", "m", "p", "Inner", "e" };

    private static bool HasCtorAndPrivateControl(Host host) => host is Host.Class or Host.Struct;

    private static string Program(Host host, Spelling spelling)
    {
        var n = spelling == Spelling.Convention ? "_" : "";
        var dec = spelling switch
        {
            Spelling.Private => "    @private\n",
            Spelling.Public => "    @public\n",
            Spelling.Protected => "    @protected\n",
            _ => "",
        };
        var header = host switch
        {
            Host.Class => "class H:\n",
            Host.Struct => "struct H:\n",
            Host.Dataclass => "@dataclass\nclass H:\n",
            _ => "@dataclass(frozen=True)\nclass H:\n",
        };
        var controls = HasCtorAndPrivateControl(host);

        return "delegate Handler() -> None\n\n"
            + header
            + $"{dec}    {n}f1: int\n"
            + $"{dec}    {n}f2: int = 1\n"
            + "    x: int = 3\n"
            + "    const K: int = 9\n"
            + (controls ? "    __z: int = 7\n\n    def __init__(self, v: int) -> None:\n" + $"        self.{n}f1 = v\n" : "")
            + "\n"
            + $"{dec}    def {n}m(self) -> int:\n        return self.{n}f1 + 1\n\n"
            + $"{dec}    property get {n}p(self) -> int:\n        return self.{n}f1 + 2\n\n"
            + $"{dec}    class {n}Inner:\n        def f(self) -> int:\n            return 9\n\n"
            + $"{dec}    event {n}e: Handler\n\n"
            + "    def show(self) -> None:\n"
            + $"        print(self.{n}f1)\n"
            + $"        print(self.{n}f2)\n"
            + "        print(self.x)\n"
            + "        print(H.K)\n"
            + (controls ? "        print(self.__z)\n" : "")
            + $"        print(self.{n}m())\n"
            + $"        print(self.{n}p)\n"
            + $"        print(H.{n}Inner().f())\n\n"
            + "def main() -> None:\n    H(4).show()\n";
    }

    private static string ExpectedOutput(Host host)
        => HasCtorAndPrivateControl(host) ? "4\n1\n3\n9\n7\n5\n6\n9\n" : "4\n1\n3\n9\n5\n6\n9\n";

    private static bool IsDataclass(Host host) => host is Host.Dataclass or Host.FrozenDataclass;

    private static SyntaxKind ExpectedSpelledAccess(Host host, Spelling spelling, string member) => spelling switch
    {
        // A dataclass FIELD's emitted access floor is protected: every dataclass subclass
        // re-synthesizes its members over the inherited roster (see DataclassSubclass_... below).
        Spelling.Private when IsDataclass(host) && member is "f1" or "f2" => SyntaxKind.ProtectedKeyword,
        Spelling.Private => SyntaxKind.PrivateKeyword,
        Spelling.Public => SyntaxKind.PublicKeyword,
        // The host axis: protected — by convention or by decorator — has no meaning in a sealed
        // struct. (A struct × @protected program is refused before emission; see the refusal cell.)
        _ => host == Host.Struct ? SyntaxKind.PrivateKeyword : SyntaxKind.ProtectedKeyword,
    };

    public static IEnumerable<object[]> ExecutingCells()
        => from host in Enum.GetValues<Host>()
           from spelling in Enum.GetValues<Spelling>()
           where !(host == Host.Struct && spelling == Spelling.Protected)
           select new object[] { host, spelling };

    [Theory]
    [MemberData(nameof(ExecutingCells))]
    public void Cell_RunsAndEmitsTheClassifiedAccess(Host host, Spelling spelling)
    {
        var source = Program(host, spelling);
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InvariantViolation,
            $"[{host}×{spelling}] access must be emit-legal in the host (no CS0666 behind SPY0908) and read "
            + $"from a symbol (no SPY0904). Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{host}×{spelling}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(ExpectedOutput(host), $"[{host}×{spelling}]\n{source}");

        var type = HostDeclaration(result.GeneratedCSharp!);
        foreach (var member in SpelledMembers)
        {
            AccessOf(type, member).Should().Be(ExpectedSpelledAccess(host, spelling, member),
                $"[{host}×{spelling}] member '{member}' must carry the classified access\n{type}");
        }

        AccessOf(type, "x").Should().Be(SyntaxKind.PublicKeyword, $"[{host}×{spelling}] control x");
        AccessOf(type, "K").Should().Be(SyntaxKind.PublicKeyword, $"[{host}×{spelling}] control K");
        if (HasCtorAndPrivateControl(host))
            AccessOf(type, "z").Should().Be(SyntaxKind.PrivateKeyword, $"[{host}×{spelling}] control __z");
    }

    [Fact]
    public void StructProtectedDecorator_IsRefusedOnEveryMemberKind()
    {
        // Prior commit: every decorated member emitted `protected` → CS0666 behind SPY0908.
        var source = Program(Host.Struct, Spelling.Protected);
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"@protected in a struct must be refused\n{source}");
        var refusals = result.RawDiagnostics
            .Where(d => d.Code == DiagnosticCodes.Validation.VirtualOnStructMethod)
            .ToList();
        refusals.Should().HaveCount(SpelledMembers.Length,
            "one SPY0415 per @protected member (field ×2, method, property, nested class, event). "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        refusals.Should().OnlyContain(d => d.Message.Contains("cannot be @protected")
            && d.Message.Contains("Structs are sealed; use @private or the _name convention."));
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "the refusal is semantic, not the C# compiler's CS0666");
    }

    [Fact]
    public void UnionCaseField_Underscore_RunsPublic()
    {
        // The union-case host: case fields carry no decorators and emit public (the case record's
        // positional shape); `_x` there never reached CS0666 and does not change.
        var source =
            "union U:\n    case Q(_x: int)\n    case R()\n\n"
            + "def main() -> None:\n    u: U = U.Q(4)\n    match u:\n        case Q(v):\n            print(v)\n"
            + "        case R():\n            print(0)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("4\n");
    }

    [Fact]
    public void DataclassSubclass_OverAPrivateBaseField_Runs()
    {
        // The subclass's synthesized constructor/__eq__/__repr__ read the base's `__x` directly, so a
        // dataclass field is never emitted narrower than protected. Prior commit: RUNS (the property
        // was hard-coded public); reading the private level alone would have made it CS0122.
        // python3: B(1, 2, 3).z == 3 and B(1, 2, 3) == B(1, 2, 3) is True.
        var source =
            "@dataclass\nclass A:\n    __x: int\n    _y: int\n\n"
            + "@dataclass\nclass B(A):\n    z: int\n\n"
            + "def main() -> None:\n    b = B(1, 2, 3)\n    print(b.z)\n    print(b == B(1, 2, 3))\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("3\nTrue\n");
        var a = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>().Single(t => t.Identifier.Text == "A");
        AccessOf(a, "x").Should().Be(SyntaxKind.ProtectedKeyword);
        AccessOf(a, "y").Should().Be(SyntaxKind.ProtectedKeyword);
    }

    [Fact]
    public void ModuleLevelUnderscoreProperty_IsInternal_AndRuns()
    {
        // The module host (#2020): a module-level property has no type symbol, so it takes the
        // host-less module-level rule — `_p` is internal (the module class is static, and a static
        // class cannot hold a protected member). Prior commit: CS1057 behind SPY0908.
        var source = "property get _p() -> int:\n    return 5\n\ndef main() -> None:\n    print(_p)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue($"{string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("5\n");
        var moduleClass = CSharpSyntaxTree.ParseText(result.GeneratedCSharp!).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(t => t.Members.OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.Text.Trim('_') == "P"));
        AccessOf(moduleClass, "p").Should().Be(SyntaxKind.InternalKeyword);
    }

    public static IEnumerable<object[]> OutsideAccessCells() => new[]
    {
        new object[] { "class H:\n    _x: int = 1\n" },
        new object[] { "struct H:\n    _x: int = 1\n" },
        new object[] { "@dataclass\nclass H:\n    _x: int = 1\n" },
    };

    [Theory]
    [MemberData(nameof(OutsideAccessCells))]
    public void OutsideAccess_ToUnderscoreMember_IsRefusedAsBefore(string host)
    {
        // Direction control: the AccessValidator reads the WRITTEN level (convention or decorator),
        // not the host-mapped one, so `h._x` from outside the type is refused with the same code and
        // message before and after #1937 (measured on the prior commit's binary).
        var source = host + "\ndef main() -> None:\n    h = H()\n    print(h._x)\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(source);
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.AccessViolation
            && d.Message == "Cannot access protected member '_x' of 'H' from outside the class hierarchy",
            string.Join(" | ", result.CompilationErrors));
    }

    /// <summary>
    /// Completeness scan (#1937): no emitter site re-derives a TYPE MEMBER's access from its name.
    /// The deleted name-keyed class-member helper stays deleted, and the underscore convention has
    /// exactly one CodeGen caller — <c>GetModuleLevelAccessModifier</c>, the host-less module-level
    /// rule (a module member has no host type and no member symbol to read). That one call is the
    /// positive control: the scan demonstrably finds <c>AccessLevelConventions.FromName</c> where it
    /// is, so its absence everywhere else is a measurement, not a vacuous pass.
    /// </summary>
    [Fact]
    public void CodeGen_ReadsMemberAccessFromSymbols_NotFromTheName()
    {
        var dir = EmitterBannedTokenScanTests.FindCodeGenSourceDirectory();
        var sources = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Select(f => (File: Path.GetFileName(f), Text: File.ReadAllText(f)))
            .ToList();

        const string DeletedHelper = "GetAccessModifier" + "FromNameConvention";
        sources.Where(s => s.Text.Contains(DeletedHelper)).Select(s => s.File)
            .Should().BeEmpty("the name-keyed member-access helper had no host axis (CS0666 in a struct)");

        const string Convention = "AccessLevelConventions.FromName(";
        var callers = sources
            .SelectMany(s => AllIndexesOf(s.Text, Convention).Select(i => (s.File, s.Text, Index: i)))
            .ToList();
        callers.Should().ContainSingle(
            "the module-level rule is the one CodeGen reader of the underscore convention; found: "
            + string.Join(", ", callers.Select(c => c.File)));

        var (file, text, index) = callers[0];
        file.Should().Be("RoslynEmitter.cs");
        var enclosing = text.LastIndexOf("private static SyntaxKind ", index, StringComparison.Ordinal);
        text.Substring(enclosing, index - enclosing).Should().Contain("GetModuleLevelAccessModifier(",
            "the one remaining FromName call is inside the host-less module-level helper");
    }

    private static IEnumerable<int> AllIndexesOf(string text, string token)
    {
        for (var i = text.IndexOf(token, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(token, i + token.Length, StringComparison.Ordinal))
            yield return i;
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        // 4 hosts × 4 spellings = 16 programs; struct × @protected is the refusal cell, 15 execute.
        ExecutingCells().Should().HaveCount(15);
        SpelledMembers.Should().HaveCount(6);
        OutsideAccessCells().Should().HaveCount(3);
    }

    // ── C# tree helpers ──────────────────────────────────────────────────────────────────────

    private static TypeDeclarationSyntax HostDeclaration(string csharp)
        => CSharpSyntaxTree.ParseText(csharp).GetRoot()
            .DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Single(t => t.Identifier.Text == "H");

    /// <summary>
    /// The access keyword of the ONE direct member of <paramref name="type"/> whose C# identifier,
    /// stripped of underscores and compared case-insensitively, is <paramref name="baseName"/>
    /// (<c>_f1</c> → <c>_F1</c>, <c>f1</c> → <c>F1</c>, <c>__z</c> → <c>__Z</c>/<c>_Z</c>).
    /// </summary>
    private static SyntaxKind AccessOf(TypeDeclarationSyntax type, string baseName)
    {
        var matches = type.Members
            .Select(m => (Member: m, Names: DeclaredNames(m)))
            .Where(p => p.Names.Any(n => string.Equals(n.Trim('_'), baseName, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Member)
            .ToList();
        matches.Should().ContainSingle($"exactly one member named like '{baseName}' in\n{type}");

        var access = matches[0].Modifiers
            .Select(m => m.Kind())
            .Where(k => k is SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword
                or SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword)
            .ToList();
        access.Should().ContainSingle($"one access keyword on '{baseName}': {matches[0]}");
        return access[0];
    }

    private static IEnumerable<string> DeclaredNames(MemberDeclarationSyntax member) => member switch
    {
        BaseFieldDeclarationSyntax f => f.Declaration.Variables.Select(v => v.Identifier.Text),
        PropertyDeclarationSyntax p => new[] { p.Identifier.Text },
        MethodDeclarationSyntax m => new[] { m.Identifier.Text },
        BaseTypeDeclarationSyntax t => new[] { t.Identifier.Text },
        EventDeclarationSyntax e => new[] { e.Identifier.Text },
        _ => Array.Empty<string>(),
    };
}
