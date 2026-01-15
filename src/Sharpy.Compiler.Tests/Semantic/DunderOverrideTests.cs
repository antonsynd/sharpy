using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

public class DunderOverrideTests
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

    [Fact]
    public void DunderStr_WithoutOverrideDecorator_ReportsError()
    {
        var source = @"
class Foo:
    def __str__(self) -> str:
        return ""Foo""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("__str__");
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("System.Object");
    }

    [Fact]
    public void DunderEq_WithoutOverrideDecorator_ReportsError()
    {
        var source = @"
class Bar:
    def __eq__(self, other: object) -> bool:
        return True
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("__eq__");
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("System.Object");
    }

    [Fact]
    public void DunderHash_WithoutOverrideDecorator_ReportsError()
    {
        var source = @"
class Baz:
    def __hash__(self) -> int:
        return 42
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("__hash__");
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("System.Object");
    }

    [Fact]
    public void DunderStr_WithOverrideDecorator_Succeeds()
    {
        var source = @"
class FooGood:
    @override
    def __str__(self) -> str:
        return ""FooGood""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DunderEq_WithOverrideDecorator_Succeeds()
    {
        var source = @"
class BarGood:
    @override
    def __eq__(self, other: object) -> bool:
        return True
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DunderHash_WithOverrideDecorator_Succeeds()
    {
        var source = @"
class BazGood:
    @override
    def __hash__(self) -> int:
        return 42
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DunderLen_WithoutOverrideDecorator_Succeeds()
    {
        // __len__ does NOT override Object method, no @override needed
        var source = @"
class MyList:
    def __len__(self) -> int:
        return 0
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DunderRepr_WithoutOverrideDecorator_Succeeds()
    {
        // __repr__ generates __Repr__(), not ToString() override
        var source = @"
class MyClass:
    def __repr__(self) -> str:
        return ""MyClass()""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DunderAdd_WithoutOverrideDecorator_Succeeds()
    {
        // Operator dunders don't override Object methods
        var source = @"
class MyNum:
    def __add__(self, other: MyNum) -> MyNum:
        return MyNum()
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MultipleDundersWithoutOverride_ReportsMultipleErrors()
    {
        var source = @"
class MultiDunder:
    def __str__(self) -> str:
        return ""MultiDunder""

    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 42
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().HaveCount(3);
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("__str__"));
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("__eq__"));
        typeChecker.Errors.Should().Contain(e => e.Message.Contains("__hash__"));
    }

    [Fact]
    public void TopLevelDunderFunction_DoesNotRequireOverride()
    {
        // Top-level functions aren't in a class, so they don't override anything
        var source = @"
def __str__() -> str:
    return ""top level""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }
}
