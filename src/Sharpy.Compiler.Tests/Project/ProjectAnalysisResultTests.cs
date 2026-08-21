using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class ProjectAnalysisResultTests
{
    private static ProjectConfig CreateConfig() => new()
    {
        ProjectFilePath = "/test/project.spyproj",
        ProjectDirectory = "/test",
        RootNamespace = "Test"
    };

    private static ProjectModel CreateModelWithFile(string filePath, string source, CompilationPhase phase)
    {
        var model = new ProjectModel(CreateConfig());
        var unit = model.CreateUnit(filePath, "test", source);
        unit.Phase = phase;
        return model;
    }

    [Fact]
    public void GetFileResult_ExistingFile_ReturnsResult()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.True(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_UnknownFile_ReturnsNull()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/unknown.spy");

        Assert.Null(fileResult);
    }

    [Fact]
    public void Dependencies_WhenDependencyGraphProvided_ReturnsQuery()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();

        var deps = new Dictionary<string, ImmutableHashSet<string>>
        {
            ["/test/main.spy"] = ImmutableHashSet<string>.Empty
        };
        var graph = new DependencyGraph(deps);

        var result = new ProjectAnalysisResult(true, model, graph, diagnostics);

        Assert.NotNull(result.Dependencies);
        Assert.Contains(result.Dependencies.AllFiles, f => f.Contains("main.spy"));
    }

    [Fact]
    public void Dependencies_WhenNoDependencyGraph_ReturnsNull()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        Assert.Null(result.Dependencies);
    }

    [Fact]
    public void GetFileResult_FileNotTypeChecked_ReturnsNotSuccess()
    {
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.Parsed);
        var diagnostics = new DiagnosticBag();
        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_TypeCheckedWithAccumulatedErrors_ReportsNotSuccess()
    {
        // Since #1360 a parse-errored unit can still advance to TypeChecked when the type
        // checker itself adds nothing, so Success must consult the unit's accumulated
        // diagnostics, not the phase alone.
        var model = CreateModelWithFile("/test/main.spy", "print(1)", CompilationPhase.TypeChecked);
        model.GetUnit("/test/main.spy")!.Diagnostics.AddError("parse error survived to TypeChecked", 1, 1);
        var result = new ProjectAnalysisResult(true, model, null, new DiagnosticBag());

        var fileResult = result.GetFileResult("/test/main.spy");

        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
    }

    [Fact]
    public void GetFileResult_ParseErroredFileThatTypechecksCleanly_FailsWithArtifactsPresent()
    {
        // End-to-end positive control for the phase-vs-diagnostics divergence: the trailing
        // dot is a parse error (SPY0101) the type checker adds nothing to, so the unit
        // reaches TypeChecked while carrying an error — Success must be false, and the
        // partial-analysis artifacts the LSP serves completion from must still be present.
        const string brokenSource =
            "class Greeter:\n"
            + "    name: str = \"world\"\n"
            + "\n"
            + "def use() -> None:\n"
            + "    g: Greeter = Greeter()\n"
            + "    g.\n";

        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("ParseErrDelta")
            .WithOutputType("exe")
            .WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(\"ok\")\n");
        helper.AddSourceFile("broken.spy", brokenSource);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "ParseErrDelta.spyproj"));
        var result = new CompilerApi().AnalyzeProject(config);

        var brokenPath = config.SourceFiles.Single(f => f.EndsWith("broken.spy"));
        var unit = result.ProjectModel.GetUnit(brokenPath);
        Assert.NotNull(unit);
        Assert.Equal(CompilationPhase.TypeChecked, unit.Phase);
        Assert.Contains(unit.Diagnostics.GetAll(), d => d.Code == DiagnosticCodes.Parser.ExpectedIdentifier);

        var fileResult = result.GetFileResult(brokenPath);
        Assert.NotNull(fileResult);
        Assert.False(fileResult.Success);
        Assert.Contains(fileResult.Diagnostics, d => d.Code == DiagnosticCodes.Parser.ExpectedIdentifier);
        Assert.NotNull(fileResult.Ast);
        Assert.NotNull(fileResult.SymbolTable);
    }

    [Fact]
    public void Success_ReflectsConstructorArgument()
    {
        var model = new ProjectModel(CreateConfig());
        var diagnostics = new DiagnosticBag();

        var successResult = new ProjectAnalysisResult(true, model, null, diagnostics);
        var failResult = new ProjectAnalysisResult(false, model, null, diagnostics);

        Assert.True(successResult.Success);
        Assert.False(failResult.Success);
    }

    [Fact]
    public void Diagnostics_ReturnsBagFromConstructor()
    {
        var model = new ProjectModel(CreateConfig());
        var diagnostics = new DiagnosticBag();
        diagnostics.AddError("test error", 1, 1);

        var result = new ProjectAnalysisResult(true, model, null, diagnostics);

        Assert.Same(diagnostics, result.Diagnostics);
        Assert.True(result.Diagnostics.HasErrors);
    }
}
