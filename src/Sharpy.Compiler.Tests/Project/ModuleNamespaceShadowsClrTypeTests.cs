using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2039 (P14c): every module is a C# namespace — the root namespace, its directories, its stem. When
/// that namespace is also the full name of a .NET type the module uses, C# resolves the name to the
/// namespace declared in the compiled source, and no spelling reaches the type (short of an extern
/// alias): <c>foo.spy</c> in RootNamespace <c>App</c> importing <c>App.Foo</c> from a referenced
/// assembly. Refused by name, SPY0615, never CS0234 behind SPY0908. The control is the same import
/// from a module whose stem differs (<c>bar.spy</c> is <c>App.Bar</c>): it runs. The Sharpy-declared
/// twin of the shape (a root <c>__init__.spy</c> type <c>A</c> beside a module <c>a.spy</c>) is
/// SPY0526 refusal 2, not this check.
/// </summary>
[Collection("HeavyCompilation")]
public class ModuleNamespaceShadowsClrTypeTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_clrshadow_{Guid.NewGuid():N}");

    public ModuleNamespaceShadowsClrTypeTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>A referenced assembly declaring the .NET type <c>App.Foo</c>.</summary>
    private string BuildReferencedAssembly()
    {
        var path = Path.Combine(_dir, "AppTypes.dll");
        var compilation = CSharpCompilation.Create(
            "AppTypes",
            new[] { CSharpSyntaxTree.ParseText("namespace App { public class Foo { public static int Value() => 4; } }") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        return path;
    }

    [Theory]
    [InlineData("foo.spy", true)]
    [InlineData("bar.spy", false)]
    public void ModuleNamedLikeAnImportedClrType_IsRefusedByName(string module, bool refused)
    {
        var dll = BuildReferencedAssembly();
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("App").WithEntryPoint("main.spy").WithAssemblyReference(dll);
        helper.AddSourceFile(module, "from app import Foo\n\ndef value() -> int:\n    return Foo.value()\n");
        var stem = Path.GetFileNameWithoutExtension(module);
        helper.AddSourceFile("main.spy", $"from {stem} import value\n\ndef main() -> None:\n    print(value())\n");
        helper.CreateProjectFile();

        var exec = helper.CompileAndExecute();
        var errors = helper.LastCompilationResult!.Diagnostics.GetErrors().ToList();
        errors.Should().NotContain(e => e.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            "never CS0234 behind SPY0908");

        if (refused)
        {
            exec.Success.Should().BeFalse();
            errors.Should().Contain(e => e.Code == DiagnosticCodes.SemanticOverflow.ModuleNamespaceShadowsClrType
                && e.Message.Contains("'App.Foo'", StringComparison.Ordinal)
                && e.Line == 1 && e.Column == 1,
                string.Join("\n", errors.Select(e => $"{e.Code} {e.Line}:{e.Column} {e.Message}")));
        }
        else
        {
            exec.Success.Should().BeTrue(string.Join("\n", errors.Select(e => $"{e.Code} {e.Message}")) + exec.Exception);
            exec.StandardOutput.Trim().Should().Be("4");
        }
    }
}
