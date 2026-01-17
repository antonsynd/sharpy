using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using System.Reflection;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for namespace generation in RoslynEmitter
/// </summary>
public class RoslynEmitterNamespaceTests
{
    private RoslynEmitter CreateEmitterWithProjectContext(
        string projectNamespace,
        string projectRootPath,
        string sourceFilePath)
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var context = new CodeGenContext(symbolTable, builtins)
        {
            ProjectNamespace = projectNamespace,
            ProjectRootPath = projectRootPath,
            SourceFilePath = sourceFilePath
        };
        return new RoslynEmitter(context);
    }

    private string GetGeneratedNamespace(RoslynEmitter emitter)
    {
        // Use reflection to call the private GenerateProjectNamespace method
        var method = typeof(RoslynEmitter).GetMethod("GenerateProjectNamespace",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var result = method?.Invoke(emitter, null);
        return result?.ToString() ?? "";
    }

    [Fact]
    public void GenerateProjectNamespace_RootInit_UsesOnlyProjectNamespace()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/__init__.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        Assert.Equal("TestProject", namespaceName);
    }

    [Fact]
    public void GenerateProjectNamespace_SingleLevelInit_IncludesDirectoryName()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/level1/__init__.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        Assert.Equal("TestProject.Level1", namespaceName);
    }

    [Fact]
    public void GenerateProjectNamespace_TwoLevelInit_IncludesBothDirectories()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/level1/level2/__init__.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        Assert.Equal("TestProject.Level1.Level2", namespaceName);
    }

    [Fact]
    public void GenerateProjectNamespace_ThreeLevelInit_NoDuplication()
    {
        // Arrange - This is the test case from the task description
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/level1/level2/level3/__init__.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        // Expected: TestProject.Level1.Level2.Level3
        // NOT:      TestProject.Level1.Level2.Level3.Level3 (with duplication)
        Assert.Equal("TestProject.Level1.Level2.Level3", namespaceName);
    }

    [Fact]
    public void GenerateProjectNamespace_RegularModuleInNestedPackage_IncludesFileName()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/level1/level2/module.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        Assert.Equal("TestProject.Level1.Level2.Module", namespaceName);
    }

    [Fact]
    public void GenerateProjectNamespace_RootLevelModule_IncludesFileName()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/mymodule.spy"
        );

        // Act
        var namespaceName = GetGeneratedNamespace(emitter);

        // Assert
        Assert.Equal("TestProject.Mymodule", namespaceName);
    }

}
