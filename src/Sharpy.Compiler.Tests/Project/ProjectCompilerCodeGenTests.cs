using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

public class ProjectCompilerCodeGenTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public ProjectCompilerCodeGenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ProjectCompilationHelper CreateHelper()
    {
        _helper = new ProjectCompilationHelper(_output);
        return _helper;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    /// <summary>
    /// Regression: codegen must use the SymbolTable's BuiltinRegistry so IsBuiltinSymbol
    /// identity checks agree with the semantic phase. A separate instance causes
    /// user-defined functions shadowing builtins to be emitted as the builtin call (#1278).
    /// </summary>
    [Fact]
    public void ProjectCompile_BuiltinShadowing_UsesUserDefinedFunction()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("Test")
            .AddSourceFile("main.spy", """
def hash(x: int) -> int:
    return x * 42

def main():
    print(hash(3))
""")
            .CreateProjectFile();
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.Equal("126\n", result.StandardOutput);
    }

    /// <summary>
    /// Multi-file variant: imported builtin-shadowing function must also use the user's definition.
    /// </summary>
    [Fact]
    public void ProjectCompile_ImportedBuiltinShadow_UsesUserDefinedFunction()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("TestImported")
            .AddSourceFile("lib.spy", """
def hash(x: int) -> int:
    return x * 42
""")
            .AddSourceFile("main.spy", """
from lib import hash

def main():
    print(hash(3))
""")
            .CreateProjectFile();
        var result = helper.CompileAndExecute();
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
        Assert.Equal("126\n", result.StandardOutput);
    }

    /// <summary>
    /// #1278, stated as identity rather than as behaviour: every file's
    /// <see cref="CodeGenContext"/> must hold the very same <c>BuiltinRegistry</c> instance the
    /// semantic phase populated (<c>SymbolTable.BuiltinRegistry</c>), not a fresh one. Code
    /// generation asks that registry builtin-identity questions whose answers depend on state the
    /// semantic phase put there, so a second instance can disagree with the phase that decided
    /// what the code means. Captured through the compiler's own <see cref="ICodeEmitterFactory"/>
    /// seam, the same seam the reparse and re-entry conformance suites use.
    /// </summary>
    [Fact]
    public void ProjectCompile_EveryCodeGenContext_SharesTheSymbolTablesBuiltinRegistry()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("RegistryIdentity")
            .AddSourceFile("lib.spy", """
def double(x: int) -> int:
    return x * 2
""")
            .AddSourceFile("main.spy", """
from lib import double

def main():
    print(double(21))
""")
            .CreateProjectFile();

        var projectFile = Directory.GetFiles(helper.ProjectDirectory, "*.spyproj").Single();
        var config = ProjectFileParser.Load(projectFile);
        var capturing = new CapturingEmitterFactory();
        var result = new Compiler(new CompilerOptions(), NullLogger.Instance, capturing)
            .CompileProject(config);

        Assert.True(result.Success,
            string.Join("; ", result.Diagnostics.GetErrors().Select(d => d.Message)));

        // Without this the loop below would pass on an empty sequence.
        Assert.Equal(2, capturing.Contexts.Count);

        foreach (var context in capturing.Contexts)
        {
            Assert.Same(context.SymbolTable.BuiltinRegistry, context.Builtins);
        }

        // ...and it is one registry for the whole compilation, not one per file.
        Assert.Single(capturing.Contexts
            .Select(c => (object)c.Builtins)
            .Distinct(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    /// Passes each <see cref="CodeGenContext"/> through to the real emitter (so the compile is
    /// a genuine one) while retaining it for inspection.
    /// </summary>
    private sealed class CapturingEmitterFactory : ICodeEmitterFactory
    {
        private readonly RoslynEmitterFactory _inner = new();

        public List<CodeGenContext> Contexts { get; } = new();

        public ICodeEmitter Create(CodeGenContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);
            return _inner.Create(context, cancellationToken);
        }
    }
}
