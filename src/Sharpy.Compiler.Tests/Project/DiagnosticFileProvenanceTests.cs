using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2032, behavioural half: every diagnostic in a compiled project's bag names the file it is
/// about. The type-check and code-generation merge seams stamp the unit's path on every path-less
/// diagnostic (the #1437 design the parse seams already used). Before this, the CodeGen-range
/// collisions CodeGenInfoComputer raises at semantic time (SPY0522/SPY0523/SPY0525) and every
/// validator warning reached the project bag with no path and rendered as <c>&lt;source&gt;</c>.
/// The emitter's own diagnostics (SPY0520 then, SPY0500 now) carried the path but no location.
/// </summary>
/// <remarks>
/// Cells: the producer {emitter (SPY0500, un-imported module), CodeGenInfoComputer (SPY0523),
/// validator warning (SPY0453), type check aborted at the error limit (SPY0220 ×101), a warning
/// anchored to a pattern node through the ILocatable overload (SPY0468, SPY0485 — #2070)}. Each cell
/// asserts that EVERY non-assembly diagnostic names the offending file, and checks the expected
/// code is present as its positive control.
/// </remarks>
[Collection("HeavyCompilation")]
public class DiagnosticFileProvenanceTests
{
    private readonly ITestOutputHelper _output;

    public DiagnosticFileProvenanceTests(ITestOutputHelper output) => _output = output;

    private static readonly Dictionary<string, (string File, string Source, string Code, int? Line, int? Column)> Cells = new()
    {
        // An emitter-time refusal (SPY0500; it was SPY0520 until #2039 retired it).
        ["emitter"] = ("thing.spy", "@lru_cache\nasync def f() -> int:\n    return 1\n", DiagnosticCodes.CodeGen.EmitError, 2, 1),
        // #2039: the members class of util.spy is UtilModule, so `def util_module` is the SPY0523 shape.
        ["codegeninfo"] = ("util.spy", "def util_module() -> int:\n    return 1\n", DiagnosticCodes.CodeGen.FunctionModuleClassCollision, 1, 5),
        ["validator"] = ("naming.spy", "def Compute() -> int:\n    return 1\n", DiagnosticCodes.Validation.NamingConventionWarning, null, null),
        ["aborted"] = ("bad.spy",
            "def f() -> None:\n" + string.Concat(Enumerable.Range(0, 101).Select(i => $"    x{i}: int = \"s\"\n")),
            DiagnosticCodes.Semantic.TypeMismatch, null, null),
        // #2070: reported through the ILocatable overload, which carried the pattern's span but no
        // line/column — rendered at 0:0.
        ["pattern_constant"] = ("shadow.spy",
            "const MAX = 100\n\ndef check(x: int) -> str:\n    match x:\n        case MAX:\n            return \"max\"\n        case _:\n            return \"other\"\n",
            DiagnosticCodes.Validation.ConstantPatternShadow, 5, 14),
        ["pattern_variant"] = ("variant.spy",
            "union Status:\n    case Idle\n    case Active\n\nconst Idle = 0\n\ndef check(s: Status) -> str:\n    match s:\n        case Idle:\n            return \"idle\"\n        case Active:\n            return \"active\"\n",
            DiagnosticCodes.Validation.VariantPatternShadowsConstant, 9, 14),
    };

    public static IEnumerable<object[]> Producers() => Cells.Keys.Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Producers))]
    public void EveryDiagnostic_NamesTheFileItIsAbout(string producer)
    {
        var (file, source, code, line, column) = Cells[producer];
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Provenance").WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(7)\n");
        helper.AddSourceFile(file, source);
        helper.CreateProjectFile();

        var result = helper.Compile();
        var all = result.Diagnostics.GetAll();
        var expectedPath = Path.GetFullPath(Path.Combine(helper.ProjectDirectory, "src", file));

        var hit = all.Where(d => d.Code == code).ToList();
        hit.Should().NotBeEmpty($"[{producer}] positive control: {code} is reported\n{Describe(all)}");
        if (line != null)
        {
            hit[0].Line.Should().Be(line, $"[{producer}] the reporting node's name token");
            hit[0].Column.Should().Be(column, $"[{producer}] the reporting node's name token");
        }

        var unattributed = all
            .Where(d => d.Phase != CompilerPhase.Assembly)
            .Where(d => string.IsNullOrEmpty(d.FilePath) || Path.GetFullPath(d.FilePath) != expectedPath)
            .ToList();
        unattributed.Should().BeEmpty($"[{producer}] every diagnostic names {file}\n{Describe(unattributed)}");

        all.Where(d => d.Phase == CompilerPhase.CodeGeneration)
            .Should().OnlyContain(d => !string.IsNullOrEmpty(d.FilePath), $"[{producer}]");
    }

    /// <summary>
    /// The analyze entry file has no SYMBOL path (#1087), so its type checker reports with none; the
    /// merge seam still names the file, on the completed arm and on the arm a type check aborted at
    /// the error limit (100) takes. In a project compile every type-checker error already carries
    /// its path, so this is the input on which the aborted seam's file scope is observable.
    /// </summary>
    [Theory]
    [InlineData("completed", 1)]
    [InlineData("aborted", 101)]
    public void Analyze_EntryFileDiagnostics_NameTheFile(string arm, int errors)
    {
        var source = "def main() -> None:\n"
            + string.Concat(Enumerable.Range(0, errors).Select(i => $"    x{i}: int = \"s\"\n"));
        var result = new Compiler(new CompilerOptions()).Analyze(source, "entry.spy");
        var all = result.Diagnostics.GetAll();

        all.Should().Contain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch, $"[{arm}] positive control\n{Describe(all)}");
        if (arm == "aborted")
        {
            all.Should().Contain(d => d.Code == DiagnosticCodes.Infrastructure.TooManyErrors,
                "the type check stopped at the error limit");
        }

        all.Where(d => d.Phase != CompilerPhase.Assembly && d.FilePath != "entry.spy")
            .Should().BeEmpty($"[{arm}] every diagnostic names entry.spy\n{Describe(all)}");
    }

    [Fact]
    public void Matrix_IsTotal() => Producers().Should().HaveCount(6);

    private static string Describe(IEnumerable<CompilerDiagnostic> diagnostics)
        => string.Join("\n", diagnostics.Select(d => $"{d.Code} {d.Phase} '{d.FilePath}' {d.Line}:{d.Column} {d.Message}"));
}
