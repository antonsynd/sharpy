using System.Collections.Immutable;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for namespace and wrapper class generation in RoslynEmitter.
/// Verifies that the project-level namespace is emitted correctly and that
/// directory hierarchy is expressed as nested static partial wrapper classes.
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

    private string GenerateCode(RoslynEmitter emitter)
    {
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        var result = emitter.GenerateCompilationUnit(module);
        return result.ToFullString();
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
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        // Root __init__.spy: module class derives from project root dir name "Src"
        // No wrapper classes needed since the file is at the root
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
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        Assert.Contains("class Level1", code);
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
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        Assert.Contains("class Level1", code);
        Assert.Contains("class Level2", code);
    }

    [Fact]
    public void GenerateProjectNamespace_ThreeLevelInit_NoDuplication()
    {
        // Arrange
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: "/project/src/level1/level2/level3/__init__.spy"
        );

        // Act
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        Assert.Contains("class Level1", code);
        Assert.Contains("class Level2", code);
        Assert.Contains("class Level3", code);
        Assert.DoesNotContain("Level3.Level3", code);
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
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        Assert.Contains("class Level1", code);
        Assert.Contains("class Level2", code);
        Assert.Contains("class Module", code);
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
        var code = GenerateCode(emitter);

        // Assert
        Assert.Contains("namespace TestProject", code);
        Assert.Contains("class Mymodule", code);
    }
}
