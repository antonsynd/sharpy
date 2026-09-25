using System.Collections.Immutable;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lowering;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for namespace generation in RoslynEmitter: the project-level namespace and the directory
/// hierarchy are one C# namespace (#1948).
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
            Ir = IrCompilation.Empty,
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

    /// <summary>
    /// #1948: the project namespace followed by every directory above the file is the C# namespace,
    /// and the module class is its ONLY class — no directory is a wrapper class. A package's
    /// <c>__init__.spy</c> emits <c>&lt;Dir&gt;Module</c> inside its own directory's namespace.
    /// </summary>
    [Theory]
    [InlineData("/project/src/__init__.spy", "TestProject", "SrcModule")]
    [InlineData("/project/src/level1/__init__.spy", "TestProject.Level1", "Level1Module")]
    [InlineData("/project/src/level1/level2/__init__.spy", "TestProject.Level1.Level2", "Level2Module")]
    [InlineData("/project/src/level1/level2/level3/__init__.spy", "TestProject.Level1.Level2.Level3", "Level3Module")]
    [InlineData("/project/src/level1/level2/module.spy", "TestProject.Level1.Level2", "Module")]
    [InlineData("/project/src/mymodule.spy", "TestProject", "Mymodule")]
    public void GenerateProjectNamespace_DirectoriesAreNamespaceSegments(
        string sourceFilePath, string expectedNamespace, string expectedClass)
    {
        var emitter = CreateEmitterWithProjectContext(
            projectNamespace: "TestProject",
            projectRootPath: "/project/src",
            sourceFilePath: sourceFilePath);

        var root = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(GenerateCode(emitter)).GetRoot();
        var namespaces = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .ToList();
        var classes = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Select(c => c.Identifier.Text)
            .ToList();

        Assert.Equal(new[] { expectedNamespace }, namespaces);
        Assert.Equal(new[] { expectedClass }, classes);
    }
}
