using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.Lowering;

/// <summary>
/// Compiles a fixture or an in-memory program through the production pipeline (single file:
/// <see cref="Compiler"/>; multi-file: <see cref="ProjectCompiler"/>) with an emitter factory that
/// captures each emitter's <see cref="CodeGenContext"/> and module, so a sweep can read the
/// lowering IR and the semantic facts the emitter saw. The shape of
/// <c>EmittedOptionalConstructionConformanceTests.CompileFixtureCapturing</c>, whose factory is
/// private to that class.
/// </summary>
internal static class LoweredCompilationCapture
{
    internal sealed record Result(
        DiagnosticBag Diagnostics, IReadOnlyList<(CodeGenContext Context, Module Module)> Units)
    {
        /// <summary>No Sharpy error other than a refusal of a complete emitter unit (C# stage, parse
        /// net, precedence net) and no internal compiler error.</summary>
        public bool FrontEndClean => !Diagnostics.GetErrors().Any(d =>
            d.Code is not (DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                or DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError
                or DiagnosticCodes.CodeGen.EmittedTreePrecedenceInversion));

        /// <summary>The SPY0909 internal-compiler-error diagnostics, if any.</summary>
        public IReadOnlyList<string> InternalCompilerErrors => Diagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError)
            .Select(d => d.Message)
            .ToList();
    }

    internal static IEnumerable<string> SourcesOf(TestFixtureInfo fixture) => fixture.IsMultiFile
        ? Directory.GetFiles(fixture.SpyFilePath, "*.spy", SearchOption.AllDirectories)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(Path.GetRelativePath(fixture.SpyFilePath, f)))
            .OrderBy(f => f, StringComparer.Ordinal)
        : new[] { fixture.SpyFilePath };

    internal static Result Compile(TestFixtureInfo fixture)
    {
        var features = fixture.Features.Count == 0
            ? FeatureFlags.None
            : FeatureFlags.None.Enable(fixture.Features);
        if (!fixture.IsMultiFile)
            return Compile(File.ReadAllText(fixture.SpyFilePath), fixture.SpyFilePath, features);

        var projectDir = fixture.SpyFilePath;
        var sourceFiles = SourcesOf(fixture).ToList();
        var entry = File.Exists(Path.Combine(projectDir, "main.spy")) ? "main.spy"
            : File.Exists(Path.Combine(projectDir, Path.GetFileName(projectDir) + ".spy"))
                ? Path.GetFileName(projectDir) + ".spy"
                : Path.GetFileName(sourceFiles[0]);
        return CompileProject(projectDir, sourceFiles, entry, features);
    }

    /// <summary>
    /// Compiles <paramref name="source"/> as a one-file project (<c>main.spy</c> in a fresh temporary
    /// directory) through <see cref="ProjectCompiler"/>, so an exception that escapes code generation
    /// surfaces as its SPY0909 diagnostic rather than through the single-file facade.
    /// </summary>
    internal static Result CompileProgram(string source)
    {
        var directory = Path.Combine(Path.GetTempPath(), "sharpy-ircov-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var mainPath = Path.Combine(directory, "main.spy");
            File.WriteAllText(mainPath, source);
            return CompileProject(directory, new List<string> { mainPath }, "main.spy", FeatureFlags.None);
        }
        finally
        {
            try
            { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
        }
    }

    /// <summary>Compiles <paramref name="source"/> in memory under the label <paramref name="filePath"/>.</summary>
    internal static Result Compile(string source, string filePath, FeatureFlags? features = null)
    {
        var factory = new CapturingEmitterFactory();
        var options = new CompilerOptions
        {
            References = new[] { SharpyCoreReference.Location },
            OutputType = "exe",
            WarningsAsErrors = false,
            Features = features ?? FeatureFlags.None
        };
        var diagnostics = new Compiler(options, NullLogger.Instance, factory).Compile(source, filePath).Diagnostics;
        return new Result(diagnostics, factory.Captured);
    }

    private static Result CompileProject(string projectDir, List<string> sourceFiles, string entry, FeatureFlags features)
    {
        var factory = new CapturingEmitterFactory();
        var config = new ProjectConfig
        {
            ProjectDirectory = projectDir,
            ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
            RootNamespace = "Sharpy.Test",
            OutputType = "exe",
            EntryPoint = entry,
            SourceFiles = sourceFiles,
            Configuration = "Debug",
            TargetFramework = "net10.0"
        };

        var moduleRegistry = new ModuleRegistry(NullLogger.Instance);
        moduleRegistry.LoadReference(SharpyCoreReference.Location);
        var diagnostics = new ProjectCompiler(
                NullLogger.Instance, moduleRegistry,
                ProjectCompilerOptions.Default with { Features = features }, factory)
            .Compile(config, CancellationToken.None, emitAssembly: false).Diagnostics;
        return new Result(diagnostics, factory.Captured);
    }

    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<(CodeGenContext, Module)> Captured { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default) =>
            new CapturingEmitter(_inner.Create(context, cancellationToken), context, Captured);

        private sealed class CapturingEmitter : ICodeEmitter
        {
            private readonly ICodeEmitter _inner;
            private readonly CodeGenContext _context;
            private readonly List<(CodeGenContext, Module)> _sink;

            public CapturingEmitter(ICodeEmitter inner, CodeGenContext context, List<(CodeGenContext, Module)> sink)
            {
                _inner = inner;
                _context = context;
                _sink = sink;
            }

            public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            {
                _sink.Add((_context, module));
                return _inner.GenerateCompilationUnit(module);
            }
        }
    }
}
