using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Tests.Semantic;

public class NameResolverOperatorTests
{
    private (SymbolTable symbolTable, NameResolver resolver) CreateNameResolver()
    {
        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var resolver = new NameResolver(symbolTable, NullLogger.Instance);
        return (symbolTable, resolver);
    }

    private Sharpy.Compiler.Parser.Ast.Module ParseSource(string source)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        return parser.ParseModule();
    }

    #region Valid Operator Registration Tests

    [Fact]
    public void RegistersValidArithmeticOperator()
    {
        var source = @"
class Vector:
    def __add__(self, other: Vector) -> Vector:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var vectorType = symbolTable.Lookup("Vector") as TypeSymbol;
        vectorType.Should().NotBeNull();
        vectorType!.OperatorMethods.Should().ContainKey("__add__");
        vectorType.OperatorMethods["__add__"].Should().HaveCount(1);
        vectorType.OperatorMethods["__add__"][0].Name.Should().Be("__add__");
    }

    [Fact]
    public void RegistersValidComparisonOperator()
    {
        var source = @"
class Point:
    def __eq__(self, other: Point) -> bool:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var pointType = symbolTable.Lookup("Point") as TypeSymbol;
        pointType.Should().NotBeNull();
        pointType!.OperatorMethods.Should().ContainKey("__eq__");
    }

    [Fact]
    public void RegistersMultipleOperators()
    {
        var source = @"
class Number:
    def __add__(self, other: Number) -> Number:
        pass
    
    def __sub__(self, other: Number) -> Number:
        pass
    
    def __mul__(self, other: Number) -> Number:
        pass
    
    def __eq__(self, other: Number) -> bool:
        pass
    
    def __lt__(self, other: Number) -> bool:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var numberType = symbolTable.Lookup("Number") as TypeSymbol;
        numberType.Should().NotBeNull();
        numberType!.OperatorMethods.Should().ContainKey("__add__");
        numberType.OperatorMethods.Should().ContainKey("__sub__");
        numberType.OperatorMethods.Should().ContainKey("__mul__");
        numberType.OperatorMethods.Should().ContainKey("__eq__");
        numberType.OperatorMethods.Should().ContainKey("__lt__");
    }

    [Fact]
    public void RegistersUnaryOperator()
    {
        var source = @"
class SignedValue:
    def __neg__(self) -> SignedValue:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var valueType = symbolTable.Lookup("SignedValue") as TypeSymbol;
        valueType.Should().NotBeNull();
        valueType!.OperatorMethods.Should().ContainKey("__neg__");
    }

    [Fact]
    public void RegistersInPlaceOperator()
    {
        var source = @"
class Counter:
    def __iadd__(self, other: int) -> Counter:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var counterType = symbolTable.Lookup("Counter") as TypeSymbol;
        counterType.Should().NotBeNull();
        counterType!.OperatorMethods.Should().ContainKey("__iadd__");
    }

    [Fact]
    public void RegistersPowerOperator()
    {
        var source = @"
class Matrix:
    def __pow__(self, exponent: int) -> Matrix:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var matrixType = symbolTable.Lookup("Matrix") as TypeSymbol;
        matrixType.Should().NotBeNull();
        matrixType!.OperatorMethods.Should().ContainKey("__pow__");
    }

    [Fact]
    public void RegistersInPlacePowerOperator()
    {
        var source = @"
class Accumulator:
    def __ipow__(self, exponent: int) -> Accumulator:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var accType = symbolTable.Lookup("Accumulator") as TypeSymbol;
        accType.Should().NotBeNull();
        accType!.OperatorMethods.Should().ContainKey("__ipow__");
    }

    [Fact]
    public void DoesNotRegisterNonOperatorDunders()
    {
        var source = @"
class Example:
    def __init__(self):
        pass
    
    def __str__(self) -> str:
        pass
    
    def __repr__(self) -> str:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var exampleType = symbolTable.Lookup("Example") as TypeSymbol;
        exampleType.Should().NotBeNull();
        exampleType!.OperatorMethods.Should().BeEmpty();
        
        // But the methods should still be in the regular Methods list
        exampleType.Methods.Should().Contain(m => m.Name == "__init__");
        exampleType.Methods.Should().Contain(m => m.Name == "__str__");
        exampleType.Methods.Should().Contain(m => m.Name == "__repr__");
    }

    #endregion

    #region Invalid Operator Rejection Tests

    [Fact]
    public void RejectsOperatorWithWrongParameterCount()
    {
        var source = @"
class BadVector:
    def __add__(self, x, y, z) -> BadVector:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().NotBeEmpty();
        resolver.Errors[0].Message.Should().Contain("must have exactly 2 parameters");
        
        var vectorType = symbolTable.Lookup("BadVector") as TypeSymbol;
        vectorType.Should().NotBeNull();
        vectorType!.OperatorMethods.Should().NotContainKey("__add__");
    }

    [Fact]
    public void RejectsComparisonOperatorWithNonBoolReturn()
    {
        var source = @"
class BadCompare:
    def __eq__(self, other: BadCompare) -> int:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().NotBeEmpty();
        resolver.Errors[0].Message.Should().Contain("must return 'bool'");
        
        var compareType = symbolTable.Lookup("BadCompare") as TypeSymbol;
        compareType.Should().NotBeNull();
        compareType!.OperatorMethods.Should().NotContainKey("__eq__");
    }

    [Fact]
    public void RejectsUnaryOperatorWithWrongParameterCount()
    {
        var source = @"
class BadNegate:
    def __neg__(self, extra) -> BadNegate:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().NotBeEmpty();
        resolver.Errors[0].Message.Should().Contain("must have exactly 1 parameter");
        
        var negateType = symbolTable.Lookup("BadNegate") as TypeSymbol;
        negateType.Should().NotBeNull();
        negateType!.OperatorMethods.Should().NotContainKey("__neg__");
    }

    [Fact]
    public void RejectsOperatorWithVoidReturn()
    {
        var source = @"
class BadAdd:
    def __add__(self, other: BadAdd) -> None:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().NotBeEmpty();
        resolver.Errors[0].Message.Should().Contain("must return a non-void type");
        
        var addType = symbolTable.Lookup("BadAdd") as TypeSymbol;
        addType.Should().NotBeNull();
        addType!.OperatorMethods.Should().NotContainKey("__add__");
    }

    #endregion

    #region Overload Tests

    // Note: Method overloading is not yet fully supported in Sharpy's symbol table.
    // This test is skipped until overload resolution is implemented.
    [Fact(Skip = "Method overloading not yet supported in symbol table")]
    public void RegistersMultipleOverloadsOfSameOperator()
    {
        var source = @"
class FlexibleAdd:
    def __add__(self, other: FlexibleAdd) -> FlexibleAdd:
        pass
    
    def __add__(self, other: int) -> FlexibleAdd:
        pass
";
        var module = ParseSource(source);
        var (symbolTable, resolver) = CreateNameResolver();
        
        resolver.ResolveDeclarations(module);

        resolver.Errors.Should().BeEmpty();
        
        var flexType = symbolTable.Lookup("FlexibleAdd") as TypeSymbol;
        flexType.Should().NotBeNull();
        flexType!.OperatorMethods.Should().ContainKey("__add__");
        flexType.OperatorMethods["__add__"].Should().HaveCount(2);
    }

    #endregion
}
