using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for @override and @virtual validation.
/// Validates that @override can only be used when base method is virtual/abstract/override.
/// </summary>
public class OverrideValidationTests
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

    // ========== Invalid @override usage - should produce errors ==========

    [Fact]
    public void Override_WithoutVirtualBase_ProducesError()
    {
        var source = @"
class Base:
    def method(self) -> str:
        return ""base""

class Derived(Base):
    @override
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("Cannot override");
        typeChecker.Errors[0].Message.Should().Contain("method");
        typeChecker.Errors[0].Message.Should().Contain("@virtual");
    }

    [Fact]
    public void Override_NoMatchingBaseMethod_ProducesError()
    {
        var source = @"
class Base:
    pass

class Derived(Base):
    @override
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("method");
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("no matching method");
    }

    [Fact]
    public void Override_NoBaseClass_ProducesError()
    {
        var source = @"
class Standalone:
    @override
    def method(self) -> str:
        return ""standalone""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("no matching method");
    }

    // ========== Valid @override usage - should NOT produce errors ==========

    [Fact]
    public void Override_WithVirtualBase_Succeeds()
    {
        var source = @"
class Base:
    @virtual
    def method(self) -> str:
        return ""base""

class Derived(Base):
    @override
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Override_WithAbstractBase_Succeeds()
    {
        var source = @"
@abstract
class Base:
    @abstract
    def method(self) -> str:
        ...

class Derived(Base):
    @override
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Override_ChainedOverride_Succeeds()
    {
        // If base method is itself an override, we can override it too
        var source = @"
class GrandBase:
    @virtual
    def method(self) -> str:
        return ""grandbase""

class Base(GrandBase):
    @override
    def method(self) -> str:
        return ""base""

class Derived(Base):
    @override
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    // ========== No @override when shadowing virtual - should produce error ==========

    [Fact]
    public void ShadowVirtual_WithoutOverride_ProducesError()
    {
        var source = @"
class Base:
    @virtual
    def method(self) -> str:
        return ""base""

class Derived(Base):
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().ContainSingle();
        typeChecker.Errors[0].Message.Should().Contain("method");
        typeChecker.Errors[0].Message.Should().Contain("@override");
        typeChecker.Errors[0].Message.Should().Contain("virtual");
    }

    // ========== Non-overriding methods - should succeed ==========

    [Fact]
    public void NewMethod_NotInBase_Succeeds()
    {
        var source = @"
class Base:
    def base_method(self) -> str:
        return ""base""

class Derived(Base):
    def derived_method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ShadowNonVirtual_WithoutOverride_Succeeds()
    {
        // Shadowing a non-virtual method without @override is OK (it hides, not overrides)
        var source = @"
class Base:
    def method(self) -> str:
        return ""base""

class Derived(Base):
    def method(self) -> str:
        return ""derived""
";
        var (module, _, _, typeChecker, _) = CompileAndCheck(source);
        typeChecker.CheckModule(module);

        typeChecker.Errors.Should().BeEmpty();
    }
}
