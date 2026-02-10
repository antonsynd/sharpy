using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for super() validation in TypeChecker.
/// Validates that super() is only used in allowed contexts with correct rules.
/// </summary>
public class SuperValidationTests
{
    private (global::Sharpy.Compiler.Parser.Ast.Module, SymbolTable, SemanticInfo, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance
        semanticBinding.MaterializeInheritance();

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Valid Usage Tests

    [Fact]
    public void ValidSuperInitInConstructor()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ValidSuperMethodCallInOverride()
    {
        var source = @"
class Parent:
    @virtual
    def process(self):
        pass

class Child(Parent):
    @override
    def process(self):
        super().process()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ValidSuperDunderInDunderMethod()
    {
        var source = @"
class Parent:
    @override
    def __eq__(self, other: object) -> bool:
        return True

    @override
    def __hash__(self) -> int:
        return 0

class Child(Parent):
    @override
    def __eq__(self, other: object) -> bool:
        return super().__eq__(other)

    @override
    def __hash__(self) -> int:
        return 1
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void ValidCrossDunderWithOverride()
    {
        var source = @"
class Parent:
    def __lt__(self, other: object) -> bool:
        return False

class Child(Parent):
    @override
    def __le__(self, other: object) -> bool:
        return super().__lt__(other)
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    #endregion

    #region Invalid Usage Tests - Outside Class

    [Fact]
    public void RejectsSuperInFreeFunction()
    {
        var source = @"
def my_func():
    super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("cannot be used outside of a class"));
    }

    #endregion

    #region Invalid Usage Tests - No Parent Class

    [Fact]
    public void RejectsSuperInClassWithNoParent()
    {
        var source = @"
class MyClass:
    def __init__(self):
        super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("no parent class"));
    }

    #endregion

    #region Invalid Usage Tests - Regular Methods

    [Fact]
    public void RejectsSuperInRegularMethod()
    {
        var source = @"
class Parent:
    def process(self):
        pass

class Child(Parent):
    def process(self):
        super().process()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("only in __init__, @override, or dunder methods"));
    }

    #endregion

    #region Invalid Usage Tests - Field Access

    [Fact]
    public void RejectsSuperFieldAccess()
    {
        var source = @"
class Parent:
    x: int = 10

class Child(Parent):
    def __init__(self):
        value: int = super().x
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("only methods are allowed"));
    }

    #endregion

    #region Invalid Usage Tests - __init__ Rules

    [Fact]
    public void RejectsSuperInitInsideControlFlow()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        if True:
            super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("not inside control flow"));
    }

    [Fact]
    public void RejectsSuperNonInitInConstructor()
    {
        var source = @"
class Parent:
    def process(self):
        pass

class Child(Parent):
    def __init__(self):
        super().process()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("can only call super().__init__"));
    }

    [Fact]
    public void RejectsDuplicateSuperInitCall()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        super().__init__()
        super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("can only be called once"));
    }

    #endregion

    #region Invalid Usage Tests - @override Rules

    [Fact]
    public void RejectsSuperWrongMethodInOverride()
    {
        var source = @"
class Parent:
    @virtual
    def process(self):
        pass
    @virtual
    def other(self):
        pass

class Child(Parent):
    @override
    def process(self):
        super().other()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("must call super().process"));
    }

    #endregion

    #region Invalid Usage Tests - Dunder Method Rules

    [Fact]
    public void RejectsSuperNonDunderInDunderMethod()
    {
        var source = @"
class Parent:
    def process(self):
        pass

class Child(Parent):
    def __eq__(self, other: object) -> bool:
        super().process()
        return True
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("must call super().__eq__"));
    }

    #endregion

    #region Invalid Usage Tests - Standalone super()

    [Fact]
    public void RejectsStandaloneSuperExpression()
    {
        var source = @"
class Parent:
    pass

class Child(Parent):
    @override
    def __init__(self):
        x = super()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("must be followed by a method call"));
    }

    #endregion

    #region Invalid Usage Tests - Non-existent Methods

    [Fact]
    public void RejectsSuperCallToNonExistentMethod()
    {
        var source = @"
class Parent:
    pass

class Child(Parent):
    @override
    def process(self):
        super().process()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        // Error is now caught by override validation: "@override but no matching method exists in base class"
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("@override") && e.Message.Contains("no matching method"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllowsSuperInitInNestedIfAsControlFlow()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        if True:
            if False:
                super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have error because it's inside control flow (nested if)
        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("not inside control flow"));
    }

    [Fact]
    public void RejectsSuperInitInWhileLoop()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        while False:
            super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("not inside control flow"));
    }

    [Fact]
    public void RejectsSuperInitInForLoop()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        for i in [1, 2, 3]:
            super().__init__()
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("not inside control flow"));
    }

    [Fact]
    public void RejectsSuperInitInTryBlock()
    {
        var source = @"
class Parent:
    def __init__(self):
        pass

class Child(Parent):
    def __init__(self):
        try:
            super().__init__()
        except:
            pass
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotBeEmpty();
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("not inside control flow"));
    }

    #endregion
}
