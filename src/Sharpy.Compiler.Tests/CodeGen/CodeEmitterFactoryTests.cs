using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Verifies that the single-file compile facade drives code generation through the injected
/// <see cref="ICodeEmitterFactory"/> on the one unified <c>ProjectCompiler</c> path (#1038),
/// including the union of compilation-wide and per-file <c>from __future__ import</c> features.
/// </summary>
public class CodeEmitterFactoryTests
{
    private class MockCodeEmitter : ICodeEmitter
    {
        public bool WasCalled { get; private set; }

        public CompilationUnitSyntax GenerateCompilationUnit(Module module)
        {
            WasCalled = true;
            return CompilationUnit();
        }
    }

    private class MockCodeEmitterFactory : ICodeEmitterFactory
    {
        public MockCodeEmitter? LastCreatedEmitter { get; private set; }
        public CodeGenContext? LastContext { get; private set; }
        public int CreateCallCount { get; private set; }

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            LastContext = context;
            LastCreatedEmitter = new MockCodeEmitter();
            return LastCreatedEmitter;
        }
    }

    private const string ValidSource = "def f() -> int:\n    return 42\n";

    [Fact]
    public void Compile_UsesInjectedEmitterFactory()
    {
        var mockFactory = new MockCodeEmitterFactory();
        var options = new CompilerOptions { OutputType = "library" };
        var compiler = new Compiler(options, NullLogger.Instance, mockFactory);

        compiler.Compile(ValidSource, "test.spy");

        Assert.True(mockFactory.CreateCallCount >= 1);
        Assert.NotNull(mockFactory.LastCreatedEmitter);
        Assert.True(mockFactory.LastCreatedEmitter!.WasCalled);
    }

    [Fact]
    public void Compile_DefaultsToRoslynEmitterFactory()
    {
        var options = new CompilerOptions { OutputType = "library" };
        var compiler = new Compiler(options, NullLogger.Instance);

        var result = compiler.Compile(ValidSource, "test.spy");

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));
        Assert.NotNull(result.GeneratedCSharpCode);
        Assert.NotEmpty(result.GeneratedCSharpCode!);
    }

    [Fact]
    public void CodeGen_ThreadsUnionOfCompilationWideAndFutureFeaturesIntoContext()
    {
        var mockFactory = new MockCodeEmitterFactory();
        var tempDir = Directory.CreateTempSubdirectory("sharpy-features-").FullName;
        try
        {
            // A semantic-scoped feature enabled per-file via `from __future__ import`.
            var filePath = Path.Combine(tempDir, "features.spy");
            File.WriteAllText(filePath, "from __future__ import __test_feature\n");

            // A distinct compilation-wide feature; Enable does not validate names.
            var options = new CompilerOptions
            {
                OutputType = "library",
                Features = Sharpy.Compiler.Shared.FeatureFlags.None.Enable("compilation_wide")
            };
            var compiler = new Compiler(options, NullLogger.Instance, mockFactory);

            compiler.Compile(File.ReadAllText(filePath), filePath);

            Assert.NotNull(mockFactory.LastContext);
            var features = mockFactory.LastContext!.Features;
            // The context receives the union of both sources.
            Assert.True(features.IsEnabled("__test_feature"),
                "per-file `from __future__ import` feature should reach the codegen context");
            Assert.True(features.IsEnabled("compilation_wide"),
                "compilation-wide feature should reach the codegen context");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }
}
