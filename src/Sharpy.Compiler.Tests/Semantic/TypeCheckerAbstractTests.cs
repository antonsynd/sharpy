using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for TypeChecker's abstract method detection and validation.
/// Covers both explicit @abstract decorator and implicit abstract (ellipsis body in @abstract class).
/// </summary>
public class TypeCheckerAbstractTests
{
    private (Module module, SymbolTable symbolTable, SemanticInfo semanticInfo, TypeChecker typeChecker) CompileAndCheck(string source)
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

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Implicit Abstract (ellipsis in @abstract class)

    [Fact]
    public void EllipsisBody_InAbstractClass_TreatedAsAbstract_InlineEllipsis()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();

        // Verify method is marked abstract in symbol table
        var shapeType = symbolTable.LookupType("Shape");
        shapeType.Should().NotBeNull();
        var areaMethod = shapeType!.Methods.FirstOrDefault(m => m.Name == "area");
        areaMethod.Should().NotBeNull();
        areaMethod!.IsAbstract.Should().BeTrue("ellipsis body in @abstract class should be treated as abstract");
    }

    [Fact]
    public void EllipsisBody_InAbstractClass_TreatedAsAbstract_MultiLine()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float:
        ...
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();

        var shapeType = symbolTable.LookupType("Shape");
        var areaMethod = shapeType!.Methods.FirstOrDefault(m => m.Name == "area");
        areaMethod!.IsAbstract.Should().BeTrue("multi-line ellipsis body in @abstract class should be treated as abstract");
    }

    [Fact]
    public void MultipleImplicitAbstractMethods_AllTreatedAsAbstract()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
    def contains(self, x: float, y: float) -> bool: ...
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();

        var shapeType = symbolTable.LookupType("Shape");
        shapeType!.Methods.Should().HaveCount(3);
        shapeType.Methods.Should().OnlyContain(m => m.IsAbstract, "all methods with ellipsis body in @abstract class should be abstract");
    }

    #endregion

    #region Explicit @abstract decorator (still valid)

    [Fact]
    public void ExplicitAbstractDecorator_StillWorks()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();

        var shapeType = symbolTable.LookupType("Shape");
        var areaMethod = shapeType!.Methods.FirstOrDefault(m => m.Name == "area");
        areaMethod!.IsAbstract.Should().BeTrue();
    }

    #endregion

    #region Mixed abstract and concrete methods

    [Fact]
    public void MixedAbstractAndConcrete_InAbstractClass()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...

    def describe(self) -> str:
        return ""shape""
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().BeEmpty();

        var shapeType = symbolTable.LookupType("Shape");
        var areaMethod = shapeType!.Methods.FirstOrDefault(m => m.Name == "area");
        var describeMethod = shapeType.Methods.FirstOrDefault(m => m.Name == "describe");

        areaMethod!.IsAbstract.Should().BeTrue("ellipsis body method should be abstract");
        describeMethod!.IsAbstract.Should().BeFalse("method with real body should not be abstract");
    }

    #endregion

    #region Ellipsis in concrete class (NotImplementedException stub)

    [Fact]
    public void EllipsisBody_InConcreteClass_NotTreatedAsAbstract()
    {
        var source = @"
class TodoService:
    def not_done_yet(self) -> int: ...
";
        var (module, symbolTable, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should NOT error - ellipsis in concrete class is valid (generates NotImplementedException)
        typeChecker.Errors.Should().BeEmpty();

        var todoType = symbolTable.LookupType("TodoService");
        var method = todoType!.Methods.FirstOrDefault(m => m.Name == "not_done_yet");
        method!.IsAbstract.Should().BeFalse("ellipsis body in concrete class should NOT be treated as abstract");
    }

    #endregion

    #region Error cases

    [Fact]
    public void AbstractDecorator_OnMethod_InConcreteClass_Error()
    {
        var source = @"
class Shape:
    @abstract
    def area(self) -> float: ...
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("abstract class",
            "should indicate that @abstract method requires @abstract class");
    }

    [Fact]
    public void AbstractDecorator_WithRealBody_Error()
    {
        var source = @"
@abstract
class Shape:
    @abstract
    def area(self) -> float:
        return 0.0
";
        var (module, _, _, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Errors.Should().NotBeEmpty();
        typeChecker.Errors[0].Message.Should().Contain("...",
            "should indicate that @abstract method must have '...' as body");
    }

    #endregion
}
