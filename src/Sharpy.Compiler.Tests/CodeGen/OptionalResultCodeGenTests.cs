using Xunit;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for code generation of Optional and Result types,
/// including Some/Nothing/Ok/Err constructor expressions.
/// </summary>
public class OptionalResultCodeGenTests
{
    private string CompileToCSharp(string sharpySource, bool isEntryPoint = false)
    {
        var logger = NullLogger.Instance;
        var lexer = new Sharpy.Compiler.Lexer.Lexer(sharpySource, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, logger);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
        typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint);

        typeChecker.Errors.Should().BeEmpty("Sharpy source should have no type errors");

        var context = new CodeGenContext(symbolTable, builtinRegistry)
        {
            IsEntryPoint = isEntryPoint,
            SemanticInfo = semanticInfo
        };
        var emitter = new RoslynEmitter(context);
        var compilationUnit = emitter.GenerateCompilationUnit(module);

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }

    #region Type Mapping

    [Fact]
    public void TypeMapping_OptionalInt_GeneratesOptionalGeneric()
    {
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
    }

    [Fact]
    public void TypeMapping_OptionalStr_GeneratesOptionalGeneric()
    {
        var code = @"
name: str? = Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<string>");
    }

    [Fact]
    public void TypeMapping_ResultType_GeneratesResultGeneric()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>");
    }

    #endregion

    #region Constructor Generation

    [Fact]
    public void Constructor_Some_GeneratesOptionalSome()
    {
        var code = @"
x: int? = Some(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Some(42)");
    }

    [Fact]
    public void Constructor_Nothing_GeneratesOptionalNothing()
    {
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Nothing");
    }

    [Fact]
    public void Constructor_Ok_GeneratesResultOk()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Ok(42)");
    }

    [Fact]
    public void Constructor_Err_GeneratesResultErr()
    {
        var code = @"
x: int !str = Err(""error"")
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Err(");
    }

    #endregion

    #region Function Return Types

    [Fact]
    public void Function_OptionalReturn_GeneratesCorrectSignature()
    {
        var code = @"
def get_value() -> int?:
    return Some(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
        csharp.Should().Contain("GetValue()");
    }

    [Fact]
    public void Function_ResultReturn_GeneratesCorrectSignature()
    {
        var code = @"
def parse(s: str) -> int !str:
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>");
        csharp.Should().Contain("Parse(");
    }

    [Fact]
    public void Function_OptionalReturn_SomeAndNothing_GeneratesBoth()
    {
        var code = @"
def maybe_double(x: int) -> int?:
    if x > 0:
        return Some(x * 2)
    return Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Some(");
        csharp.Should().Contain("Optional<int>.Nothing");
    }

    [Fact]
    public void Function_ResultReturn_OkAndErr_GeneratesBoth()
    {
        var code = @"
def parse(s: str) -> int !str:
    if s == """":
        return Err(""empty string"")
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Ok(42)");
        csharp.Should().Contain("Result<int, string>.Err(");
    }

    #endregion

    #region Default Parameters

    [Fact]
    public void DefaultParam_Nothing_GeneratesOptionalNothing()
    {
        var code = @"
def foo(x: int? = Nothing) -> None:
    pass
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Nothing");
    }

    #endregion
}
