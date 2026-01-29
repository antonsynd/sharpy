using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for lambda parameter type inference from context (bidirectional type checking).
/// Verifies that lambda parameters can be inferred from expected function types.
/// </summary>
public class LambdaTypeInferenceTests
{
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

    #region Function Argument Context

    [Fact]
    public void Lambda_AsFunctionArgument_InfersParameterTypes()
    {
        var source = @"
def apply(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    result = apply(lambda n: n * 2, 5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Lambda_AsFunctionArgument_MultipleParams_InfersTypes()
    {
        var source = @"
def combine(f: (int, str) -> str, x: int, s: str) -> str:
    return f(x, s)

def main():
    result = combine(lambda n, s: s, 5, ""hello"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Lambda_AsFunctionArgument_ReturnsCorrectType()
    {
        var source = @"
def transform(f: (int) -> int, x: int) -> int:
    return f(x)

def main():
    result = transform(lambda n: n + 10, 42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Variable Declaration Context

    [Fact]
    public void Lambda_WithTypedDeclaration_InfersParameterTypes()
    {
        var source = @"
def main():
    f: (int) -> int = lambda x: x * 2
    result = f(5)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Lambda_WithTypedDeclaration_MultipleParams()
    {
        var source = @"
def main():
    f: (int, int) -> int = lambda a, b: a + b
    result = f(3, 4)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region No Context (Unknown Parameters)

    [Fact]
    public void Lambda_WithAutoType_HasUnknownParameters()
    {
        // Lambda without type context - parameters should be Unknown
        // This is acceptable when used with auto type (runtime inference)
        var source = @"
def main():
    f: auto = lambda x: x
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should not error - auto type accepts Unknown parameters
        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Return Value Context

    [Fact]
    public void Lambda_ReturnedFromFunction_InfersFromReturnType()
    {
        var source = @"
def make_doubler() -> (int) -> int:
    return lambda x: x * 2
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
