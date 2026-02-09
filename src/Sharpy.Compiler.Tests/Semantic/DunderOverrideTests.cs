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
    public void DunderStr_WithoutOverrideDecorator_Succeeds()
    {
        var source = @"
class Foo:
    def __str__(self) -> str:
        return ""Foo""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DunderEq_WithoutOverrideDecorator_Succeeds()
    {
        var source = @"
class Bar:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DunderHash_WithoutOverrideDecorator_Succeeds()
    {
        var source = @"
class Baz:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 42
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DunderEq_WithOverrideDecorator_Succeeds()
    {
        var source = @"
class BarGood:
    @override
    def __eq__(self, other: object) -> bool:
        return True

    @override
    def __hash__(self) -> int:
        return 0
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DunderHash_WithOverrideDecorator_Succeeds()
    {
        var source = @"
class BazGood:
    @override
    def __eq__(self, other: object) -> bool:
        return True

    @override
    def __hash__(self) -> int:
        return 42
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void MultipleDundersWithoutOverride_Succeeds()
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
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
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void DunderStr_InDerivedClass_WithoutOverrideDecorator_Succeeds()
    {
        // A derived class can define __str__ without @override even when base defines it
        var source = @"
class Base:
    @virtual
    def greet(self) -> str:
        return ""hello""

    def __str__(self) -> str:
        return ""Base""

class Derived(Base):
    def __str__(self) -> str:
        return ""Derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }
}
