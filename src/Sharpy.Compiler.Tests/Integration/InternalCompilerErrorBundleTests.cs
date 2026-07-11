using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Verifies the SPY0909 last-chance handler: a deliberately-thrown internal error during code
/// generation surfaces an <c>SPY0909</c> diagnostic and writes a minimal-repro crash bundle.
/// The crash is injected via a throwing <see cref="ICodeEmitterFactory"/> so no real compiler
/// bug is required to exercise the path.
/// </summary>
public sealed class InternalCompilerErrorBundleTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public InternalCompilerErrorBundleTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_ice_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void DeliberateCodegenCrash_SurfacesSpy0909_AndWritesBundle()
    {
        var spyPath = Path.Combine(_tempDir, "crash.spy");
        File.WriteAllText(spyPath, "x: int = 1\n");
        var outputPath = Path.Combine(_tempDir, "out.dll");

        var registry = new ModuleRegistry(NullLogger.Instance);
        registry.LoadReference(SharpyCoreReference.Location);

        var config = new ProjectConfig
        {
            ProjectDirectory = _tempDir,
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            RootNamespace = "Test",
            OutputType = "library",
            EntryPoint = "crash.spy",
            SourceFiles = new List<string> { spyPath },
            Configuration = "Debug",
            TargetFramework = "net10.0",
            OutputAssemblyPathOverride = outputPath
        };

        var compiler = new ProjectCompiler(
            NullLogger.Instance,
            moduleRegistry: registry,
            emitterFactory: new ThrowingEmitterFactory());

        var result = compiler.Compile(config);

        Assert.False(result.Success);

        var ice = result.Diagnostics.GetErrors()
            .FirstOrDefault(d => d.Code == DiagnosticCodes.Infrastructure.InternalCompilerError);
        Assert.NotNull(ice);
        Assert.Contains("internal compiler error", ice!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(CrashBundleWriter.CrashRootDirectoryName, ice.Message, StringComparison.Ordinal);

        var crashRoot = Path.Combine(_tempDir, CrashBundleWriter.CrashRootDirectoryName);
        Assert.True(Directory.Exists(crashRoot), "crash root directory should exist");
        var bundles = Directory.GetDirectories(crashRoot);
        Assert.NotEmpty(bundles);

        var report = File.ReadAllText(Path.Combine(bundles[0], "report.md"));
        _output.WriteLine(report);
        Assert.Contains("SPY0909", report, StringComparison.Ordinal);
        Assert.Contains("TestEmitter", report, StringComparison.Ordinal);
        Assert.Contains("deliberate crash", report, StringComparison.Ordinal);
        // The entry source is embedded verbatim.
        Assert.Contains("x: int = 1", File.ReadAllText(Path.Combine(bundles[0], "sources", "crash.spy")), StringComparison.Ordinal);
    }

    private sealed class ThrowingEmitterFactory : ICodeEmitterFactory
    {
        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
            => new ThrowingEmitter();
    }

    private sealed class ThrowingEmitter : ICodeEmitter
    {
        public CompilationUnitSyntax GenerateCompilationUnit(Module module)
            => throw new InternalCompilerErrorException(
                "TestEmitter", "deliberate crash for SPY0909 test", node: module);
    }
}
