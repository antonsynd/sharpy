using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Every public compiler entry point that builds a module registry from a
/// <see cref="ProjectConfig"/> reports a reference that resolves to no file as SPY0305 (#2223,
/// plan-05dd79 Decision 5), through the one registry read
/// <c>ModuleRegistry.MissingReferenceDiagnostics</c>.
/// </summary>
/// <remarks>
/// <para>The doors are enumerated by reflection and pinned to a LITERAL list of three, so a new
/// public method taking a <see cref="ProjectConfig"/> fails the pin until it gets a cell here.
/// The private single-file door (<c>CompilerApi.CompileAsSyntheticProject</c>) takes a string and
/// is covered by the CLI's <c>CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305</c>.</para>
/// <para>Cells: door × {missing reference, control without it}. The two compile doors must refuse
/// BEFORE the project pipeline runs — on this route it writes the assembly — so a missing cell
/// asserts both a null <see cref="ProjectCompilationResult.OutputAssemblyPath"/> and that nothing
/// was written under the project directory; a refusal merged after the pipeline fails only the
/// second. The control is the same project without the reference item: it compiles (or analyses)
/// with no SPY0305, and its assembly is on disk at the path the missing cell checks.</para>
/// </remarks>
public sealed class RegistryErrorDoorTests : IDisposable
{
    private const string CompileProject = "Compiler.CompileProject(ProjectConfig)";
    private const string CompileProjectWithToken = "Compiler.CompileProject(ProjectConfig, CancellationToken)";
    private const string AnalyzeProject = "CompilerApi.AnalyzeProject(ProjectConfig, CompilerOptions, CancellationToken)";

    private const string MissingItem =
        "<Reference Include=\"Absent\"><HintPath>libs/Absent.dll</HintPath></Reference>";

    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "spy-regdoor-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public void ProjectConfigDoors_AreExactlyTheThreeThisClassCovers()
    {
        var doors = new[] { typeof(global::Sharpy.Compiler.Compiler), typeof(CompilerApi) }
            .SelectMany(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ProjectConfig)))
                .Select(m => $"{type.Name}.{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})"))
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();

        doors.Should().HaveCount(3, "every public door taking a ProjectConfig needs a cell in this class");
        doors.Should().Equal(
            new[] { AnalyzeProject, CompileProject, CompileProjectWithToken }.OrderBy(d => d, StringComparer.Ordinal),
            "the doors are named literally, not re-derived from the enumeration under test");
    }

    [Theory]
    [InlineData(CompileProject, true)]
    [InlineData(CompileProject, false)]
    [InlineData(CompileProjectWithToken, true)]
    [InlineData(CompileProjectWithToken, false)]
    [InlineData(AnalyzeProject, true)]
    [InlineData(AnalyzeProject, false)]
    public void Door_ReportsAMissingReferenceAsSpy0305(string door, bool missingReference)
    {
        var dir = WriteProject(missingReference ? MissingItem : "");
        var config = ProjectFileParser.Load(Path.Combine(dir, "app.spyproj"));
        var missingPath = Path.Combine(dir, "libs", "Absent.dll");
        var defaultOutput = config.OutputAssemblyPath;
        var cell = $"[{door}, missing={missingReference}]";

        var (success, diagnostics, outputPath) = Run(door, config);
        var spy0305 = diagnostics.Where(d => d.Code == DiagnosticCodes.Semantic.AssemblyNotFound).ToList();

        if (missingReference)
        {
            success.Should().BeFalse($"{cell} a reference that resolves nowhere fails the door");
            spy0305.Should().ContainSingle($"{cell} one SPY0305 for the one missing reference");
            spy0305[0].Message.Should().Contain(missingPath, $"{cell} the diagnostic names the path");
            if (door != AnalyzeProject)
            {
                outputPath.Should().BeNull($"{cell} nothing was compiled");
                File.Exists(defaultOutput).Should().BeFalse($"{cell} the assembly is not written");
                Directory.EnumerateFiles(dir, "*.dll", SearchOption.AllDirectories).Should()
                    .BeEmpty($"{cell} the door refuses before the pipeline writes anything");
            }
        }
        else
        {
            spy0305.Should().BeEmpty($"{cell} control: no reference, no SPY0305");
            success.Should().BeTrue(
                $"{cell} control compiles\n" + string.Join("\n", diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}: {d.Message}")));
            if (door != AnalyzeProject)
            {
                outputPath.Should().Be(defaultOutput, $"{cell} control writes the default output the missing cell checks");
                File.Exists(defaultOutput).Should().BeTrue($"{cell} control: the assembly is on disk");
            }
        }
    }

    /// <summary>
    /// The analysis door reads a CACHED registry (keyed by reference path + mtime): a warm call must
    /// still report SPY0305, the reference appearing must clear it, and the reference vanishing
    /// again must bring it back — the cache must neither pin nor lose the diagnostic.
    /// </summary>
    [Fact]
    public void AnalyzeProject_CachedRegistry_ReportsWarm_AndReKeysWhenTheReferenceAppearsOrVanishes()
    {
        var dir = WriteProject(MissingItem);
        var config = ProjectFileParser.Load(Path.Combine(dir, "app.spyproj"));
        var missingPath = Path.Combine(dir, "libs", "Absent.dll");
        var api = new CompilerApi(NullLogger.Instance, DefaultReferences.Resolve());

        int Spy0305() => api.AnalyzeProject(config).Diagnostics.GetAll()
            .Count(d => d.Code == DiagnosticCodes.Semantic.AssemblyNotFound);

        Spy0305().Should().Be(1, "cold: the reference is missing");
        Spy0305().Should().Be(1, "warm: the cached registry still carries its SPY0305");

        EmitTinyAssembly(missingPath);
        Spy0305().Should().Be(0, "the reference appeared: the cache key changed and the registry was rebuilt");

        File.Delete(missingPath);
        Spy0305().Should().Be(1, "the reference vanished: the cache key changed back and SPY0305 returns");
    }

    private static (bool Success, IReadOnlyList<CompilerDiagnostic> Diagnostics, string? OutputPath) Run(
        string door, ProjectConfig config)
    {
        var defaults = DefaultReferences.Resolve();
        switch (door)
        {
            case CompileProject:
            case CompileProjectWithToken:
                {
                    // The CLI's route (`sharpyc project`): defaults into the config, the config's
                    // references into discovery through ForProject.
                    DefaultReferences.ApplyTo(config, defaults);
                    var options = CompilerOptionsFactory.ForProject(config, defaultReferences: defaults);
                    var compiler = new global::Sharpy.Compiler.Compiler(options, NullLogger.Instance);
                    var result = door == CompileProject
                        ? compiler.CompileProject(config)
                        : compiler.CompileProject(config, CancellationToken.None);
                    return (result.Success, result.Diagnostics.GetAll(), result.OutputAssemblyPath);
                }

            case AnalyzeProject:
                {
                    var result = new CompilerApi(NullLogger.Instance, defaults).AnalyzeProject(config);
                    return (result.Success, result.Diagnostics.GetAll(), null);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(door), door, null);
        }
    }

    /// <summary>
    /// The single-file analyze door (every public <c>CompilerApi.Analyze</c> overload runs one body,
    /// <c>AnalyzeCore</c>) builds its registry from a synthetic config through the same cached
    /// <c>GetOrBuildAnalysisContext</c> as <see cref="AnalyzeProject"/>, so it reports a missing
    /// reference the same way (#2223; the CLI's <c>emit diagnostics</c>/<c>emit hover</c> reach it).
    /// The control is the same analysis with the reference present.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SingleFileAnalyze_ReportsAMissingReferenceAsSpy0305(bool withPath, bool missingReference)
    {
        var dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var source = "def main() -> None:\n    print(\"hi\")\n";
        var entry = Path.Combine(dir, "main.spy");
        File.WriteAllText(entry, source);
        var reference = Path.Combine(dir, "libs", "Probe.dll");
        if (!missingReference)
            EmitTinyAssembly(reference);
        var options = CompilerOptionsFactory.ForCli(outputType: "library", references: new[] { reference });
        var cell = $"[Analyze withPath={withPath}, missing={missingReference}]";

        var api = new CompilerApi(NullLogger.Instance, DefaultReferences.Resolve());
        var result = withPath ? api.Analyze(source, entry, options) : api.Analyze(source, options);
        var spy0305 = result.Diagnostics.Where(d => d.Code == DiagnosticCodes.Semantic.AssemblyNotFound).ToList();

        if (missingReference)
        {
            result.Success.Should().BeFalse($"{cell} a reference that resolves nowhere fails the analysis");
            spy0305.Should().ContainSingle($"{cell}").Which.Message.Should().Contain(reference);
        }
        else
        {
            spy0305.Should().BeEmpty($"{cell} control: the reference exists");
            result.Success.Should().BeTrue(
                $"{cell} control\n" + string.Join("\n", result.Diagnostics.Where(d => d.IsError).Select(d => $"{d.Code}: {d.Message}")));
        }
    }

    private string WriteProject(string referenceItem)
    {
        var dir = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "main.spy"), "def main() -> None:\n    print(\"hi\")\n");
        File.WriteAllText(Path.Combine(dir, "app.spyproj"),
            "<Project>\n"
            + "  <PropertyGroup><RootNamespace>App</RootNamespace><OutputType>exe</OutputType>"
            + "<EntryPoint>main.spy</EntryPoint></PropertyGroup>\n"
            + "  <ItemGroup>\n"
            + "    <SpyFile Include=\"main.spy\" />\n"
            + $"    {referenceItem}\n"
            + "  </ItemGroup>\n"
            + "</Project>\n");
        return dir;
    }

    /// <summary>A uniquely named, loadable assembly, so its load collides with nothing in this process.</summary>
    internal static void EmitTinyAssembly(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var compilation = CSharpCompilation.Create(
            "RefProbe_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText("public static class RefProbe { public static int One() => 1; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
    }
}
