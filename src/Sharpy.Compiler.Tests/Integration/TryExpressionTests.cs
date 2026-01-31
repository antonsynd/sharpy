using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests for the 'try' expression, which wraps an expression in Result[T, E].
/// try expr → Result[T, Exception], try[E] expr → Result[T, E].
/// </summary>
public class TryExpressionTests : IntegrationTestBase
{
    public TryExpressionTests(ITestOutputHelper output) : base(output)
    {
    }

    private (Module, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, typeChecker);
    }

    #region Type Checking

    [Fact]
    public void Try_SimpleExpression_ProducesResultType()
    {
        var source = @"
def foo() -> None:
    x = try 42
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Try_StringExpression_ProducesResultType()
    {
        var source = @"
def foo() -> None:
    x = try ""hello""
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Try_FunctionCallExpression_ProducesResultType()
    {
        var source = @"
def compute(x: int) -> int:
    return x * 2

def foo() -> None:
    y = try compute(21)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Try_WithArithmetic_WrapsFullExpression()
    {
        var source = @"
def foo() -> None:
    x = try 6 * 7
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Generated C# Verification

    [Fact]
    public void Try_GeneratedCSharp_ContainsResultTry()
    {
        var source = @"
def main():
    x = try 42
    print(x.unwrap())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("Result.Try", result.GeneratedCSharp!);
    }

    [Fact]
    public void Try_GeneratedCSharp_UsesLambdaWrapper()
    {
        var source = @"
def main():
    x = try 42
    print(x.unwrap())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.NotNull(result.GeneratedCSharp);
        // Should wrap the operand in a lambda: () => 42
        Assert.Contains("() => 42", result.GeneratedCSharp!);
    }

    #endregion

    #region Code Generation - Success Cases

    [Fact]
    public void Try_NonThrowingExpression_ProducesOk()
    {
        var source = @"
def main():
    result = try 42
    print(result.unwrap())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("42", result.StandardOutput);
    }

    [Fact]
    public void Try_WithArithmetic_CompilesAndRuns()
    {
        var source = @"
def main():
    result = try 6 * 7
    print(result.unwrap())
";

        var result = CompileAndExecute(source);

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.CompilationErrors)}");
        Assert.Contains("42", result.StandardOutput);
    }

    #endregion
}
