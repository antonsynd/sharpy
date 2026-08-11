using Microsoft.CodeAnalysis;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// The SPY0908 net is a net, not an error channel (#1387, #1146 spine). It exists so a raw
/// <c>CSxxxx</c> from the compiler's own generated C# can never reach the user; it says "this is a
/// Sharpy compiler bug — please report it". That sentence is only true when the compiler believed
/// the program was good. A module-class name collision reports SPY0520 and the emitter then
/// <em>drops</em> the refused unit's C#, so the surviving units are compiled with no entry point and
/// Roslyn answers CS5001 — a consequence of the deliberate refusal, which the net used to hand back
/// to the user as an internal compiler error alongside the real diagnostic.
///
/// <para>
/// These tests run the whole pipeline <b>including assembly emit</b>. That matters: SPY0908 is a
/// Phase 7 diagnostic and is unreachable from <c>emit</c>/analysis-only compiles, so a test written
/// against those paths would assert its absence vacuously.
/// </para>
/// </summary>
public class GeneratedCodeNetSuppressionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<string> _tempDirs = new();

    public GeneratedCodeNetSuppressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    // The two module-class-collision shapes that exist in the fixture corpus
    // (Integration/TestFixtures/name_collision/). Kept verbatim so a change to either fixture and a
    // change here stay comparable.
    private const string EscapedStructCollisionSource = """
        struct `CollidesWithModuleClass`:
            n: int


        def main() -> None:
            x = `CollidesWithModuleClass`(4)
            print(x.n)
        """;

    private const string UnionCollisionSource = """
        union CollidesWithModuleClass:
            case Circle(r: float)
            case Empty()


        def main() -> None:
            s: CollidesWithModuleClass = CollidesWithModuleClass.Circle(5.0)
            print(1)
        """;

    [Theory]
    [InlineData(EscapedStructCollisionSource)]
    [InlineData(UnionCollisionSource)]
    public void ModuleClassCollision_ReportsSpy0520_AndNotSpy0908(string source)
    {
        var result = CompileWithAssemblyEmit(source, "collides_with_module_class.spy");

        var codes = result.Diagnostics.GetAll().Select(d => d.Code).ToList();
        _output.WriteLine("Diagnostics: " + string.Join(", ", codes));

        Assert.Contains(DiagnosticCodes.CodeGen.NameCollision, codes);
        Assert.DoesNotContain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, codes);

        // Non-vacuity guard. Without this the assertion above would also pass if Phase 7 had never
        // run at all, or if the collision had stopped being detected. The suppressed channel is
        // populated only by an assembly compile that actually happened and that Roslyn actually
        // rejected — so its contents prove the SPY0908 absence is the gate's doing, and prove the
        // leak corpus the #1146 sweeps depend on did not silently shrink.
        Assert.Contains(result.SuppressedGeneratedCodeDiagnostics, d => d.Code == "CS5001");
        Assert.DoesNotContain(
            result.SuppressedGeneratedCodeDiagnostics,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    /// <summary>
    /// Positive control for the pair above, at the seam #1059 made <c>internal</c> for exactly this
    /// purpose: the same deliberately invalid generated C#, compiled by the same
    /// <see cref="AssemblyCompiler"/>, differing only in whether errors were already reported.
    /// A clean bag must still produce SPY0908 — the net is disarmed by the gate, not deleted.
    /// </summary>
    [Fact]
    public void InvalidGeneratedCSharp_WithCleanBag_StillReportsSpy0908()
    {
        var result = CompileGeneratedCSharp(InvalidGeneratedCSharp, compilationAlreadyFailed: false);

        Assert.False(result.Success);
        Assert.Contains(
            result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
        Assert.Empty(result.SuppressedGeneratedCodeDiagnostics);
    }

    [Fact]
    public void InvalidGeneratedCSharp_WithErrorsAlreadyReported_SuppressesSpy0908ButKeepsTheEvidence()
    {
        var logger = new DebugCapturingLogger();
        var result = CompileGeneratedCSharp(
            InvalidGeneratedCSharp, compilationAlreadyFailed: true, logger);

        Assert.False(result.Success);
        Assert.DoesNotContain(
            result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        // Test-reachable channel: the raw Roslyn id and text survive, unwrapped.
        Assert.NotEmpty(result.SuppressedGeneratedCodeDiagnostics);
        Assert.All(result.SuppressedGeneratedCodeDiagnostics,
            d => Assert.True(d.Code?.StartsWith("CS", StringComparison.Ordinal) == true));

        // Debug log: the other half of "must remain observable". A verbose build still carries
        // what a genuine emitter bug would have left behind.
        Assert.Contains(logger.DebugMessages, m => m.Contains("#1387") && m.Contains("CS"));
    }

    // Deliberately invalid C#: an unknown type, so Roslyn answers CS0246 regardless of the
    // reference set. Mirrors how #1059's own tests exercise the net.
    private const string InvalidGeneratedCSharp = """
        public static class Program
        {
            public static void Main()
            {
                NoSuchTypeAnywhere x = default;
            }
        }
        """;

    /// <summary>
    /// Compiles <paramref name="source"/> through <see cref="ProjectCompiler"/> with
    /// <c>emitAssembly: true</c> — the path <c>sharpyc run</c> takes and the only one that reaches
    /// the SPY0908 net.
    /// </summary>
    private ProjectCompilationResult CompileWithAssemblyEmit(string source, string fileName)
    {
        var dir = CreateTempDir();
        var sourcePath = Path.Combine(dir, fileName);
        File.WriteAllText(sourcePath, source);

        var config = new ProjectConfig
        {
            ProjectDirectory = dir,
            ProjectFilePath = Path.Combine(dir, "test.spyproj"),
            RootNamespace = "SuppressionTest",
            OutputType = "exe",
            EntryPoint = sourcePath,
            SourceFiles = new List<string> { sourcePath },
            Configuration = "Debug",
            TargetFramework = "net10.0",
            References = { SharpyCoreReference.Location },
            OutputAssemblyPathOverride = Path.Combine(dir, "out.exe")
        };

        var logger = new TestHelpers.OutputTestLogger(_output);
        var moduleRegistry = new ModuleRegistry(logger);
        moduleRegistry.LoadReference(SharpyCoreReference.Location);

        var projectCompiler = new ProjectCompiler(logger, moduleRegistry);
        return projectCompiler.Compile(config, CancellationToken.None, emitAssembly: true);
    }

    private AssemblyCompilationResult CompileGeneratedCSharp(
        string csharp, bool compilationAlreadyFailed, ICompilerLogger? logger = null)
    {
        var dir = CreateTempDir();
        var config = new ProjectConfig
        {
            ProjectDirectory = dir,
            ProjectFilePath = Path.Combine(dir, "test.spyproj"),
            RootNamespace = "SuppressionTest",
            OutputType = "exe",
            Configuration = "Debug",
            TargetFramework = "net10.0",
            OutputAssemblyPathOverride = Path.Combine(dir, "out.exe")
        };

        var compiler = new AssemblyCompiler(logger ?? new TestHelpers.OutputTestLogger(_output));
        return compiler.CompileToAssembly(
            new Dictionary<string, string> { [Path.Combine(dir, "generated.cs")] = csharp },
            new Dictionary<string, SyntaxTree>(),
            config,
            compilationAlreadyFailed);
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_net_1387_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    /// <summary>
    /// Reports debug logging as enabled and records what is written to it, so the "suppressed but
    /// still observable" half of the #1387 contract is asserted rather than assumed.
    /// </summary>
    private sealed class DebugCapturingLogger : ICompilerLogger
    {
        public List<string> DebugMessages { get; } = new();

        public void LogDebug(string message) => DebugMessages.Add(message);
        public bool IsEnabled(CompilerLogLevel level) => true;

        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) { }
        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogInfo(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
    }
}
