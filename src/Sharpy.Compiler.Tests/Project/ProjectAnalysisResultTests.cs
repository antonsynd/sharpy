using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
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
