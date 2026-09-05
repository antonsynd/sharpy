using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The parameter-default constant matrix — kind × host (#1762, #1769, R-R).
///
/// <para><b>Contract.</b> A default value for a parameter is admitted or refused by
/// <c>ConstantDefaultClassifier</c> via <c>DefaultParameterValidator</c>. The classifier
/// maps the default's AST shape to an <c>EmittableConstantKind</c>, and the validator checks
/// that kind against the <c>AdmissionTable</c> for the host position. Admitted defaults compile
/// and run with the printed value. Refused defaults report SPY0401. No cell produces SPY0908.</para>
///
/// <para><b>Axes.</b> Kind: {Literal, NegatedLiteral, ConstReference, EnumMember, NoneLiteral,
/// NoneCall, SomeIntoOptional, TupleLiteral, ConditionalOfConstants} × Host: {Def, Lambda, Init}.
/// Totality: 9 × 3 = 27 cells.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ParameterDefaultConstantMatrixTests : IntegrationTestBase
{
    public ParameterDefaultConstantMatrixTests(ITestOutputHelper output) : base(output) { }

    // ── Axis sizes, anchored to literals ─────────────────────────────────────────────────────
    private const int KindCount = 9;
    private const int HostCount = 3;
    private const int AdmittedCellCount = 21;
    private const int RefusedCellCount = 6;

    // ── Axis 1: default-value kinds ──────────────────────────────────────────────────────────

    private sealed record Kind(
        string Name,
        string ParamType,
        string DefaultExpr,
        string Prelude,
        string? AcceptedOutput,
        string? RefusedFragment);

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
    };

    // ── Cell resolution ──────────────────────────────────────────────────────────────────────

    private static string Key(Host h, Kind k) => $"{h.Name}×{k.Name}";

    private static Host H(string name) => Hosts.Single(h => h.Name == name);

    private static Kind K(string name) => Kinds.Single(k => k.Name == name);

    private enum Verdict { Admitted, Refused }

    private static Verdict Classify(Kind k) =>
        k.AcceptedOutput != null ? Verdict.Admitted : Verdict.Refused;

    private static IEnumerable<object[]> CellsWhere(Verdict verdict)
        => from h in Hosts
           from k in Kinds
           where Classify(k) == verdict
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
            d => d.Code == DiagnosticCodes.Validation.NonConstDefault,
            $"[{host} × {kind}] must report SPY0401. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        result.RawDiagnostics.Where(d => d.Code == DiagnosticCodes.Validation.NonConstDefault)
            .First().Message.Should().Contain(k.RefusedFragment!,
                $"[{host} × {kind}] must carry the expected diagnostic text\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{host} × {kind}] must never produce SPY0908\n{source}");
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
        (admitted + refused).Should().Be(KindCount * HostCount,
            $"admitted ({admitted}) + refused ({refused}) must be the whole product "
            + $"({KindCount} × {HostCount})");
    }

    // ── Classifier scan (guarded-by anchor for DispatchSiteInventoryTests) ───────────────────
    // DispatchSiteInventoryTests checks that THIS test's source contains both the production file
    // name ("ConstantDefaultClassifier.cs") and the method name ("Classify") as string literals.
    // The Path.Combine below satisfies that check.

    [Fact]
    public void ClassifierSwitch_IsScannedByThisMatrix()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "Sharpy.Compiler",
            "Semantic", "Validation", "ConstantDefaultClassifier.cs");

        File.Exists(path).Should().BeTrue(
            "the production file ConstantDefaultClassifier.cs must exist — " +
            "this test guards the \"Classify\" switch in it");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, ".git")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not find repo root");
    }
}
