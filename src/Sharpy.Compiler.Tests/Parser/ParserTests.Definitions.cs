#pragma warning disable CS0618 // ParserError is obsolete
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests: Type definitions, imports, complex examples, and error cases
/// </summary>
public partial class ParserTests
{
    #region Class Definitions

    [Fact]
    public void ParseSimpleClassDef()
    {
        var source = @"
class Person:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Name.Should().Be("Person");
        classDef.BaseClasses.Should().BeEmpty();
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseClassWithBase()
    {
        var source = @"
class Employee(Person):
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.BaseClasses.Should().HaveCount(1);
        classDef.BaseClasses[0].Name.Should().Be("Person");
    }

    [Fact]
    public void ParseClassWithMultipleBases()
    {
        var source = @"
class Manager(Person, ILeader):
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.BaseClasses.Should().HaveCount(2);
    }

    [Fact]
    public void ParseClassWithMethods()
    {
        var source = @"
class Counter:
    def increment(self):
        self.count = self.count + 1
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<FunctionDef>().Which.Name.Should().Be("increment");
    }

    [Fact]
    public void ParseDecoratedClass()
    {
        var source = @"
@dataclass
class Point:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Decorators.Should().HaveCount(1);
        classDef.Decorators[0].Name.Should().Be("dataclass");
    }

    #endregion

    #region Struct Definitions

    [Fact]
    public void ParseSimpleStructDef()
    {
        var source = @"
struct Point:
    x: int
    y: int
";
        var module = Parse(source);
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;
        structDef.Name.Should().Be("Point");
        structDef.Body.Should().HaveCount(2);
        structDef.Body[0].Should().BeOfType<VariableDeclaration>().Which.Name.Should().Be("x");
        structDef.Body[1].Should().BeOfType<VariableDeclaration>().Which.Name.Should().Be("y");
    }

    #endregion

    #region Interface Definitions

    [Fact]
    public void ParseSimpleInterfaceDef()
    {
        var source = @"
interface IDrawable:
    def draw():
        ...
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        interfaceDef.Name.Should().Be("IDrawable");
        interfaceDef.Body.Should().HaveCount(1);
    }

    #endregion

    #region Enum Definitions

    [Fact]
    public void ParseSimpleEnumDef()
    {
        var source = @"
enum Color:
    RED
    GREEN
    BLUE
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.Name.Should().Be("Color");
        enumDef.Members.Should().HaveCount(3);
        enumDef.Members[0].Name.Should().Be("RED");
        enumDef.Members[0].Value.Should().BeNull();
    }

    [Fact]
    public void ParseEnumWithValues()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.Members.Should().HaveCount(3);
        enumDef.Members[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
    }

    #endregion

    #region Import Statements

    [Fact]
    public void ParseImportStatement()
    {
        var module = Parse("import math");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(1);
        import.Names[0].Name.Should().Be("math");
        import.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseImportWithAlias()
    {
        var module = Parse("import numpy as np");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names[0].Name.Should().Be("numpy");
        import.Names[0].AsName.Should().Be("np");
    }

    [Fact]
    public void ParseMultipleImports()
    {
        var module = Parse("import math, sys");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(2);
        import.Names[0].Name.Should().Be("math");
        import.Names[1].Name.Should().Be("sys");
    }

    [Fact]
    public void ParseFromImport()
    {
        var module = Parse("from math import pi");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("math");
        fromImport.Names.Should().HaveCount(1);
        fromImport.Names[0].Name.Should().Be("pi");
        fromImport.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseFromImportWithAlias()
    {
        var module = Parse("from math import sqrt as square_root");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Names[0].Name.Should().Be("sqrt");
        fromImport.Names[0].AsName.Should().Be("square_root");
    }

    [Fact]
    public void ParseFromImportMultiple()
    {
        var module = Parse("from math import pi, e");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Names.Should().HaveCount(2);
    }

    [Fact]
    public void ParseDottedModuleImport()
    {
        var module = Parse("import utils.helpers");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(1);
        import.Names[0].Name.Should().Be("utils.helpers");
        import.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseDottedModuleImportWithAlias()
    {
        var module = Parse("import utils.helpers as h");
        var import = module.Body[0].Should().BeOfType<ImportStatement>().Subject;
        import.Names.Should().HaveCount(1);
        import.Names[0].Name.Should().Be("utils.helpers");
        import.Names[0].AsName.Should().Be("h");
    }

    [Fact]
    public void ParseFromDottedModuleImport()
    {
        var module = Parse("from utils.helpers import format_text");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("utils.helpers");
        fromImport.Names.Should().HaveCount(1);
        fromImport.Names[0].Name.Should().Be("format_text");
        fromImport.Names[0].AsName.Should().BeNull();
    }

    [Fact]
    public void ParseFromDottedModuleImportMultiple()
    {
        var module = Parse("from utils.helpers import func1, func2");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("utils.helpers");
        fromImport.Names.Should().HaveCount(2);
        fromImport.Names[0].Name.Should().Be("func1");
        fromImport.Names[1].Name.Should().Be("func2");
    }

    [Fact]
    public void ParseFromDottedModuleImportWithAliases()
    {
        var module = Parse("from utils.helpers import func1 as f1, func2 as f2");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("utils.helpers");
        fromImport.Names.Should().HaveCount(2);
        fromImport.Names[0].Name.Should().Be("func1");
        fromImport.Names[0].AsName.Should().Be("f1");
        fromImport.Names[1].Name.Should().Be("func2");
        fromImport.Names[1].AsName.Should().Be("f2");
    }

    #endregion

    #region Complex Examples

    [Fact]
    public void ParseComplexFunction()
    {
        var source = @"
def factorial(n: int) -> int:
    if n <= 1:
        return 1
    else:
        return n * factorial(n - 1)
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("factorial");
        funcDef.Body.Should().HaveCount(1);
        funcDef.Body[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParseComplexClass()
    {
        var source = @"
class BankAccount:
    def __init__(self, balance: float):
        self.balance = balance

    def deposit(self, amount: float):
        self.balance = self.balance + amount

    def withdraw(self, amount: float) -> bool:
        if self.balance >= amount:
            self.balance = self.balance - amount
            return True
        return False
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Name.Should().Be("BankAccount");
        classDef.Body.Should().HaveCount(3);
        classDef.Body.All(s => s is FunctionDef).Should().BeTrue();
    }

    [Fact]
    public void ParseNestedStructures()
    {
        var source = @"
for i in range(10):
    if i % 2 == 0:
        print(i)
    else:
        continue
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Body.Should().HaveCount(1);
        forStmt.Body[0].Should().BeOfType<IfStatement>();
    }

    #endregion

    #region Error Cases

    [Fact]
    public void ParseError_MissingColon()
    {
        var errors = ParseExpectingError("if True\n    pass");
        errors.Should().Contain("Expected Colon");
    }

    [Fact]
    public void ParseError_InvalidIndentation()
    {
        ParseExpectingError("def foo():\npass");
    }

    [Fact]
    public void ParseError_UnexpectedToken()
    {
        ParseExpectingError("def 123():\n    pass");
    }

    [Fact]
    public void ParseError_UnclosedBracket()
    {
        var errors = ParseExpectingError("[1, 2, 3");
        errors.Should().Contain("Expected RightBracket");
    }

    #endregion

    #region Additional Operator Tests

    [Fact]
    public void ParseBitwiseAnd()
    {
        var module = Parse("x & y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseAnd);
    }

    [Fact]
    public void ParseBitwiseOr()
    {
        var module = Parse("x | y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseOr);
    }

    [Fact]
    public void ParseBitwiseXor()
    {
        var module = Parse("x ^ y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.BitwiseXor);
    }

    [Fact]
    public void ParseBitwiseNot()
    {
        var module = Parse("~x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.BitwiseNot);
    }

    [Fact]
    public void ParseLeftShift()
    {
        var module = Parse("x << 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.LeftShift);
    }

    [Fact]
    public void ParseRightShift()
    {
        var module = Parse("x >> 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.RightShift);
    }

    [Fact]
    public void ParseFloorDivide()
    {
        var module = Parse("10 // 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.FloorDivide);
    }

    [Fact]
    public void ParseModulo()
    {
        var module = Parse("10 % 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Modulo);
    }

    [Fact]
    public void ParsePower()
    {
        var module = Parse("2 ** 8");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Power);
    }

    [Fact]
    public void ParsePowerRightAssociative()
    {
        // 2 ** 3 ** 2 should parse as 2 ** (3 ** 2) = 2 ** 9 = 512
        var module = Parse("2 ** 3 ** 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        outer.Operator.Should().Be(BinaryOperator.Power);
        outer.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");

        var inner = outer.Right.Should().BeOfType<BinaryOp>().Subject;
        inner.Operator.Should().Be(BinaryOperator.Power);
        inner.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
        inner.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    [Fact]
    public void ParseNullCoalesce()
    {
        var module = Parse("x ?? y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    [Fact]
    public void ParseLogicalOr()
    {
        var module = Parse("x or y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Or);
    }

    [Fact]
    public void ParseLogicalAnd()
    {
        var module = Parse("x and y");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.And);
    }

    [Fact]
    public void ParseInOperator()
    {
        var module = Parse("x in [1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.In);
    }

    [Fact]
    public void ParseNotInOperator()
    {
        var module = Parse("x not in [1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.NotIn);
    }

    [Fact]
    public void ParseIsNone()
    {
        var module = Parse("x is None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Is);
    }

    [Fact]
    public void ParseIsNotNone()
    {
        var module = Parse("x is not None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.IsNot);
    }

    #endregion

    #region Augmented Assignment Tests

    [Fact]
    public void ParsePlusAssign()
    {
        var module = Parse("x += 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PlusAssign);
    }

    [Fact]
    public void ParseMinusAssign()
    {
        var module = Parse("x -= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.MinusAssign);
    }

    [Fact]
    public void ParseStarAssign()
    {
        var module = Parse("x *= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.StarAssign);
    }

    [Fact]
    public void ParseSlashAssign()
    {
        var module = Parse("x /= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.SlashAssign);
    }

    [Fact]
    public void ParseFloorDivAssign()
    {
        var module = Parse("x //= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.DoubleSlashAssign);
    }

    [Fact]
    public void ParseModuloAssign()
    {
        var module = Parse("x %= 5");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PercentAssign);
    }

    [Fact]
    public void ParsePowerAssign()
    {
        var module = Parse("x **= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.PowerAssign);
    }

    [Fact]
    public void ParseBitwiseAndAssign()
    {
        var module = Parse("x &= 0xFF");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.AndAssign);
    }

    [Fact]
    public void ParseBitwiseOrAssign()
    {
        var module = Parse("x |= 0x01");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.OrAssign);
    }

    [Fact]
    public void ParseBitwiseXorAssign()
    {
        var module = Parse("x ^= 0x10");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.XorAssign);
    }

    [Fact]
    public void ParseLeftShiftAssign()
    {
        var module = Parse("x <<= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.LeftShiftAssign);
    }

    [Fact]
    public void ParseRightShiftAssign()
    {
        var module = Parse("x >>= 2");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assign.Operator.Should().Be(AssignmentOperator.RightShiftAssign);
    }

    #endregion

}
