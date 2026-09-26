using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// An <c>except</c> clause's type is a type position: it resolves through the same resolver as an
/// annotation, with the same refusal and the same symbol. The unbound handler used to classify its
/// type and, when the name denoted no type, record nothing and report nothing — the emitter then
/// spelled the raw text, so <c>except NoSuchErr:</c> was SPY0908 CS0246, and <c>except re.Error:</c>
/// (python's name is <c>re.error</c>) went from running at acd1d40a2 to SPY0908 CS0426 once the
/// module-as-namespace layout (#2039) left no C# type at that spelling. The annotation
/// <c>def f(e: re.Error)</c> and the bound <c>except re.Error as e:</c> already drew SPY0202.
/// </summary>
/// <remarks>
/// Cells: position {except, except bound, except tuple, except*, annotation, isinstance, raise} ×
/// spelling {<c>re.error</c>, <c>socket.timeout</c>, a user module's <c>errs.MyErr</c> — each runs;
/// <c>re.Error</c>, <c>socket.Timeout</c>, <c>errs.myErr</c>, the bare <c>NoSuchErr</c> — each refused}.
/// A refused annotation-shaped cell carries exactly the annotation cell's one diagnostic (same code,
/// same message); an expression-shaped cell (isinstance, raise) is refused as a value. No refused
/// cell is SPY0908.
/// </remarks>
public class ExceptTypeResolutionMatrixTests : StdlibAwareIntegrationTestBase
{
    public ExceptTypeResolutionMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string ErrsModule =
        "class MyErr(Exception):\n"
        + "    def __init__(self, m: str):\n"
        + "        super().__init__(m)\n";

    private static readonly string[] WorkingSpellings = ["re.error", "socket.timeout", "errs.MyErr"];
    private static readonly string[] RefusedSpellings = ["re.Error", "socket.Timeout", "errs.myErr", "NoSuchErr"];

    /// <summary>Annotation-shaped positions: each must resolve exactly as <c>annotation</c> does.</summary>
    private static readonly string[] TypePositions = ["except", "except_bound", "except_tuple", "except_star", "annotation"];

    /// <summary>Expression-shaped positions: the spelling is a value there, not a type.</summary>
    private static readonly string[] ValuePositions = ["isinstance", "raise"];

    private static string MainModule(string position, string spelling) => "import re\nimport socket\nimport errs\n\n" + position switch
    {
        "except" => "def main() -> None:\n    try:\n        raise ValueError(\"x\")\n"
            + $"    except {spelling}:\n        print(\"caught\")\n    except ValueError:\n        print(\"ve\")\n",
        "except_bound" => "def main() -> None:\n    try:\n        raise ValueError(\"x\")\n"
            + $"    except {spelling} as e:\n        print(\"caught\")\n    except ValueError:\n        print(\"ve\")\n",
        "except_tuple" => "def main() -> None:\n    try:\n        raise ValueError(\"x\")\n"
            + $"    except ({spelling}, KeyError):\n        print(\"caught\")\n    except ValueError:\n        print(\"ve\")\n",
        "except_star" => "def main() -> None:\n    errors: list[Exception] = [ValueError(\"x\")]\n    try:\n"
            + "        raise ExceptionGroup(\"g\", errors)\n"
            + $"    except* {spelling}:\n        print(\"caught\")\n    except* ValueError:\n        print(\"ve\")\n",
        "annotation" => $"def f(e: {spelling}) -> None:\n    print(\"caught\")\n\ndef main() -> None:\n    print(\"ve\")\n",
        "isinstance" => "def main() -> None:\n    e: object = ValueError(\"x\")\n"
            + $"    print(\"caught\" if isinstance(e, {spelling}) else \"ve\")\n",
        "raise" => $"def main() -> None:\n    try:\n        raise {spelling}(\"boom\")\n    except Exception:\n        print(\"raised\")\n",
        _ => throw new ArgumentOutOfRangeException(nameof(position)),
    };

    private static string ExpectedOutput(string position) => position == "raise" ? "raised" : "ve";

    private ExecutionResult Run(string position, string spelling)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"sharpy_except_type_{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        try
        {
            File.WriteAllText(Path.Combine(projectDir, "errs.spy"), ErrsModule);
            File.WriteAllText(Path.Combine(projectDir, "main.spy"), MainModule(position, spelling));
            return CompileAndExecuteProject(projectDir, "main.spy");
        }
        finally
        {
            try
            { Directory.Delete(projectDir, recursive: true); }
            catch (IOException) { }
        }
    }

    private static List<CompilerDiagnostic> Errors(ExecutionResult result)
        => result.RawDiagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Error).ToList();

    private static string Describe(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message).Concat(result.CompilationErrors));

    public static IEnumerable<object[]> WorkingCells()
        => TypePositions.Concat(ValuePositions).SelectMany(p => WorkingSpellings.Select(s => new object[] { p, s }));

    public static IEnumerable<object[]> RefusedTypeCells()
        => TypePositions.Where(p => p != "annotation").SelectMany(p => RefusedSpellings.Select(s => new object[] { p, s }));

    public static IEnumerable<object[]> RefusedValueCells()
        => ValuePositions.SelectMany(p => RefusedSpellings.Select(s => new object[] { p, s }));

    [Theory]
    [MemberData(nameof(WorkingCells))]
    public void PythonSpelling_Runs_InEveryPosition(string position, string spelling)
    {
        var result = Run(position, spelling);

        result.Success.Should().BeTrue($"[{position} × {spelling}] {Describe(result)}");
        result.StandardOutput.Trim().Should().Be(ExpectedOutput(position));
    }

    [Theory]
    [MemberData(nameof(RefusedTypeCells))]
    public void UnresolvedSpelling_InATypePosition_IsTheAnnotationRefusal(string position, string spelling)
    {
        var annotation = Errors(Run("annotation", spelling));
        annotation.Should().ContainSingle($"[annotation × {spelling}] is the reference refusal");
        annotation[0].Code.Should().Be(DiagnosticCodes.Semantic.UndefinedType);

        var result = Run(position, spelling);

        result.Success.Should().BeFalse($"[{position} × {spelling}] must be refused");
        var errors = Errors(result);
        errors.Select(e => e.Code).Should().NotContain(
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{position} × {spelling}] never SPY0908");
        errors.Should().ContainSingle($"[{position} × {spelling}] one mistake, one diagnostic: {Describe(result)}");
        (errors[0].Code, errors[0].Message).Should().Be(
            (annotation[0].Code, annotation[0].Message),
            $"[{position} × {spelling}] resolves exactly as the annotation does");
    }

    [Theory]
    [MemberData(nameof(RefusedValueCells))]
    public void UnresolvedSpelling_InAValuePosition_IsRefused(string position, string spelling)
    {
        var result = Run(position, spelling);

        result.Success.Should().BeFalse($"[{position} × {spelling}] must be refused");
        var errors = Errors(result);
        errors.Select(e => e.Code).Should().NotContain(
            DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, $"[{position} × {spelling}] never SPY0908");
        errors.Should().ContainSingle($"[{position} × {spelling}] {Describe(result)}");
        errors[0].Code.Should().BeOneOf(
            [DiagnosticCodes.Semantic.UndefinedVariable, DiagnosticCodes.Semantic.UndefinedMember],
            $"[{position} × {spelling}] {Describe(result)}");
    }

    [Fact]
    public void Matrix_IsTotal()
    {
        WorkingCells().Should().HaveCount(21);
        RefusedTypeCells().Should().HaveCount(16);
        RefusedValueCells().Should().HaveCount(8);
    }
}
