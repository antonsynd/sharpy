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
/// <c>ConstantDefaultClassifier</c> via <c>ConstantPositionValidator</c>. The classifier
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
/// Dataclass}. Totality: 28 × 5 = 140 cells (52 admitted, 84 refused — SPY0401, or SPY0400 for the
/// mutable list/dict literals — and 4 N/A). There is no known-red bucket.</para>
///
/// <para><b>Contract (module consts).</b> A module <c>const</c> whose declared type C# admits for
/// <c>const</c> — every <c>PrimitiveCatalog</c> primitive but <c>object</c>/<c>void</c> — and whose
/// initializer the classifier admits for <c>AdmissionTable.ConstInitializer</c> emits as
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
    private const int KindCount = 28;
    private const int HostCount = 5;
    private const int AdmittedCellCount = 52;
    private const int RefusedCellCount = 84;
    private const int NotApplicableCellCount = 4;

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
        // ── Phase 2 additions: operator/const-ref/T? kinds (#1788) ──────
        new("FloorDiv", "int", "7 // 2", "", null,
            "'//' lowers to FloorDiv which is not a C# constant operator"),
        new("FloorMod", "int", "7 % 3", "", null,
            "'%' lowers to FloorMod which is not a C# constant operator"),
        new("FloatFloorDiv", "float", "7.0 // 2.0", "", null,
            "'//' lowers to FloorDiv which is not a C# constant operator"),
        new("FloatPow", "float", "2.0 ** 3.0", "", null,
            "'**' lowers to Math.Pow which is not a C# constant operator"),
        new("StrRepeat", "str", "\"ab\" * 2", "", null,
            "'*' on str lowers to string.Repeat which is not a C# constant operator"),
        new("ConstRefCall", "float", "FM",
            "const FM: float = max(4.0, 1.0)\n\n", null,
            "is not a compile-time constant"),
        new("OptionalConstRef", "int?", "O",
            "const O: int? = Some(1)\n\n", null,
            "is not a compile-time constant"),
        // EnumConstRef: C# admits `const Color C = Color.RED`, so an enum const is a compile-time
        // constant and reads in every constant position (#1782).
        new("EnumConstRef", "Color", "E",
            "enum Color:\n    RED = 1\n    GREEN = 2\n\nconst E: Color = Color.RED\n\n", "RED\n", null),
        // LocalConstRef and ClassConstRef have host-specific preludes; their Kind.Prelude is empty
        // because the host composer provides the scope. Only the listed hosts apply — all others are
        // N/A because the scope that owns the const is absent in those hosts.
        new("LocalConstRef", "int", "K", "", "1\n", null),
        // ClassConstRef: a class field const IS a compile-time constant — the ONE analysis walks
        // type bodies and folds their integers, so `C.K` reads in every default position (#1791).
        new("ClassConstRef", "int", "C.K",
            "class C:\n    const K: int = 1\n\n", "1\n", null),
    };

    // ── Axis 2: host positions ───────────────────────────────────────────────────────────────

    private sealed record Host(
        string Name,
        Func<Kind, string> Compose);

    private static readonly Host[] Hosts =
    {
        new("Def", k => k.Name switch
        {
            "LocalConstRef" =>
                "def outer():\n    const K: int = 1\n    def inner(x: int = K) -> None:\n        print(x)\n    inner()\n\ndef main():\n    outer()\n",
            _ => $"{k.Prelude}def f(x: {k.ParamType} = {k.DefaultExpr}) -> None:\n    print(x)\n\ndef main():\n    f()\n",
        }),

        new("Lambda", k => k.Name switch
        {
            "LocalConstRef" =>
                "def outer():\n    const K: int = 1\n    f = lambda x: int = K: x\n    print(f())\n\ndef main():\n    outer()\n",
            _ => $"{k.Prelude}def main():\n    f = lambda x: {k.ParamType} = {k.DefaultExpr}: x\n    print(f())\n",
        }),

        new("Init", k =>
            $"{k.Prelude}class D:\n    x: {k.ParamType}\n\n    def __init__(self, x: {k.ParamType} = {k.DefaultExpr}):\n        self.x = x\n\ndef main():\n    print(D().x)\n"),

        new("Method", k =>
            $"{k.Prelude}class D:\n    def m(self, x: {k.ParamType} = {k.DefaultExpr}) -> None:\n        print(x)\n\ndef main():\n    D().m()\n"),

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
        // LocalConstRef: the const K lives in an enclosing function scope — only Def and Lambda
        // place the default inside that scope; Init/Method/Dataclass have no enclosing function.
        ["Init×LocalConstRef"] = "a local const lives in function scope — Init is a class member with no enclosing function to own the const",
        ["Method×LocalConstRef"] = "a local const lives in function scope — Method is a class member with no enclosing function to own the const",
        ["Dataclass×LocalConstRef"] = "a local const lives in function scope — Dataclass is a class member with no enclosing function to own the const",
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
            + $"({KindCount} × {HostCount}) — there is no known-red bucket");
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

    // ══ The const-HOST matrix: one fact, every host, every consumer ═══════════════════════════

    /// <summary>
    /// A <c>const</c> DECLARATION host. <see cref="Declare"/> wraps the declaration; <see cref="Read"/>
    /// spells the reference a consumer uses; <see cref="EmittedPattern"/> is the C# the declaration
    /// must produce when the fact is true, and <see cref="EmittedNonConstPattern"/> when it is false.
    /// The host is the axis the class-field regression lived on: the same initializer emitted
    /// <c>const</c> at module scope and <c>static readonly</c> in a class body (#1791).
    /// </summary>
    private sealed record ConstHost(
        string Name,
        Func<string, string> Declare,
        string Read,
        Func<string, string> EmittedPattern,
        Func<string, string> EmittedNonConstPattern,
        bool InsideFunction);

    private static readonly ConstHost[] ConstHosts =
    {
        new("Module",
            decl => decl + "\n",
            "A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("ClassField",
            decl => "class Holder:\n    " + decl + "\n\n",
            "Holder.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("NestedClassField",
            decl => "class Outer:\n    class Holder:\n        " + decl + "\n\n",
            "Outer.Holder.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("Local",
            decl => decl,
            "A",
            cs => $@"\bconst {Regex.Escape(cs)} A = ",
            cs => $@"^\s+{Regex.Escape(cs)} A = ",
            InsideFunction: true),

        new("StructField",
            decl => "struct S:\n    x: int\n    " + decl + "\n\n",
            "S.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("StructFieldOnlyConsts",
            decl => "struct S:\n    " + decl + "\n\n",
            "S.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("InterfaceField",
            decl => "interface I:\n    " + decl + "\n\n",
            "I.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("NestedStructField",
            decl => "class Outer:\n    struct S:\n        x: int\n        " + decl + "\n\n",
            "Outer.S.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),

        new("NestedInterfaceField",
            decl => "class Outer:\n    interface I:\n        " + decl + "\n\n",
            "Outer.I.A",
            cs => $@"\bpublic const {Regex.Escape(cs)} A = ",
            cs => $@"\bpublic static readonly {Regex.Escape(cs)} A = ",
            InsideFunction: false),
    };

    /// <summary>
    /// A declared type for the host matrix: the Sharpy spelling, the C# keyword the emitted
    /// declaration carries, and what <c>print</c> shows.
    /// </summary>
    private sealed record HostType(string Name, string CSharpType, string Printed, string Prelude = "");

    private static readonly HostType[] HostTypes =
    {
        new("int", "int", "4"),
        new("float", "double", "4.0"),
        new("str", "string", "ab"),
        new("bool", "bool", "True"),
        // A `char` annotation maps to C# `string` at BASE and HEAD alike (`'a'` is a string literal
        // in this grammar) — the mapping is a separate concern; what this matrix reads is whether the
        // declaration carries `const`.
        new("char", "string", "a"),
        new("Color", "Color", "RED", "enum Color:\n    RED = 1\n    GREEN = 2\n\n"),
    };

    /// <summary>
    /// An initializer form and whether the fact is true for it. <c>FoldedCallLowered</c> (<c>7 // 2</c>,
    /// <c>max(...)</c>) is the arm that must emit NON-const and still run — the cell that ICEd CS0133
    /// at a local host while the module host was fine.
    /// </summary>
    private sealed record Initializer(
        string Name, Func<HostType, string?> Expr, bool IsCompileTime, Func<HostType, string>? Extra = null);

    private static readonly Initializer[] Initializers =
    {
        new("Literal", t => t.Name switch
        {
            "int" => "4",
            "float" => "4.0",
            "str" => "\"ab\"",
            "bool" => "True",
            "char" => "'a'",
            _ => "Color.RED",
        }, IsCompileTime: true),

        new("ForwardThroughConst", t => "B", IsCompileTime: true,
            Extra: t => t.Name switch
            {
                "int" => "4",
                "float" => "4.0",
                "str" => "\"ab\"",
                "bool" => "True",
                "char" => "'a'",
                _ => "Color.RED",
            }),

        // Native folds only: str "+" and bool "and" fold; int "2 + 2" folds; a char/enum/float
        // fold has no admitted native form at this type, so the cell is N/A by construction.
        new("FoldedNative", t => t.Name switch
        {
            "int" => "2 + 2",
            "float" => "2.0 + 2.0",
            "str" => "\"a\" + \"b\"",
            "bool" => "True and True",
            _ => null,
        }, IsCompileTime: true),

        // Lowers to a Builtins call: never a C# constant, at any host.
        new("FoldedCallLowered", t => t.Name switch
        {
            "int" => "9 // 2",
            "float" => "9.0 // 2.0",
            _ => null,
        }, IsCompileTime: false),

        new("Call", t => t.Name switch
        {
            "int" => "max(1, 4)",
            "float" => "max(1.0, 4.0)",
            _ => null,
        }, IsCompileTime: false),
    };

    private static ConstHost CH(string name) => ConstHosts.Single(h => h.Name == name);

    private static HostType HT(string name) => HostTypes.Single(t => t.Name == name);

    private static Initializer INIT(string name) => Initializers.Single(i => i.Name == name);

    /// <summary>
    /// The program for one host cell: the const declaration in its host, plus the consumer.
    /// </summary>
    private static string ComposeHostCell(ConstHost host, HostType type, Initializer init, string consumer)
    {
        var expr = init.Expr(type)!;
        var decl = $"const A: {type.Name} = {expr}";
        if (init.Extra != null)
            decl = host.InsideFunction
                ? $"const B: {type.Name} = {init.Extra(type)}\n    {decl}"
                : $"{decl}\nconst B: {type.Name} = {init.Extra(type)}";

        if (host.InsideFunction)
            return type.Prelude + "def main():\n    " + decl + "\n    " + consumer + "\n";

        return type.Prelude + host.Declare(decl) + "def main():\n    " + consumer + "\n";
    }

    public static IEnumerable<object[]> ConstHostCells =>
        from h in ConstHosts
        from t in HostTypes
        from i in Initializers
        where i.Expr(t) != null
        select new object[] { h.Name, t.Name, i.Name };

    // Anchored to literals: 4 hosts × 6 types × 5 initializers = 120, of which the initializer
    // forms that have no spelling at a type (FoldedNative for char/Color; FoldedCallLowered and
    // Call for str/bool/char/Color) are N/A by construction.
    private const int ConstHostCount = 9;
    private const int HostTypeCount = 6;
    private const int InitializerCount = 5;
    private const int HostCellsNotApplicable = 9 * (2 + 4 + 4); // hosts × (FoldedNative + 2×call arms)

    [Fact]
    public void ConstHostMatrix_IsTotalOverItsAxes()
    {
        ConstHosts.Length.Should().Be(ConstHostCount);
        HostTypes.Length.Should().Be(HostTypeCount);
        Initializers.Length.Should().Be(InitializerCount);
        ConstHosts.Select(h => h.Name).Should().OnlyHaveUniqueItems();
        HostTypes.Select(t => t.Name).Should().OnlyHaveUniqueItems();
        Initializers.Select(i => i.Name).Should().OnlyHaveUniqueItems();

        var product = ConstHostCount * HostTypeCount * InitializerCount;
        var applicable = ConstHostCells.Count();
        (product - applicable).Should().Be(HostCellsNotApplicable,
            "every N/A cell is a type that has no spelling for that initializer form, counted from literals");
    }

    /// <summary>
    /// THE host-axis cell (#1791): every const host emits <c>const</c> iff the fact, and the value
    /// reads back through <c>print</c>. A call-lowered or call initializer emits NON-const and the
    /// program still runs — the arm that ICEd CS0133 at a local host and CS1736 through a class-field
    /// default. Reading the emitted C# is what makes the cell discriminating: a <c>static readonly</c>
    /// prints the same value.
    /// </summary>
    [Theory]
    [MemberData(nameof(ConstHostCells))]
    public void ConstDeclaration_EmitsConstIffTheFact(string host, string type, string initializer)
    {
        var h = CH(host);
        var t = HT(type);
        var i = INIT(initializer);
        var source = ComposeHostCell(h, t, i, $"print({h.Read})");

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {type} × {initializer}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{host} × {type} × {initializer}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(t.Printed + "\n",
            $"[{host} × {type} × {initializer}] reads the const's declared value\n{source}");

        result.GeneratedCSharp.Should().NotBeNull();
        if (i.IsCompileTime)
        {
            result.GeneratedCSharp.Should().MatchRegex(h.EmittedPattern(t.CSharpType),
                $"[{host} × {type} × {initializer}] the fact is true, so the declaration carries "
                + $"'const'\n{result.GeneratedCSharp}");
        }
        else
        {
            result.GeneratedCSharp.Should().NotMatchRegex(h.EmittedPattern(t.CSharpType),
                $"[{host} × {type} × {initializer}] the fact is false, so the declaration must NOT "
                + $"carry 'const' (C# would answer CS0133)\n{result.GeneratedCSharp}");
        }
    }

    // ══ The consumer axis for the same fact ══════════════════════════════════════════════════

    /// <summary>
    /// A constant-position consumer of the const declared by a <see cref="ConstHost"/>. The two
    /// axes are factored on purpose: <see cref="ConstDeclaration_EmitsConstIffTheFact"/> varies
    /// host × type × initializer against the emitted declaration, and this one varies host ×
    /// consumer against the two facts, so the product stays measurable while both axes stay total.
    /// </summary>
    private sealed record HostConsumer(string Name, Func<ConstHost, string, string> Compose);

    private static readonly HostConsumer[] HostConsumers =
    {
        new("Print", (h, init) => h.InsideFunction
            ? $"def main():\n    const A: int = {init}\n    print(A)\n"
            : h.Declare($"const A: int = {init}") + "def main():\n    print(" + h.Read + ")\n"),

        new("DefDefault", (h, init) => h.InsideFunction
            ? $"def main():\n    const A: int = {init}\n    def f(x: int = A) -> None:\n        print(x)\n    f()\n"
            : h.Declare($"const A: int = {init}")
              + $"def f(x: int = {h.Read}) -> None:\n    print(x)\n\ndef main():\n    f()\n"),

        new("LambdaDefault", (h, init) => h.InsideFunction
            ? $"def main():\n    const A: int = {init}\n    f = lambda x: int = A: x\n    print(f())\n"
            : h.Declare($"const A: int = {init}")
              + $"def main():\n    f = lambda x: int = {h.Read}: x\n    print(f())\n"),

        new("MethodDefault", (h, init) => h.Declare($"const A: int = {init}")
            + $"class Reader:\n    def m(self, x: int = {h.Read}) -> None:\n        print(x)\n\ndef main():\n    Reader().m()\n"),

        new("MatchCase", (h, init) => h.InsideFunction
            ? $"def main():\n    const A: int = {init}\n    v: int = 4\n    match v:\n        case A:\n            print(\"hit\")\n        case _:\n            print(\"miss\")\n"
            : h.Declare($"const A: int = {init}")
              + $"def main():\n    v: int = 4\n    match v:\n        case {h.Read}:\n            print(\"hit\")\n        case _:\n            print(\"miss\")\n"),
    };

    /// <summary>The compile-time initializer and the one whose lowering is a call.</summary>
    private const string CompileTimeInit = "4";
    private const string CallInit = "max(1, 4)";

    /// <summary>
    /// Consumer cells this seam does not decide, each with its reason and the issue that owns it.
    /// </summary>
    private static readonly Dictionary<string, string> HostConsumerNotApplicable = new(StringComparer.Ordinal)
    {
        ["Local×MethodDefault"] =
            "a local const lives in function scope — a class member has no enclosing function to own it",
        ["NestedClassField×MatchCase"] =
            "a three-part qualified name in a pattern head is SPY0203 'Type Outer has no member Holder' "
            + "at BASE and HEAD alike — nested-type member access in a pattern head, #1799, not this seam",
        ["NestedStructField×MatchCase"] =
            "a three-part qualified name in a pattern head is SPY0203 — nested-type member access in a pattern, #1799",
        ["NestedInterfaceField×MatchCase"] =
            "a three-part qualified name in a pattern head is SPY0203 — nested-type member access in a pattern, #1799",
    };

    private static HostConsumer HC(string name) => HostConsumers.Single(c => c.Name == name);

    public static IEnumerable<object[]> HostConsumerCells =>
        from h in ConstHosts
        from c in HostConsumers
        where !HostConsumerNotApplicable.ContainsKey($"{h.Name}×{c.Name}")
        select new object[] { h.Name, c.Name };

    private const int HostConsumerCount = 5;
    private const int HostConsumerNotApplicableCount = 4;

    [Fact]
    public void HostConsumerMatrix_IsTotalOverItsAxes()
    {
        HostConsumers.Length.Should().Be(HostConsumerCount);
        HostConsumers.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        HostConsumerNotApplicable.Should().HaveCount(HostConsumerNotApplicableCount,
            "every N/A cell is written down with its reason and its issue");

        var product = ConstHostCount * HostConsumerCount;
        var keys = (from h in ConstHosts from c in HostConsumers select $"{h.Name}×{c.Name}").ToHashSet();
        HostConsumerNotApplicable.Keys.Should().OnlyContain(k => keys.Contains(k),
            "an N/A key must name a real cell (stale entries fail)");
        (HostConsumerCells.Count() + HostConsumerNotApplicableCount).Should().Be(product,
            $"applicable + N/A must be the whole product ({ConstHostCount} × {HostConsumerCount})");
    }

    /// <summary>
    /// A const whose fact is TRUE reads in every constant position, at every host, and prints its
    /// declared value.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostConsumerCells))]
    public void CompileTimeConst_ReadsAtEveryConsumer(string host, string consumer)
    {
        var source = HC(consumer).Compose(CH(host), CompileTimeInit);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {consumer}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{host} × {consumer}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(consumer == "MatchCase" ? "hit\n" : "4\n",
            $"[{host} × {consumer}] reads the const's declared value\n{source}");
    }

    /// <summary>
    /// A const whose fact is FALSE (its initializer lowers to a call) is refused BY NAME at every
    /// consumer that needs a C# constant — SPY0401 for a default, SPY0605 for a pattern head —
    /// never by Roslyn behind SPY0908. A plain read is not a constant position, so it still runs.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostConsumerCells))]
    public void NonCompileTimeConst_IsRefusedByNameAtEveryConsumer(string host, string consumer)
    {
        var source = HC(consumer).Compose(CH(host), CallInit);

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {consumer}] must never produce SPY0908 — the refusal is ours, not Roslyn's. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        // A plain read is not a constant position, and a QUALIFIED pattern head is not one either:
        // the emitter lowers `case Holder.A:` to `case var t when t == Holder.A`, a guard that needs
        // no C# constant. Only the bare spelling becomes a C# constant pattern, so only it is
        // refused. This is the discriminating half of the cell — the two spellings take different
        // emitter routes for the same const.
        var isConstantPosition = consumer != "Print"
            && !(consumer == "MatchCase" && CH(host).Read.Contains('.', StringComparison.Ordinal));

        if (!isConstantPosition)
        {
            result.Success.Should().BeTrue(
                $"[{host} × {consumer}] is not a constant position — the const emits non-const and "
                + $"the program runs. "
                + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be(consumer == "MatchCase" ? "hit\n" : "4\n",
                $"[{host} × {consumer}] still produces the right answer\n{source}");
            return;
        }

        var expectedCode = consumer == "MatchCase"
            ? DiagnosticCodes.SemanticOverflow.ConstantPatternNotCompileTime
            : DiagnosticCodes.Validation.NonConstDefault;

        result.Success.Should().BeFalse($"[{host} × {consumer}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == expectedCode,
            $"[{host} × {consumer}] must report {expectedCode}. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.First(d => d.Code == expectedCode).Message
            .Should().Contain("is not a compile-time constant",
                $"[{host} × {consumer}] names the const that broke constancy\n{source}");
    }

    // ══ One declaration, one symbol: the class-body const spellings ══════════════════════════

    /// <summary>
    /// A <c>const</c> declared in a type body is ONE symbol at every spelling that reads it (#1791,
    /// #1795).
    ///
    /// <para>It was two. <c>NameResolver</c> creates the field symbol and puts it in the type's
    /// <c>Fields</c>; the checker enters a FRESH scope for the body, so its
    /// <c>Lookup(name, searchParents: false)</c> missed that symbol and took the function-level-const
    /// arm, defining a SECOND one. Every reference bound to the second while
    /// <c>CodeGenInfoComputer.ProcessField</c> named the first, so a bare read in a signature
    /// position had no CodeGenInfo and the emitter threw SPY0909, and a sibling-const initializer
    /// could not resolve the name at all.</para>
    ///
    /// <para>Each cell EXECUTES, because the failure was at code generation: a cell that only
    /// checked diagnostics would have passed over the ICE.</para>
    /// </summary>
    [Theory]
    // The bare spelling in an instance method's parameter default — SPY0401 at 646b9cf08,
    // SPY0909 at fb728b9be.
    [InlineData("BareInstanceMethodDefault",
        "class C:\n    const K: int = 1\n\n    def m(self, x: int = K) -> None:\n        print(x)\n\ndef main():\n    C().m()\n",
        "1\n")]
    // The @static twin, same history.
    [InlineData("BareStaticMethodDefault",
        "class C:\n    const K: int = 1\n\n    @static\n    def m(x: int = K) -> None:\n        print(x)\n\ndef main():\n    C.m()\n",
        "1\n")]
    // The qualified spelling, which printed 1 at 646b9cf08 and CS1736 at fb728b9be.
    [InlineData("QualifiedMethodDefault",
        "class C:\n    const K: int = 1\n\nclass D:\n    def m(self, x: int = C.K) -> None:\n        print(x)\n\ndef main():\n    D().m()\n",
        "1\n")]
    // A sibling const read BACKWARD — SPY0909 at both shas (#1795).
    [InlineData("SiblingConstBackward",
        "class C:\n    const A: int = 3\n    const B: int = A\n\ndef main():\n    print(C.B)\n",
        "3\n")]
    // A sibling const read FORWARD — SPY0200 at both shas. A type body resolves its own const
    // dependency order, as C# does (#1795).
    [InlineData("SiblingConstForward",
        "class C:\n    const A: int = B\n    const B: int = 3\n\ndef main():\n    print(C.A)\n",
        "3\n")]
    // A bare read in a method BODY still resolves the const (the class-scope rule R-Y refuses
    // instance fields by bare name, not consts a signature or initializer reads).
    [InlineData("QualifiedBodyRead",
        "class C:\n    const K: int = 7\n\n    def m(self) -> None:\n        print(C.K)\n\ndef main():\n    C().m()\n",
        "7\n")]
    public void ClassBodyConst_IsOneSymbol_AtEverySpelling(string label, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError
                || d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] two symbols for one const is what produced SPY0909/SPY0908 here. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile and run. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected,
            $"[{label}] reads the const's declared value\n{source}");
    }

    /// <summary>
    /// The emitted declaration for the sibling chain: BOTH consts carry <c>const</c>, so the second
    /// is a compile-time constant built from the first rather than a <c>static readonly</c> read at
    /// runtime. This is what makes the executing cell above discriminating — a pair of
    /// <c>static readonly</c> fields prints 3 too.
    /// </summary>
    [Fact]
    public void SiblingConstChain_BothEmitAsCSharpConst()
    {
        var result = CompileAndExecute(
            "class C:\n    const A: int = 3\n    const B: int = A\n\ndef main():\n    print(C.B)\n");

        result.Success.Should().BeTrue(
            $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}");
        result.GeneratedCSharp.Should().NotBeNull();
        result.GeneratedCSharp.Should().Contain("public const int A = 3",
            $"the referenced const is compile-time\n{result.GeneratedCSharp}");
        result.GeneratedCSharp.Should().Contain("public const int B = A",
            $"and so is the one that reads it\n{result.GeneratedCSharp}");
    }

    // ══ Struct constructor roster: consts are excluded (#1794) ════════════════════════════════

    [Theory]
    [InlineData("StructWithConstAndField",
        "struct S:\n    x: int\n    const K: int = 7\n\ndef main():\n    s = S(5)\n    print(s.x)\n    print(S.K)\n",
        "5\n7\n")]
    [InlineData("StructWithConstAndFieldParameterless",
        "struct S:\n    x: int = 2\n    const K: int = 7\n\ndef main():\n    s = S()\n    print(s.x)\n    print(S.K)\n",
        "2\n7\n")]
    [InlineData("AllConstStruct",
        "struct S:\n    const A: int = 1\n    const B: str = \"two\"\n\ndef main():\n    print(S.A)\n    print(S.B)\n",
        "1\ntwo\n")]
    [InlineData("InterfaceConstReadFromImplementingClass",
        "interface I:\n    const K: int = 1\n\nclass C(I):\n    def read(self) -> int:\n        return I.K\n\ndef main():\n    print(C().read())\n",
        "1\n")]
    public void StructAndInterfaceConst_RosterAndAccess(string label, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{label}] must never ICE. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected, $"[{label}]\n{source}");
    }

    // ══ The decorator-argument matrix ════════════════════════════════════════════════════════

    /// <summary>
    /// A bracket-attribute argument kind: the attribute parameter type it needs, the default for the
    /// attribute's second parameter, the declarations the argument itself needs, its spelling, the
    /// C# text the emitted attribute argument must carry when admitted, and the refusal fragment
    /// otherwise. Reading the EMITTED argument is what makes the cell discriminating — a program
    /// that merely compiles says nothing about which value reached the attribute.
    /// </summary>
    private sealed record DecoratorArgument(
        string Name,
        string ParamType,
        string ParamDefault,
        string Prelude,
        string Expr,
        string? EmittedText,
        string? RefusedFragment);

    private static readonly DecoratorArgument[] DecoratorArguments =
    {
        new("StringLiteral", "str", "\"d\"", "", "\"gone\"", "\"gone\"", null),
        new("IntLiteral", "int", "1", "", "3", "3", null),
        new("EnumMember", "Color", "Color.GREEN",
            "enum Color:\n    RED = 1\n    GREEN = 2\n\n", "Color.RED", "Color.RED", null),
        // A const reference and a constant composition are C# constant expressions, so they read
        // here exactly as they read in a parameter default (#1782, #1801).
        new("StrConst", "str", "\"d\"", "const MSG: str = \"gone\"\n\n", "MSG", "MSG", null),
        new("IntConst", "int", "1", "const N: int = 3\n\n", "N", "N", null),
        new("Folded", "str", "\"d\"", "", "\"a\" + \"b\"", "\"a\" + \"b\"", null),
        // The emitter's composition arm is ONE pattern — Parenthesized or BinaryOp or UnaryOp or
        // ConditionalExpression — so each alternative needs a row or deleting it is caught by
        // nothing. The parentheses survive into the emitted argument verbatim.
        new("Parenthesized", "str", "\"d\"", "", "(\"a\")", "(\"a\")", null),
        new("ConditionalOfConsts", "str", "\"d\"", "const ON: bool = True\n\n",
            "\"a\" if ON else \"b\"", "ON ? \"a\" : \"b\"", null),
        // The UNARY arm of the same rule: `not` is native for the only operand type it takes.
        new("NegatedBool", "bool", "False", "", "not True", "!true", null),
        new("OptionalConst", "int", "1", "const O: int? = Some(1)\n\n", "O", null,
            "is not a compile-time constant"),
        new("CallInitializedConst", "str", "\"d\"", "const CI: str = \"a\".upper()\n\n", "CI", null,
            "is not a compile-time constant"),
        new("Call", "str", "\"d\"", "def g() -> str:\n    return \"x\"\n\n", "g()", null,
            "must be a compile-time constant"),
    };

    private sealed record DecoratorPosition(string Name, Func<DecoratorArgument, string> Spell);

    private static readonly DecoratorPosition[] DecoratorPositions =
    {
        new("Positional", a => $"tag_attribute({a.Expr})"),
        new("Keyword", a => $"tag_attribute({a.ParamDefault}, b={a.Expr})"),
    };

    // Anchored to literals, not to the arrays under test.
    private const int DecoratorArgumentCount = 12;
    private const int DecoratorPositionCount = 2;
    private const int DecoratorAdmittedCount = 9;
    private const int DecoratorRefusedCount = 3;

    private static DecoratorArgument DA(string name) => DecoratorArguments.Single(a => a.Name == name);

    private static DecoratorPosition DP(string name) => DecoratorPositions.Single(p => p.Name == name);

    /// <summary>
    /// A user bracket attribute whose two parameters take the argument's own type, so positional and
    /// keyword are the SAME argument in two positions rather than two different attributes.
    /// </summary>
    private static string ComposeDecoratorCell(DecoratorArgument arg, DecoratorPosition position) =>
        "from System import Attribute\n\n"
        + arg.Prelude
        + $"class TagAttribute(Attribute):\n    a: {arg.ParamType}\n    b: {arg.ParamType}\n\n"
        + $"    def __init__(self, a: {arg.ParamType}, b: {arg.ParamType} = {arg.ParamDefault}):\n"
        + "        super().__init__()\n        self.a = a\n        self.b = b\n\n"
        + $"@[{position.Spell(arg)}]\ndef foo() -> int:\n    return 1\n\ndef main():\n    print(foo())\n";

    public static IEnumerable<object[]> DecoratorCells =>
        from a in DecoratorArguments
        from p in DecoratorPositions
        select new object[] { a.Name, p.Name };

    [Fact]
    public void DecoratorMatrix_IsTotalOverItsAxes()
    {
        DecoratorArguments.Length.Should().Be(DecoratorArgumentCount);
        DecoratorPositions.Length.Should().Be(DecoratorPositionCount);
        DecoratorArguments.Select(a => a.Name).Should().OnlyHaveUniqueItems();
        DecoratorPositions.Select(p => p.Name).Should().OnlyHaveUniqueItems();
        DecoratorCells.Count().Should().Be(DecoratorArgumentCount * DecoratorPositionCount);

        DecoratorArguments.Count(a => a.EmittedText != null).Should().Be(DecoratorAdmittedCount,
            "the admitted half is written down");
        DecoratorArguments.Count(a => a.EmittedText == null).Should().Be(DecoratorRefusedCount,
            "the refused half is written down");
        (DecoratorAdmittedCount + DecoratorRefusedCount).Should().Be(DecoratorArgumentCount);

    }

    /// <summary>
    /// A bracket-attribute argument is admitted iff the constant fact allows it, at BOTH positions,
    /// and the emitted attribute carries the argument's own text. A refused argument reports SPY0425
    /// naming the reason — never SPY0908 or SPY0909.
    /// </summary>
    [Theory]
    [MemberData(nameof(DecoratorCells))]
    public void DecoratorArgument_IsAdmittedIffTheFact(string argument, string position)
    {
        var a = DA(argument);
        var source = ComposeDecoratorCell(a, DP(position));

        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                || d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError,
            $"[{argument} × {position}] must never ICE — a decorator argument is refused by name. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        if (a.EmittedText != null)
        {
            result.Success.Should().BeTrue(
                $"[{argument} × {position}] must compile and run. "
                + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be("1\n", $"[{argument} × {position}] runs\n{source}");
            result.GeneratedCSharp.Should().NotBeNull();
            result.GeneratedCSharp.Should().Contain(a.EmittedText,
                $"[{argument} × {position}] the emitted attribute carries the argument's own text — "
                + $"an exit code alone cannot say WHICH value reached the attribute\n{result.GeneratedCSharp}");
            return;
        }

        result.Success.Should().BeFalse($"[{argument} × {position}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Validation.NonConstantDecoratorArgument,
            $"[{argument} × {position}] must report SPY0425. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.First(d => d.Code == DiagnosticCodes.Validation.NonConstantDecoratorArgument)
            .Message.Should().Contain(a.RefusedFragment!,
                $"[{argument} × {position}] names why the argument is not constant\n{source}");
    }

    // ══ Match-case constant-pattern matrix ════════════════════════════════════════════════════

    /// <summary>
    /// Match-case constant patterns: a const used in <c>case NAME:</c> must be compile-time.
    /// Compile-time consts match and print "hit"; non-compile-time consts report SPY0605 with
    /// the guard steer. Positive control: <c>case 3:</c> (literal) always matches.
    /// </summary>
    [Theory]
    [InlineData("Literal", "const A: int = 3\n", "case A:", true, null)]
    [InlineData("Folded", "const A: int = 1 + 2\n", "case A:", true, null)]
    [InlineData("ConstRefCall", "const FM: float = max(4.0, 1.0)\n", "case FM:", false,
        DiagnosticCodes.SemanticOverflow.ConstantPatternNotCompileTime)]
    [InlineData("OptionalConstRef", "const O: int? = Some(1)\n", "case O:", false,
        DiagnosticCodes.SemanticOverflow.ConstantPatternNotCompileTime)]
    public void MatchCaseConstantPattern_AdmittedOrRefused(
        string label, string prelude, string caseArm, bool shouldCompile, string? expectedCode)
    {
        var scrutineeType = label.Contains("float") ? "float" : (label.Contains("Optional") ? "int?" : "int");
        var scrutineeValue = label.Contains("float") ? "4.0" : (label.Contains("Optional") ? "Some(3)" : "3");
        var source =
            $"{prelude}\ndef main():\n    v: {scrutineeType} = {scrutineeValue}\n    match v:\n" +
            $"        {caseArm}\n            print(\"hit\")\n        case _:\n            print(\"miss\")\n";

        var result = CompileAndExecute(source);

        if (shouldCompile)
        {
            result.Success.Should().BeTrue(
                $"[MatchCase × {label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be("hit\n",
                $"[MatchCase × {label}] the const matches its own value\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse(
                $"[MatchCase × {label}] must be refused\n{source}");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == expectedCode,
                $"[MatchCase × {label}] must report {expectedCode}. Got: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        }
    }

    /// <summary>Positive control: a literal in a case arm always works.</summary>
    [Fact]
    public void MatchCaseLiteral_PositiveControl()
    {
        var source = "def main():\n    v: int = 3\n    match v:\n        case 3:\n            print(\"hit\")\n        case _:\n            print(\"miss\")\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"literal case must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("hit\n");
    }

    // ── Classifier scan (guarded-by anchor for DispatchSiteInventoryTests) ───────────────────
    // DispatchSiteInventoryTests requires the guarded-by class to SCAN the site: this test reads
    // ConstantDefaultClassifier.cs, collects every EmittableConstantKind the "Classify" switch
    // returns, and asserts the set equals the enum's members — a kind added to the enum without a
    // classifying arm (or an arm deleted) goes red. The enum size is anchored to a literal so the
    // comparison is not "the enum against itself".

    private const int EmittableConstantKindCount = 16;

    /// <summary>
    /// The qualified-name arm dispatches through <c>ConstantDefaultClassifier.RootsInIdentifier</c>,
    /// which walks a member chain down to its root. Scanning it keeps this class's guarded-by claim
    /// for that site honest: the chain must bottom out in an identifier and in nothing else.
    /// </summary>
    [Fact]
    public void MemberChainRoot_DispatchesOnIdentifierAndMemberAccessOnly()
    {
        var arms = Infrastructure.SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Semantic/Validation/ConstantDefaultClassifier.cs", "RootsInIdentifier");

        arms.Should().BeEquivalentTo(new[] { "Identifier", "MemberAccess" },
            "a qualified constant is an identifier under zero or more member accesses; every other "
            + "receiver shape is a runtime expression and answers false");
    }

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
        start.Should().BeGreaterThan(-1);
        end.Should().BeGreaterThan(start);
        var classifyBody = source.Substring(start, end - start);
        var returned = Regex.Matches(classifyBody, @"EmittableConstantKind\.(?<name>[A-Z]\w*)")
            .Select(m => m.Groups["name"].Value).ToHashSet(StringComparer.Ordinal);

        returned.Should().BeEquivalentTo(declared,
            "every kind the enum declares is produced by Classify, and Classify names no kind the enum lacks");
    }

}
