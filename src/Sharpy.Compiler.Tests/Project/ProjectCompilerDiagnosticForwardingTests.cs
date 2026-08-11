using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Text;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Covers how name-resolution diagnostics cross the per-file → project boundary (#1280, #1077).
/// The forwarding used to rebuild each diagnostic from its message, line, column, and code,
/// which silently dropped everything else the record carries — its <see cref="TextSpan"/> above
/// all — and only errors were forwarded at all, so a name-resolution warning would have vanished.
/// </summary>
public class ProjectCompilerDiagnosticForwardingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public ProjectCompilerDiagnosticForwardingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    /// <summary>
    /// End-to-end: an inheritance-phase error (raised on the shared NameResolver in phase 4b)
    /// reaches the project diagnostic bag with the span the resolver stamped, and with the file it
    /// was declared in. Reconstructing the diagnostic from line/column loses the span, and with it
    /// the squiggle range every span-consuming surface (LSP, the renderer's caret) draws from.
    ///
    /// <para>The file half is #1369. Phase 4b runs on ONE aggregate resolver holding every file's
    /// definitions, and <c>AggregateTypeDefinitionsFrom</c> used to copy <c>(Def, ModulePath)</c>
    /// out of the per-file resolvers while dropping their file paths — so the aggregate had no
    /// identity to stamp and every Phase-4b inheritance diagnostic arrived with a null
    /// <c>FilePath</c>. A line and column with no file cannot be opened, and the renderer cannot
    /// group it.</para>
    ///
    /// <para>Both files declare a failing class deliberately: with only one, "attributed to
    /// main.spy" is also what a resolver that stamped a single global path would produce. Two
    /// errors that must carry two DIFFERENT files can only be satisfied per definition.</para>
    /// </summary>
    [Fact]
    public void ProjectCompile_InheritanceErrors_KeepTheResolverStampedSpanAndTheirOwnFile()
    {
        _helper = new ProjectCompilationHelper(_output);
        _helper.WithRootNamespace("InheritanceSpan")
            .AddSourceFile("lib.spy", """
class LibDerived(NoSuchLibBase):
    pass

def helper() -> int:
    return 1
""")
            .AddSourceFile("main.spy", """
class Derived(NoSuchBase):
    pass

def main():
    print(1)
""")
            .CreateProjectFile();

        var result = _helper.Compile();

        Assert.False(result.Success, "an unresolved base type is an error");

        var diagnostics = result.Diagnostics.GetAll();
        AssertInheritanceErrorAttributed(diagnostics, "Base type 'NoSuchBase' not found", "main.spy");
        AssertInheritanceErrorAttributed(diagnostics, "Base type 'NoSuchLibBase' not found", "lib.spy");
    }

    /// <summary>
    /// The per-diagnostic assertions of the test above. The <c>NotEmpty</c> check is the load
    /// bearing one for everything after it: <c>Assert.All</c> over an empty set passes, so an
    /// attribution assertion that never met a diagnostic would report a green fix for a
    /// diagnostic that stopped being raised at all.
    /// </summary>
    private void AssertInheritanceErrorAttributed(
        IEnumerable<CompilerDiagnostic> diagnostics, string message, string expectedFileName)
    {
        var errors = diagnostics.Where(d => d.Message.Contains(message)).ToList();

        Assert.True(errors.Count > 0,
            $"no diagnostic said \"{message}\" — the arrangement stopped producing the error this "
            + "test attributes, so every assertion below it would have passed vacuously");
        Assert.All(errors, d =>
        {
            Assert.True(d.Span.HasValue,
                $"the name resolver stamps a span on this diagnostic; forwarding must preserve it: {d}");
            Assert.Equal(CompilerPhase.NameResolution, d.Phase);
            Assert.True(!string.IsNullOrEmpty(d.FilePath),
                $"a Phase-4b inheritance error carries the file it was declared in (#1369): {d}");
            Assert.Equal(expectedFileName, Path.GetFileName(d.FilePath));
            Assert.Equal(
                Path.GetFullPath(Path.Combine(_helper!.SourceDirectory, expectedFileName)),
                Path.GetFullPath(d.FilePath!));
        });
    }

    /// <summary>
    /// The forwarding helper itself: it must forward EVERY diagnostic, not just errors, and hand
    /// each one over as the record it received — same span, same severity, same phase, same
    /// file path. Driven directly because no name-resolution pass emits a warning today: the
    /// wholesale forwarding is what makes the first one that does actually arrive.
    /// </summary>
    [Fact]
    public void ForwardDiagnostics_ForwardsWarningsToBothBags_WithSpanIntact()
    {
        var compiler = new Sharpy.Compiler.Project.ProjectCompiler(NullLogger.Instance);
        var model = new ProjectModel(new ProjectConfig { RootNamespace = "ForwardingTest" });
        var compilerBag = InstallProjectModel(compiler, model);

        var warning = new CompilerDiagnostic(
            "a name-resolution warning",
            CompilerDiagnosticSeverity.Warning,
            Line: 7,
            Column: 3,
            FilePath: "/project/main.spy",
            Code: "SPY0451",
            Phase: CompilerPhase.NameResolution,
            Span: new TextSpan(42, 9));
        var error = new CompilerDiagnostic(
            "a name-resolution error",
            CompilerDiagnosticSeverity.Error,
            Line: 9,
            Column: 1,
            FilePath: "/project/main.spy",
            Code: "SPY0201",
            Phase: CompilerPhase.NameResolution,
            Span: new TextSpan(60, 4));

        Invoke(compiler, "ForwardDiagnostics", new List<CompilerDiagnostic> { warning, error });

        foreach (var (bagName, bag) in new[]
                 {
                     ("project bag", model.GlobalDiagnostics),
                     ("compilation bag", compilerBag)
                 })
        {
            var forwardedWarning = Assert.Single(bag.GetAll(), d => d.IsWarning);
            Assert.Equal(warning, forwardedWarning);
            Assert.True(forwardedWarning.Span.HasValue, $"{bagName} lost the span");
            Assert.Equal(new TextSpan(42, 9), forwardedWarning.Span!.Value);
            Assert.Equal(7, forwardedWarning.Line);
            Assert.Equal(3, forwardedWarning.Column);
            Assert.Equal("/project/main.spy", forwardedWarning.FilePath);
            Assert.Equal(CompilerPhase.NameResolution, forwardedWarning.Phase);

            var forwardedError = Assert.Single(bag.GetAll(), d => d.IsError);
            Assert.Equal(error, forwardedError);
        }
    }

    /// <summary>
    /// Points the compiler's <c>_projectModel</c> at <paramref name="model"/> and returns its
    /// per-compilation <c>_diagnostics</c> bag, so the forwarding helper can be driven without
    /// running a compilation.
    /// </summary>
    private static DiagnosticBag InstallProjectModel(
        Sharpy.Compiler.Project.ProjectCompiler compiler, ProjectModel model)
    {
        var modelField = Field(compiler, "_projectModel");
        modelField.SetValue(compiler, model);
        return (DiagnosticBag)Field(compiler, "_diagnostics").GetValue(compiler)!;
    }

    private static FieldInfo Field(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(field != null,
            $"ProjectCompiler.{name} was renamed or removed — this test drives it directly");
        return field!;
    }

    private static void Invoke(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(method != null,
            $"ProjectCompiler.{methodName} was renamed or removed — this test drives it directly");
        method!.Invoke(instance, arguments);
    }
}
