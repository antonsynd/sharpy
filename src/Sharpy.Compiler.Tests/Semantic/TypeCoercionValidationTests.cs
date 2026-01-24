using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for type coercion validation (the `to` operator).
/// Validates that invalid casts are rejected at compile time.
/// </summary>
public class TypeCoercionValidationTests
{
    private (Module, SymbolTable, SemanticInfo, TypeChecker, NameResolver) CompileAndCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker, nameResolver);
    }

    // ========== Invalid casts - should produce errors ==========

    [Fact]
    public void IntToStr_ProducesError()
    {
        var source = @"
x: int = 42
s = x to str
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot cast");
        typeChecker.Errors[0].Message.Should().Contain("int");
        typeChecker.Errors[0].Message.Should().Contain("str");
        typeChecker.Errors[0].Message.Should().Contain("Use str(...)");
    }

    [Fact]
    public void LongToStr_ProducesError()
    {
        var source = @"
x: long = 42
s = x to str
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot cast");
        typeChecker.Errors[0].Message.Should().Contain("str");
    }

    [Fact]
    public void FloatToStr_ProducesError()
    {
        var source = @"
x: float = 3.14
s = x to str
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot cast");
        typeChecker.Errors[0].Message.Should().Contain("str");
    }

    [Fact]
    public void BoolToStr_ProducesError()
    {
        var source = @"
x: bool = True
s = x to str
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot cast");
        typeChecker.Errors[0].Message.Should().Contain("str");
    }

    // ========== Valid numeric casts - should NOT produce errors ==========

    [Fact]
    public void LongToInt_IsValid()
    {
        var source = @"
x: long = 42
y = x to int
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IntToLong_IsValid()
    {
        var source = @"
x: int = 42
y = x to long
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FloatToInt_IsValid()
    {
        var source = @"
x: float = 3.14
y = x to int
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void IntToFloat_IsValid()
    {
        var source = @"
x: int = 42
y = x to float
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    // ========== Object unboxing - should be valid ==========

    [Fact]
    public void ObjectToInt_IsValid()
    {
        var source = @"
x: object = 42
y = x to int
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ObjectToStr_IsValid()
    {
        var source = @"
x: object = ""hello""
y = x to str
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    // ========== Class casting - inheritance relationships ==========

    [Fact]
    public void BaseToDerivied_Downcast_IsValid()
    {
        var source = @"
class Animal:
    pass

class Dog(Animal):
    pass

a: Animal = Dog()
d = a to Dog
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DerivedToBase_Upcast_IsValid()
    {
        var source = @"
class Animal:
    pass

class Dog(Animal):
    pass

d: Dog = Dog()
a = d to Animal
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }

    // ========== Nullable form - should also validate ==========

    [Fact]
    public void IntToNullableStr_ProducesError()
    {
        var source = @"
x: int = 42
s = x to str?
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot cast");
        typeChecker.Errors[0].Message.Should().Contain("str");
    }

    // ========== str(x) - the correct way - should work ==========

    [Fact]
    public void StrFunctionCall_IsValid()
    {
        var source = @"
x: int = 42
s = str(x)
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: true);

        typeChecker.Errors.Should().BeEmpty();
    }
}
