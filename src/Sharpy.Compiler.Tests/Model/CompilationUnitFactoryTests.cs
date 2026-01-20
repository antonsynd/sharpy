using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class CompilationUnitFactoryTests
{
    #region ComputeModulePath Tests

    [Theory]
    [InlineData("/project/src/main.spy", "/project", "src.main")]
    [InlineData("/project/main.spy", "/project", "main")]
    [InlineData("/project/utils/helpers.spy", "/project", "utils.helpers")]
    [InlineData("/project/src/lib/math/vector.spy", "/project", "src.lib.math.vector")]
    public void ComputeModulePath_ReturnsCorrectPath(string filePath, string projectRoot, string expected)
    {
        var result = CompilationUnitFactory.ComputeModulePath(filePath, projectRoot);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeModulePath_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.ComputeModulePath(null!, "/project"));
    }

    [Fact]
    public void ComputeModulePath_NullProjectRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.ComputeModulePath("/project/main.spy", null!));
    }

    #endregion

    #region Lex Tests

    [Fact]
    public void Lex_ValidSource_SetsTokensAndPhase()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42");

        var result = CompilationUnitFactory.Lex(unit);

        Assert.True(result);
        Assert.NotNull(unit.Tokens);
        Assert.True(unit.Tokens.Count > 0);
        Assert.Equal(CompilationPhase.Lexed, unit.Phase);
    }

    [Fact]
    public void Lex_InvalidSource_AddsDiagnosticAndFails()
    {
        // Unterminated string literal causes a lexer error
        var unit = new CompilationUnit("/test/a.spy", "test.a", "\"unterminated string");

        var result = CompilationUnitFactory.Lex(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }

    [Fact]
    public void Lex_NullUnit_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.Lex(null!));
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_ValidTokens_SetsAstAndPhase()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42");
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.True(result);
        Assert.NotNull(unit.Ast);
        Assert.Equal(CompilationPhase.Parsed, unit.Phase);
    }

    [Fact]
    public void Parse_WithImports_ExtractsImportStatements()
    {
        var source = @"import math
from utils import helper
x = 1";
        var unit = new CompilationUnit("/test/a.spy", "test.a", source);
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.True(result);
        Assert.Single(unit.Imports);
        Assert.Single(unit.FromImports);
    }

    [Fact]
    public void Parse_WithoutTokens_Throws()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        // Don't call Lex()

        Assert.Throws<InvalidOperationException>(() =>
            CompilationUnitFactory.Parse(unit));
    }

    [Fact]
    public void Parse_InvalidSyntax_AddsDiagnosticAndFails()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "def foo( -> int:");
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }

    #endregion

    #region LexAndParse Tests

    [Fact]
    public void LexAndParse_ValidSource_Succeeds()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42\ny: str = \"hello\"");

        var result = CompilationUnitFactory.LexAndParse(unit);

        Assert.True(result);
        Assert.NotNull(unit.Tokens);
        Assert.NotNull(unit.Ast);
        Assert.Equal(CompilationPhase.Parsed, unit.Phase);
    }

    [Fact]
    public void LexAndParse_LexError_FailsEarly()
    {
        // Unterminated string causes a lexer error
        var unit = new CompilationUnit("/test/a.spy", "test.a", "\"unterminated");

        var result = CompilationUnitFactory.LexAndParse(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Null(unit.Ast);
    }

    #endregion

    #region SetDependencies Tests

    [Fact]
    public void SetDependencies_SetsDependencies()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        var deps = new[] { "/test/b.spy", "/test/c.spy" };

        CompilationUnitFactory.SetDependencies(unit, deps);

        Assert.Equal(2, unit.DirectDependencies.Count);
        Assert.Contains("/test/b.spy", unit.DirectDependencies);
        Assert.Contains("/test/c.spy", unit.DirectDependencies);
    }

    #endregion
}
