using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The parameter-default constant matrix — kind × host (#1762, #1769, R-R) — and its sibling, the
/// module-const matrix — declared type × reference kind × consumer (#1762 follow-up).
///
/// <para><b>Contract (defaults).</b> A default value for a parameter is admitted or refused by
/// <c>ConstantDefaultClassifier</c> via <c>DefaultParameterValidator</c>. The classifier
/// maps the default's AST shape to an <c>EmittableConstantKind</c>, and the validator checks
/// that kind against the <c>AdmissionTable</c> for the host position. Admitted defaults compile
/// and run with the printed value. Refused defaults report SPY0401. No cell produces SPY0908.
/// A <c>@dataclass</c> field default is a host too: it becomes the synthesized constructor's
/// parameter default, so it is admitted by the same table (#1769 — unvisited, the refused kinds
/// ICEd CS1736 there while their def twins were SPY0401).</para>
///
/// <para><b>Axes.</b> Kind: {Literal, NegatedLiteral, ConstReference, EnumMember, NoneLiteral,
/// NoneCall, SomeIntoOptional, TupleLiteral, ConditionalOfConstants, Folded, ResultOk, ResultErr,
/// NestedTuple, ListLiteral, DictLiteral, Call, Constructor, Lambda} × Host: {Def, Lambda, Init, Method,
/// Dataclass}. Totality: 18 × 5 = 90 cells (40 admitted, 50 refused — SPY0401, or SPY0400 for the
/// mutable list/dict literals).</para>
///
/// <para><b>Contract (module consts).</b> A module <c>const</c> whose declared type C# admits for
/// <c>const</c> — every <c>PrimitiveCatalog</c> primitive but <c>object</c>/<c>void</c> — and whose
/// initializer the classifier admits for <c>AdmissionTable.ModuleConst</c> emits as
/// <c>public const</c> (<c>CodeGenInfo.IsCompileTimeConstant</c>), so every constant-position
/// consumer reads it: a def/lambda/method parameter default, a <c>case</c> pattern, another const,
/// a plain read. Between dfcdd47fa and the fix the fact was integer-only, so float/float32/decimal/
/// str/bool consts fell to <c>static readonly</c>: the parameter defaults ICEd CS1736, the case
/// pattern CS9135, and a forward reference read the zero-initialized field (0.0 / None / False).</para>
///
/// <para><b>Axes.</b> Type: {int, int8, uint64, float, float32, decimal, str, bool} × Reference:
/// {Backward, ForwardThroughConst, Folded} × Consumer: {Print, DefDefault, LambdaDefault,
/// MethodDefault, MatchCase}. Totality: 8 × 3 × 5 = 120 cells, of which the Folded × {float32,
/// decimal} × 5 = 10 are declared N/A (the checker refuses every binary literal expression into a
/// float32/decimal slot — SPY0220 "float64 → decimal" — at BASE and HEAD alike; a sibling of the
/// literal-derived-fact class, #1731/#1741, not of this seam).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ParameterDefaultConstantMatrixTests : IntegrationTestBase
{
    public ParameterDefaultConstantMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    private const int KindCount = 18;
    private const int HostCount = 5;
    private const int AdmittedCellCount = 40;
    private const int RefusedCellCount = 49;
    private const int NotApplicableCellCount = 1;

    // ── Axis 1: default-value kinds ──────────────────────────────────────────────────────────

    private sealed record Kind(
        string Name,
        string ParamType,
        string DefaultExpr,
        string Prelude,
        string? AcceptedOutput,
        string? RefusedFragment,
        string RefusedCode = DiagnosticCodes.Validation.NonConstDefault);

    private static readonly Kind[] Kinds =
    {
        new("Literal", "int", "42", "", "42\n", null),
        new("NegatedLiteral", "int", "-1", "", "-1\n", null),
        new("ConstReference", "int", "A", "const A: int = 100\n\n", "100\n", null),
        new("EnumMember", "Color", "Color.RED",
            "enum Color:\n    RED = 1\n    GREEN = 2\n\n", "RED\n", null),
        new("NoneLiteral", "int | None", "None", "", "None\n", null),
        new("NoneCall", "int?", "None()", "", "None\n", null),
        new("SomeIntoOptional", "int?", "Some(42)", "", null,
            "must be a compile-time constant expression"),
        new("TupleLiteral", "tuple[int, int]", "(1, 2)", "", null,
            "Tuple literals are not emittable as parameter defaults"),
        new("ConditionalOfConstants", "int", "1 if True else 2", "", "1\n", null),
        // Plan-757fbb Phase 4 acceptance names these too (added in the verification round):
        new("Folded", "int", "1 + 2", "", "3\n", null),
        new("ResultOk", "int!str", "Ok(1)", "", null,
            "must be a compile-time constant expression"),
        new("ResultErr", "int!str", "Err(\"e\")", "", null,
            "must be a compile-time constant expression"),
        new("NestedTuple", "tuple[tuple[int, int], int]", "((1, 2), 3)", "", null,
            "Tuple literals are not emittable"),
        new("ListLiteral", "list[int]", "[1]", "", null,
            "Mutable default value", DiagnosticCodes.Validation.MutableDefault),
        new("DictLiteral", "dict[str, int]", "{\"a\": 1}", "", null,
            "Mutable default value", DiagnosticCodes.Validation.MutableDefault),
        new("Call", "int", "g()", "def g() -> int:\n    return 1\n\n", null,
            "must be a compile-time constant expression"),
        new("Constructor", "Box", "Box(1)",
            "class Box:\n    v: int\n\n    def __init__(self, v: int) -> None:\n        self.v = v\n\n", null,
            "must be a compile-time constant expression"),
        new("Lambda", "() -> int", "lambda: 1", "", null,
            "must be a compile-time constant expression"),
    };

    // ── Axis 2: host positions ───────────────────────────────────────────────────────────────

    private sealed record Host(
        string Name,
        Func<Kind, string> Compose);

    private static readonly Host[] Hosts =
    {
        new("Def", k =>
            $"{k.Prelude}def f(x: {k.ParamType} = {k.DefaultExpr}) -> None:\n    print(x)\n\ndef main():\n    f()\n"),

        new("Lambda", k =>
            $"{k.Prelude}def main():\n    f = lambda x: {k.ParamType} = {k.DefaultExpr}: x\n    print(f())\n"),

        new("Init", k =>
            $"{k.Prelude}class C:\n    x: {k.ParamType}\n\n    def __init__(self, x: {k.ParamType} = {k.DefaultExpr}):\n        self.x = x\n\ndef main():\n    print(C().x)\n"),

        new("Method", k =>
            $"{k.Prelude}class C:\n    def m(self, x: {k.ParamType} = {k.DefaultExpr}) -> None:\n        print(x)\n\ndef main():\n    C().m()\n"),

        // The field default becomes the synthesized constructor's parameter default (#1769).
        new("Dataclass", k =>
            $"{k.Prelude}@dataclass\nclass D:\n    x: {k.ParamType} = {k.DefaultExpr}\n\ndef main():\n    print(D().x)\n"),
    };

    // ── Cell resolution ──────────────────────────────────────────────────────────────────────

    private static string Key(Host h, Kind k) => $"{h.Name}×{k.Name}";

    private static Host H(string name) => Hosts.Single(h => h.Name == name);

    private static Kind K(string name) => Kinds.Single(k => k.Name == name);

    private enum Verdict { Admitted, Refused }

    private static Verdict Classify(Kind k) =>
        k.AcceptedOutput != null ? Verdict.Admitted : Verdict.Refused;

    /// <summary>
    /// Cells that are not this seam's to decide, each with the reason (no entry without one). A
    /// lambda-typed default inside a lambda's own parameter list —
    /// <c>lambda x: () -> int = lambda: 1: x</c> — does not parse (SPY0103 at the nested lambda's
    /// colon); the grammar, not the constant-default table, refuses it.
    /// </summary>
    private static readonly Dictionary<string, string> NotApplicableCells = new(StringComparer.Ordinal)
    {
        ["Lambda×Lambda"] = "a lambda default inside a lambda parameter list is a parse error (SPY0103) — parser grammar, not the admission table",
    };

    private static IEnumerable<object[]> CellsWhere(Verdict verdict)
        => from h in Hosts
           from k in Kinds
           where Classify(k) == verdict && !NotApplicableCells.ContainsKey(Key(h, k))
           select new object[] { h.Name, k.Name };

    public static IEnumerable<object[]> AdmittedCells => CellsWhere(Verdict.Admitted);

    public static IEnumerable<object[]> RefusedCells => CellsWhere(Verdict.Refused);

    // ── The cells ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(AdmittedCells))]
    public void AdmittedCell_RunsAndPrintsTheDefaultValue(string host, string kind)
    {
        var h = H(host);
        var k = K(kind);
        var source = h.Compose(k);

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{host} × {kind}] must compile — the classifier admits this kind at this host. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(k.AcceptedOutput,
            $"[{host} × {kind}] prints the default value\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {kind}] must never produce SPY0908\n{source}");
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void RefusedCell_ReportsSPY0401(string host, string kind)
    {
        var h = H(host);
        var k = K(kind);
        var source = h.Compose(k);

        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            $"[{host} × {kind}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == k.RefusedCode,
            $"[{host} × {kind}] must report {k.RefusedCode}. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.Where(d => d.Code == k.RefusedCode)
            .First().Message.Should().Contain(k.RefusedFragment!,
                $"[{host} × {kind}] must carry the expected diagnostic text\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {kind}] must never produce SPY0908\n{source}");
    }

    /// <summary>
    /// The SPY0401 steer quotes the parameter's type as the user SPELLED it. Before the fix it
    /// interpolated the <c>TypeAnnotation</c> record's <c>ToString()</c>, so users saw
    /// <c>TypeAnnotation { LineStart = 1, ColumnStart = 10, … }</c> in place of <c>int?</c>.
    /// </summary>
    [Theory]
    [InlineData("Def", "def f(x: int? = None()) -> ...: x ??= Some(...)")]
    [InlineData("Dataclass", "x: int? = None()")]
    public void RefusedSomeCell_SteerSpellsTheAnnotation(string host, string spelledSteer)
    {
        var source = H(host).Compose(K("SomeIntoOptional"));

        var result = CompileAndExecute(source);

        var message = result.RawDiagnostics
            .Single(d => d.Code == DiagnosticCodes.Validation.NonConstDefault).Message;
        message.Should().Contain(spelledSteer, $"the steer quotes the annotation's source spelling\n{source}");
        message.Should().NotContain("TypeAnnotation {", "a record dump is not a type spelling");
    }

    // ── Totality ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Kinds.Length.Should().Be(KindCount);
        Hosts.Length.Should().Be(HostCount);
        Kinds.Select(k => k.Name).Should().OnlyHaveUniqueItems();
        Hosts.Select(h => h.Name).Should().OnlyHaveUniqueItems();

        var product = (from h in Hosts from k in Kinds select Key(h, k)).ToHashSet();
        product.Count.Should().Be(KindCount * HostCount);

        var admitted = AdmittedCells.Count();
        var refused = RefusedCells.Count();

        admitted.Should().Be(AdmittedCellCount, "the admitted half is written down");
        refused.Should().Be(RefusedCellCount, "the refused half is written down");
        NotApplicableCells.Should().HaveCount(NotApplicableCellCount, "every N/A cell is written down with its reason");
        NotApplicableCells.Keys.Should().OnlyContain(key => product.Contains(key), "an N/A key must name a real cell (stale entries fail)");
        (admitted + refused + NotApplicableCells.Count).Should().Be(KindCount * HostCount,
            $"admitted ({admitted}) + refused ({refused}) + N/A ({NotApplicableCells.Count}) must be the whole product "
            + $"({KindCount} × {HostCount})");
    }

    // ══ Module-const matrix: declared type × reference kind × consumer ═══════════════════════

    private const int ConstTypeCount = 8;
    private const int ReferenceKindCount = 3;
    private const int ConsumerCount = 5;
    private const int ModuleConstNotApplicableCellCount = 10; // Folded × {float32, decimal} × 5 consumers

    /// <summary>
    /// A C#-const-eligible declared type: its Sharpy spelling, the C# keyword the emitted
    /// declaration must carry, a literal, the folded form (null when the checker refuses every
    /// binary literal expression into the slot — see the class remarks), and what <c>print</c>
    /// shows for the value.
    /// </summary>
    private sealed record ConstType(string Name, string CSharpType, string Literal, string? Folded, string Printed);

    private static readonly ConstType[] ConstTypes =
    {
        new("int", "int", "4", "2 + 2", "4"),
        new("int8", "sbyte", "100", "50 + 50", "100"),
        new("uint64", "ulong", "4", "2 + 2", "4"),
        new("float", "double", "4.0", "2.0 + 2.0", "4.0"),
        new("float32", "float", "4.0", null, "4.0"),
        new("decimal", "decimal", "4.0", null, "4.0"),
        new("str", "string", "\"ab\"", "\"a\" + \"b\"", "ab"),
        new("bool", "bool", "True", "True and True", "True"),
    };

    /// <summary>
    /// How <c>A</c> is declared. ForwardThroughConst declares <c>A</c> BEFORE the const it reads —
    /// the cell that printed B's zero-initialized field (0.0 / None / False) at HEAD-before-fix.
    /// Its operands are distinct from the literal only through B, so a wrong reading cannot
    /// coincide with the right one.
    /// </summary>
    private sealed record ReferenceKind(string Name, Func<ConstType, string?> Decls, bool DeclaresB);

    private static readonly ReferenceKind[] ReferenceKinds =
    {
        new("Backward", t => $"const A: {t.Name} = {t.Literal}\n", DeclaresB: false),
        new("ForwardThroughConst", t => $"const A: {t.Name} = B\nconst B: {t.Name} = {t.Literal}\n", DeclaresB: true),
        new("Folded", t => t.Folded == null ? null : $"const A: {t.Name} = {t.Folded}\n", DeclaresB: false),
    };

    /// <summary>A constant-position consumer of <c>A</c> and the stdout it must produce.</summary>
    private sealed record Consumer(string Name, Func<ConstType, string> Program, Func<ConstType, string> Expected);

    private static readonly Consumer[] Consumers =
    {
        new("Print",
            _ => "def main():\n    print(A)\n",
            t => t.Printed + "\n"),
        new("DefDefault",
            t => $"def f(x: {t.Name} = A) -> None:\n    print(x)\n\ndef main():\n    f()\n",
            t => t.Printed + "\n"),
        new("LambdaDefault",
            t => $"def main():\n    f = lambda x: {t.Name} = A: x\n    print(f())\n",
            t => t.Printed + "\n"),
        new("MethodDefault",
            t => $"class C:\n    def m(self, x: {t.Name} = A) -> None:\n        print(x)\n\ndef main():\n    C().m()\n",
            t => t.Printed + "\n"),
        new("MatchCase",
            t => $"def main():\n    v: {t.Name} = {t.Literal}\n    match v:\n        case A:\n            print(\"hit\")\n        case _:\n            print(\"miss\")\n",
            _ => "hit\n"),
    };

    private static ConstType T(string name) => ConstTypes.Single(t => t.Name == name);

    private static ReferenceKind R(string name) => ReferenceKinds.Single(r => r.Name == name);

    private static Consumer C(string name) => Consumers.Single(c => c.Name == name);

    private static bool IsApplicable(ConstType t, ReferenceKind r) => r.Decls(t) != null;

    public static IEnumerable<object[]> ModuleConstCells =>
        from t in ConstTypes
        from r in ReferenceKinds
        from c in Consumers
        where IsApplicable(t, r)
        select new object[] { t.Name, r.Name, c.Name };

    public static IEnumerable<object[]> ModuleConstDeclarationCells =>
        from t in ConstTypes
        from r in ReferenceKinds
        where IsApplicable(t, r)
        select new object[] { t.Name, r.Name };

    [Theory]
    [MemberData(nameof(ModuleConstCells))]
    public void ModuleConstCell_ConsumerReadsTheConstsValue(string type, string reference, string consumer)
    {
        var t = T(type);
        var r = R(reference);
        var c = C(consumer);
        var source = r.Decls(t) + "\n" + c.Program(t);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{type} × {reference} × {consumer}] must never produce SPY0908 — a const in a constant "
            + $"position must emit as a C# const. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{type} × {reference} × {consumer}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(c.Expected(t),
            $"[{type} × {reference} × {consumer}] reads the const's declared value\n{source}");
    }

    /// <summary>
    /// The materialized fact itself: <c>CodeGenInfo.IsCompileTimeConstant</c> drives the emitted
    /// modifier, so every applicable declaration is <c>public const &lt;type&gt;</c>. This is what
    /// makes the Backward × Print cells discriminating — a <c>static readonly</c> prints the same
    /// value there and only its consumers reveal the difference.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModuleConstDeclarationCells))]
    public void ModuleConstDeclaration_EmitsAsCSharpConst(string type, string reference)
    {
        var t = T(type);
        var r = R(reference);
        var source = r.Decls(t) + "\ndef main():\n    print(A)\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"[{type} × {reference}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp.Should().MatchRegex(
            $@"\bpublic const {Regex.Escape(t.CSharpType)} A = ",
            $"[{type} × {reference}] A is a compile-time constant\n{result.GeneratedCSharp}");
        if (r.DeclaresB)
        {
            result.GeneratedCSharp.Should().MatchRegex(
                $@"\bpublic const {Regex.Escape(t.CSharpType)} B = ",
                $"[{type} × {reference}] the referenced const is compile-time too\n{result.GeneratedCSharp}");
        }
    }

    /// <summary>
    /// The N/A cells are refused by the checker, not by this seam: a binary literal expression into
    /// a float32/decimal slot is SPY0220 at BASE and HEAD alike. The control keeps the N/A
    /// declaration honest — when the checker learns to narrow it, the cell rejoins the matrix.
    /// </summary>
    [Theory]
    [InlineData("float32")]
    [InlineData("decimal")]
    public void ModuleConstFoldedCell_NotApplicable_IsTheCheckersRefusal(string type)
    {
        var t = T(type);
        t.Folded.Should().BeNull("the cell is declared N/A");

        var result = CompileAndExecute($"const A: {t.Name} = 2.0 + 2.0\n\ndef main():\n    print(A)\n");

        result.Success.Should().BeFalse($"the checker refuses the fold into {type} today");
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch,
            "the refusal is SPY0220, upstream of this seam — if this cell starts compiling, give the type a "
            + "Folded form and drop it from ModuleConstNotApplicableCellCount");
    }

    [Fact]
    public void ModuleConstMatrix_IsTotalOverItsAxes()
    {
        ConstTypes.Length.Should().Be(ConstTypeCount);
        ReferenceKinds.Length.Should().Be(ReferenceKindCount);
        Consumers.Length.Should().Be(ConsumerCount);
        ConstTypes.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        ReferenceKinds.Select(r => r.Name).Should().OnlyHaveUniqueItems();
        Consumers.Select(c => c.Name).Should().OnlyHaveUniqueItems();

        var product = ConstTypeCount * ReferenceKindCount * ConsumerCount;
        var applicable = ModuleConstCells.Count();
        var notApplicable = product - applicable;

        notApplicable.Should().Be(ModuleConstNotApplicableCellCount,
            "every N/A cell is declared with its reason (Folded × {float32, decimal} × every consumer)");
        (applicable + notApplicable).Should().Be(product,
            $"applicable ({applicable}) + N/A ({notApplicable}) must be the whole product "
            + $"({ConstTypeCount} × {ReferenceKindCount} × {ConsumerCount})");
        ModuleConstDeclarationCells.Count().Should().Be(
            ConstTypeCount * ReferenceKindCount - ModuleConstNotApplicableCellCount / ConsumerCount);
    }

    // ── Classifier scan (guarded-by anchor for DispatchSiteInventoryTests) ───────────────────
    // DispatchSiteInventoryTests requires the guarded-by class to SCAN the site: this test reads
    // ConstantDefaultClassifier.cs, collects every EmittableConstantKind the "Classify" switch
    // returns, and asserts the set equals the enum's members — a kind added to the enum without a
    // classifying arm (or an arm deleted) goes red. The enum size is anchored to a literal so the
    // comparison is not "the enum against itself".

    private const int EmittableConstantKindCount = 16;

    [Fact]
    public void ClassifierSwitch_ReturnsEveryEmittableConstantKind()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "Sharpy.Compiler",
            "Semantic", "Validation", "ConstantDefaultClassifier.cs");
        var source = File.ReadAllText(path);

        var enumBody = Regex.Match(source, @"enum EmittableConstantKind\s*\{(?<body>[^}]*)\}",
            RegexOptions.Singleline).Groups["body"].Value;
        var declared = Regex.Matches(enumBody, @"^\s*(?<name>[A-Z]\w*)\s*,?\s*$", RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);
        declared.Should().HaveCount(EmittableConstantKindCount,
            "the kind axis is anchored to a literal, not to the enum this test scans");

        var start = source.IndexOf("public static EmittableConstantKind Classify(", StringComparison.Ordinal);
        var end = source.IndexOf("public static bool IsAdmitted(", StringComparison.Ordinal);
        start.Should().BeGreaterThan(-1); end.Should().BeGreaterThan(start);
        var classifyBody = source.Substring(start, end - start);
        var returned = Regex.Matches(classifyBody, @"EmittableConstantKind\.(?<name>[A-Z]\w*)")
            .Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

        returned.Should().BeEquivalentTo(declared,
            "every kind the enum declares is produced by Classify, and Classify names no kind the enum lacks");
    }

}
