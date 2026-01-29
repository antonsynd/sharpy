using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for tagged union constructor inference (Some/Nothing/Ok/Err).
/// Verifies that the type checker correctly recognizes these constructors
/// when the expected type is known from context.
/// </summary>
public class ConstructorInferenceTests
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

    #region Some Inference

    [Fact]
    public void Some_WithTypedVariable_NoError()
    {
        var source = @"
x: int? = Some(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Some_InReturn_InfersFromReturnType()
    {
        var source = @"
def get_value() -> int?:
    return Some(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Some_WithStringOptional_NoError()
    {
        var source = @"
name: str? = Some(""hello"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Some_WithoutTypeContext_ReportsError()
    {
        var source = @"
x = Some(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Cannot infer type for 'Some()'"));
    }

    [Fact]
    public void Some_TypeMismatch_ReportsError()
    {
        var source = @"
x: str? = Some(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("not compatible"));
    }

    [Fact]
    public void Some_AssignedToNonOptional_FallsThrough()
    {
        // When expectedType is not OptionalType, Some should fall through to regular function call handling
        // and report "undefined identifier" since there's no user-defined Some function
        var source = @"
x: int = Some(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region Nothing Inference

    [Fact]
    public void Nothing_WithTypedVariable_NoError()
    {
        var source = @"
x: int? = Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Nothing_InReturn_InfersFromReturnType()
    {
        var source = @"
def get_nothing() -> int?:
    return Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Nothing_WithoutTypeContext_ReportsError()
    {
        var source = @"
x = Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Cannot infer type for 'Nothing'"));
    }

    [Fact]
    public void Nothing_AssignedToNonOptional_ReportsError()
    {
        var source = @"
x: int = Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Nothing") && e.Message.Contains("Optional"));
    }

    [Fact]
    public void Nothing_InAssignment_NoError()
    {
        var source = @"
def foo() -> None:
    x: int? = Some(42)
    x = Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Ok Inference

    [Fact]
    public void Ok_WithTypedVariable_NoError()
    {
        var source = @"
x: int !str = Ok(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Ok_InReturn_InfersFromReturnType()
    {
        var source = @"
def parse(s: str) -> int !str:
    return Ok(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Ok_WithoutTypeContext_ReportsError()
    {
        var source = @"
x = Ok(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Cannot infer type for 'Ok()'"));
    }

    #endregion

    #region Err Inference

    [Fact]
    public void Err_WithTypedVariable_NoError()
    {
        var source = @"
x: int !str = Err(""error message"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Err_InReturn_InfersFromReturnType()
    {
        var source = @"
def parse(s: str) -> int !str:
    return Err(""invalid input"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Err_WithoutTypeContext_ReportsError()
    {
        var source = @"
x = Err(""error"")
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().Contain(e => e.Message.Contains("Cannot infer type for 'Err()'"));
    }

    #endregion

    #region Default Parameters

    [Fact]
    public void Nothing_AsDefaultParameter_NoError()
    {
        var source = @"
def foo(x: int? = Nothing) -> None:
    pass
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Function Argument Inference

    [Fact]
    public void Some_AsFunctionArgument_InfersFromParameterType()
    {
        var source = @"
def process(opt: int?) -> None:
    pass

def main() -> None:
    process(Some(42))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Nothing_AsFunctionArgument_InfersFromParameterType()
    {
        var source = @"
def process(opt: int?) -> None:
    pass

def main() -> None:
    process(Nothing)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Ok_AsFunctionArgument_InfersFromParameterType()
    {
        var source = @"
def handle(result: int !str) -> None:
    pass

def main() -> None:
    handle(Ok(42))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Err_AsFunctionArgument_InfersFromParameterType()
    {
        var source = @"
def handle(result: int !str) -> None:
    pass

def main() -> None:
    handle(Err(""error""))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Some_AsKeywordArgument_InfersFromParameterType()
    {
        var source = @"
def process(opt: int?) -> None:
    pass

def main() -> None:
    process(opt=Some(42))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Some_AsSecondArgument_InfersFromParameterType()
    {
        var source = @"
def process(name: str, opt: int?) -> None:
    pass

def main() -> None:
    process(""test"", Some(42))
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion

    #region Mixed Usage

    [Fact]
    public void Some_And_Nothing_InSameFunction_NoError()
    {
        var source = @"
def maybe_double(x: int) -> int?:
    if x > 0:
        return Some(x * 2)
    return Nothing
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Ok_And_Err_InSameFunction_NoError()
    {
        var source = @"
def parse(s: str) -> int !str:
    if s == """":
        return Err(""empty string"")
    return Ok(42)
";
        var (module, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();
    }

    #endregion
}
