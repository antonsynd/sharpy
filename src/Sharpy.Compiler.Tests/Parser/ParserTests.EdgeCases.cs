using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests: Const declarations, generics, expressions, and edge cases
/// </summary>
public partial class ParserTests
{
    #region Const Declaration Tests

    [Fact]
    public void ParseConstDeclaration()
    {
        var module = Parse("const MAX: int = 100");
        var constDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        constDecl.Name.Should().Be("MAX");
        constDecl.IsConst.Should().BeTrue();
        constDecl.Type.Name.Should().Be("int");
        constDecl.InitialValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("100");
    }

    [Fact]
    public void ParseConstDeclaration_WithInferredType()
    {
        // Type annotation is optional for const declarations per spec
        var module = Parse("const APP_NAME = \"MyApp\"");
        var constDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        constDecl.Name.Should().Be("APP_NAME");
        constDecl.IsConst.Should().BeTrue();
        constDecl.Type.Should().BeNull(); // No type annotation
        constDecl.InitialValue.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("MyApp");
    }

    [Fact]
    public void ParseConstDeclaration_AllLiteralTypes_WithInferredType()
    {
        // Test all supported literal types for const inference
        var source = @"const STR = ""hello""
const INT = 42
const FLOAT = 3.14
const BOOL = True";
        var module = Parse(source);
        module.Body.Should().HaveCount(4);

        // String const
        var strConst = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        strConst.Name.Should().Be("STR");
        strConst.IsConst.Should().BeTrue();
        strConst.Type.Should().BeNull();
        strConst.InitialValue.Should().BeOfType<StringLiteral>();

        // Int const
        var intConst = module.Body[1].Should().BeOfType<VariableDeclaration>().Subject;
        intConst.Name.Should().Be("INT");
        intConst.IsConst.Should().BeTrue();
        intConst.Type.Should().BeNull();
        intConst.InitialValue.Should().BeOfType<IntegerLiteral>();

        // Float const
        var floatConst = module.Body[2].Should().BeOfType<VariableDeclaration>().Subject;
        floatConst.Name.Should().Be("FLOAT");
        floatConst.IsConst.Should().BeTrue();
        floatConst.Type.Should().BeNull();
        floatConst.InitialValue.Should().BeOfType<FloatLiteral>();

        // Bool const
        var boolConst = module.Body[3].Should().BeOfType<VariableDeclaration>().Subject;
        boolConst.Name.Should().Be("BOOL");
        boolConst.IsConst.Should().BeTrue();
        boolConst.Type.Should().BeNull();
        boolConst.InitialValue.Should().BeOfType<BooleanLiteral>();
    }

    #endregion

    #region Auto Type Inference Tests

    [Fact]
    public void ParseAutoDeclaration()
    {
        var module = Parse("x: auto = 42");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("x");
        varDecl.IsConst.Should().BeFalse();
        varDecl.Type.Name.Should().Be("auto");
        varDecl.InitialValue.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("42");
    }

    [Fact]
    public void ParseAutoShadowing()
    {
        var source = @"x: int = 5
x: auto = ""hello""";
        var module = Parse(source);
        module.Body.Should().HaveCount(2);

        var firstDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        firstDecl.Name.Should().Be("x");
        firstDecl.Type.Name.Should().Be("int");

        var secondDecl = module.Body[1].Should().BeOfType<VariableDeclaration>().Subject;
        secondDecl.Name.Should().Be("x");
        secondDecl.Type.Name.Should().Be("auto");
        secondDecl.InitialValue.Should().BeOfType<StringLiteral>();
    }

    #endregion

    #region Generic Type Tests

    [Fact]
    public void ParseGenericType()
    {
        var module = Parse("x: list[int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.TypeArguments.Should().HaveCount(1);
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseNestedGenericType()
    {
        var module = Parse("x: list[list[int]]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.TypeArguments[0].Name.Should().Be("list");
        varDecl.Type.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseDictGenericType()
    {
        var module = Parse("x: dict[str, int]");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.TypeArguments.Should().HaveCount(2);
        varDecl.Type.TypeArguments[0].Name.Should().Be("str");
        varDecl.Type.TypeArguments[1].Name.Should().Be("int");
    }

    [Fact]
    public void ParseGenericClassDefinition()
    {
        var source = @"
class Container[T]:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters.Should().HaveCount(1);
        classDef.TypeParameters[0].Name.Should().Be("T");
    }

    [Fact]
    public void ParseMultipleTypeParameters()
    {
        var source = @"
class Pair[T, U]:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters.Should().HaveCount(2);
        classDef.TypeParameters[0].Name.Should().Be("T");
        classDef.TypeParameters[1].Name.Should().Be("U");
    }

    [Fact]
    public void ParseGenericFunctionDefinition()
    {
        var source = @"
def identity[T](value: T) -> T:
    return value
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("identity");
        funcDef.TypeParameters.Should().HaveCount(1);
        funcDef.TypeParameters[0].Name.Should().Be("T");
        funcDef.Parameters.Should().HaveCount(1);
        funcDef.Parameters[0].Type!.Name.Should().Be("T");
        funcDef.ReturnType!.Name.Should().Be("T");
    }

    [Fact]
    public void ParseGenericFunctionWithMultipleTypeParameters()
    {
        var source = @"
def find_max[T, U](a: T, b: U) -> T:
    return a
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("find_max");
        funcDef.TypeParameters.Should().HaveCount(2);
        funcDef.TypeParameters[0].Name.Should().Be("T");
        funcDef.TypeParameters[1].Name.Should().Be("U");
        funcDef.Parameters.Should().HaveCount(2);
        funcDef.Parameters[0].Type!.Name.Should().Be("T");
        funcDef.Parameters[1].Type!.Name.Should().Be("U");
        funcDef.ReturnType!.Name.Should().Be("T");
    }

    [Fact]
    public void ParseGenericFunctionWithGenericReturnType()
    {
        var source = @"
def create_list[T]() -> list[T]:
    return []
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("create_list");
        funcDef.TypeParameters.Should().HaveCount(1);
        funcDef.TypeParameters[0].Name.Should().Be("T");
        funcDef.ReturnType!.Name.Should().Be("list");
        funcDef.ReturnType!.TypeArguments.Should().HaveCount(1);
        funcDef.ReturnType!.TypeArguments[0].Name.Should().Be("T");
    }

    [Fact]
    public void ParseNonGenericFunctionHasEmptyTypeParameters()
    {
        var source = @"
def normal_func(x: int) -> int:
    return x
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Name.Should().Be("normal_func");
        funcDef.TypeParameters.Should().BeEmpty();
    }

    [Fact]
    public void ParseFunctionWithInterfaceConstraint()
    {
        var source = @"
def find_max[T: IComparable](a: T, b: T) -> T:
    return a
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters.Should().HaveCount(1);
        func.TypeParameters[0].Name.Should().Be("T");
        func.TypeParameters[0].Constraints.Should().HaveCount(1);
        var typeConstraint = func.TypeParameters[0].Constraints[0].Should().BeOfType<TypeConstraint>().Subject;
        typeConstraint.Type.Name.Should().Be("IComparable");
    }

    [Fact]
    public void ParseFunctionWithClassConstraint()
    {
        var source = @"
def process[T: class](item: T):
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters[0].Constraints[0].Should().BeOfType<ClassConstraint>();
    }

    [Fact]
    public void ParseFunctionWithStructConstraint()
    {
        var source = @"
def process[T: struct](item: T):
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters[0].Constraints[0].Should().BeOfType<StructConstraint>();
    }

    [Fact]
    public void ParseFunctionWithNewConstraint()
    {
        var source = @"
def create[T: new()]() -> T:
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters[0].Constraints[0].Should().BeOfType<NewConstraint>();
    }

    [Fact]
    public void ParseFunctionWithMultipleConstraints()
    {
        var source = @"
def process[T: IFoo & IBar](item: T):
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters[0].Constraints.Should().HaveCount(2);
        func.TypeParameters[0].Constraints[0].Should().BeOfType<TypeConstraint>().Which.Type.Name.Should().Be("IFoo");
        func.TypeParameters[0].Constraints[1].Should().BeOfType<TypeConstraint>().Which.Type.Name.Should().Be("IBar");
    }

    [Fact]
    public void ParseClassWithConstraint()
    {
        var source = @"
class Container[T: class]:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters[0].Constraints[0].Should().BeOfType<ClassConstraint>();
    }

    [Fact]
    public void ParseMultipleTypeParamsWithConstraints()
    {
        var source = @"
def convert[T: IInput, U: class & IOutput](val: T) -> U:
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.TypeParameters.Should().HaveCount(2);
        func.TypeParameters[0].Name.Should().Be("T");
        func.TypeParameters[1].Name.Should().Be("U");
        func.TypeParameters[0].Constraints.Should().HaveCount(1);
        func.TypeParameters[1].Constraints.Should().HaveCount(2);
    }

    #endregion

    #region Keyword Argument Tests

    [Fact]
    public void ParseFunctionCallWithKeywordArgs()
    {
        var module = Parse("foo(x=1, y=2)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.KeywordArguments.Should().HaveCount(2);
        call.KeywordArguments[0].Name.Should().Be("x");
        call.KeywordArguments[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseFunctionCallMixedArgs()
    {
        var module = Parse("foo(1, 2, z=3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        call.Arguments.Should().HaveCount(2);
        call.KeywordArguments.Should().HaveCount(1);
        call.KeywordArguments[0].Name.Should().Be("z");
    }

    [Fact]
    public void ParseFunctionCallPositionalAfterKeywordThrows()
    {
        // foo(x=1, 2) - positional after keyword is a syntax error
        var errors = ParseExpectingError("foo(x=1, 2)");
        errors.Should().Contain("Positional argument cannot follow keyword argument");
    }

    #endregion

    #region From Import All Tests

    [Fact]
    public void ParseFromImportAll()
    {
        var module = Parse("from math import *");
        var fromImport = module.Body[0].Should().BeOfType<FromImportStatement>().Subject;
        fromImport.Module.Should().Be("math");
        fromImport.ImportAll.Should().BeTrue();
    }

    #endregion

    #region Chained Member Access Tests

    [Fact]
    public void ParseChainedMemberAccess()
    {
        var module = Parse("a.b.c");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        outer.Member.Should().Be("c");

        var inner = outer.Object.Should().BeOfType<MemberAccess>().Subject;
        inner.Member.Should().Be("b");
        inner.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("a");
    }

    [Fact]
    public void ParseChainedIndexAccess()
    {
        var module = Parse("matrix[0][1]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<IndexAccess>().Subject;
        outer.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");

        var inner = outer.Object.Should().BeOfType<IndexAccess>().Subject;
        inner.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
        inner.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("matrix");
    }

    [Fact]
    public void ParseMixedMemberAndIndexAccess()
    {
        var module = Parse("obj.list[0].field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outer = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        outer.Member.Should().Be("field");

        var index = outer.Object.Should().BeOfType<IndexAccess>().Subject;
        var member = index.Object.Should().BeOfType<MemberAccess>().Subject;
        member.Member.Should().Be("list");
    }

    #endregion

    #region Empty Collection Tests

    [Fact]
    public void ParseEmptyTuple()
    {
        var module = Parse("()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ParseEmptySet_SpecialSyntax()
    {
        // According to v0.2 spec, {/} is empty set, {} is empty dict
        var module = Parse("{/}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<SetLiteral>().Which.Elements.Should().BeEmpty();
    }

    #endregion

    #region Docstring Tests

    [Fact]
    public void ParseModuleDocstring()
    {
        var source = @"
""""""This is a module docstring""""""

def foo():
    pass
";
        var module = Parse(source);
        module.DocString.Should().Be("This is a module docstring");
        module.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseFunctionDocstring()
    {
        var source = @"
def greet(name: str):
    """"""Greet a person by name""""""
    print(name)
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.DocString.Should().Be("Greet a person by name");
    }

    [Fact]
    public void ParseClassDocstring()
    {
        var source = @"
class Person:
    """"""Represents a person""""""
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.DocString.Should().Be("Represents a person");
    }

    #endregion

    #region Type Check Disambiguation Tests

    [Fact]
    public void ParseTypeCheckVsIdentityComparison()
    {
        // According to spec: "x is MyClass" is type check, "x is None" is identity comparison
        // Current implementation treats both as comparison operators
        // This requires lookahead to check if RHS is a type name vs None/identifier
        var module = Parse("x is int");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<TypeCheck>();
    }

    #endregion

    #region Additional Control Flow Tests

    [Fact]
    public void ParseMultipleElifClauses()
    {
        var source = @"
if x == 1:
    print(""one"")
elif x == 2:
    print(""two"")
elif x == 3:
    print(""three"")
else:
    print(""other"")
";
        var module = Parse(source);
        var ifStmt = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        ifStmt.ElifClauses.Should().HaveCount(2);
    }

    [Fact]
    public void ParseNestedIfStatements()
    {
        var source = @"
if x > 0:
    if y > 0:
        print(""both positive"")
";
        var module = Parse(source);
        var outer = module.Body[0].Should().BeOfType<IfStatement>().Subject;
        outer.ThenBody.Should().HaveCount(1);
        outer.ThenBody[0].Should().BeOfType<IfStatement>();
    }

    [Fact]
    public void ParseForWithTupleUnpacking()
    {
        var source = @"
for x, y in pairs:
    print(x, y)
";
        var module = Parse(source);
        var forStmt = module.Body[0].Should().BeOfType<ForStatement>().Subject;
        forStmt.Target.Should().BeOfType<TupleLiteral>().Which.Elements.Should().HaveCount(2);
    }

    #endregion

    #region v0.1 Comprehensive Edge Cases

    [Fact]
    public void ParseEmptyClassWithPass()
    {
        var source = @"
class Empty:
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Name.Should().Be("Empty");
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseEmptyStructWithPass()
    {
        var source = @"
struct EmptyStruct:
    pass
";
        var module = Parse(source);
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;
        structDef.Name.Should().Be("EmptyStruct");
        structDef.Body.Should().HaveCount(1);
        structDef.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseEmptyInterfaceWithPass()
    {
        var source = @"
interface IEmpty:
    pass
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        interfaceDef.Name.Should().Be("IEmpty");
        interfaceDef.Body.Should().HaveCount(1);
        interfaceDef.Body[0].Should().BeOfType<PassStatement>();
    }

    [Fact]
    public void ParseError_EmptyEnum_ThrowsError()
    {
        var source = @"
enum Empty:
    pass
";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("must have at least one member");
    }

    [Fact]
    public void ParseMultipleDecoratorsOnClass()
    {
        var source = @"
@sealed
@dataclass
class Point:
    x: int
    y: int
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Decorators.Should().HaveCount(2);
        classDef.Decorators[0].Name.Should().Be("sealed");
        classDef.Decorators[1].Name.Should().Be("dataclass");
    }

    [Fact]
    public void ParseDeeplyNestedGenerics()
    {
        var source = "x: dict[str, list[tuple[int, float]]]";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.TypeArguments[1].Name.Should().Be("list");
        varDecl.Type.TypeArguments[1].TypeArguments[0].Name.Should().Be("tuple");
        varDecl.Type.TypeArguments[1].TypeArguments[0].TypeArguments.Should().HaveCount(2);
    }

    [Fact]
    public void ParseNullableGenericType()
    {
        var source = "x: list[int]?";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.IsOptional.Should().BeTrue();
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseNullableNestedGenericType()
    {
        var source = "x: dict[str, list[int]?]";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("dict");
        varDecl.Type.TypeArguments[1].Name.Should().Be("list");
        varDecl.Type.TypeArguments[1].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseListOfNullableInts()
    {
        var source = "points: list[int?]";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.IsOptional.Should().BeFalse();
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
        varDecl.Type.TypeArguments[0].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseNullableListOfNullableInts()
    {
        var source = "both: list[int?]?";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("list");
        varDecl.Type.IsOptional.Should().BeTrue();
        varDecl.Type.TypeArguments[0].Name.Should().Be("int");
        varDecl.Type.TypeArguments[0].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseNullableStringType()
    {
        var source = "name: str?";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("str");
        varDecl.Type.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseConstWithExplicitType()
    {
        var source = "const MAX_SIZE: int = 100";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Name.Should().Be("MAX_SIZE");
        varDecl.IsConst.Should().BeTrue();
        varDecl.Type.Name.Should().Be("int");
    }

    [Fact]
    public void ParseComplexMemberAccessChain()
    {
        var source = "result = obj.method().property.nested_method(arg).field";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;

        var memberAccess = assignment.Value;
        memberAccess.Should().BeOfType<MemberAccess>();
    }

    [Fact]
    public void ParseChainedIndexingAndMemberAccess()
    {
        var source = "value = array[0].method()[key].property";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;

        // Should parse without throwing
        assignment.Value.Should().NotBeNull();
    }

    [Fact]
    public void ParseLambdaWithMultipleParameters()
    {
        var source = "add = lambda x, y: x + y";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var lambda = assignment.Value.Should().BeOfType<LambdaExpression>().Subject;
        lambda.Parameters.Should().HaveCount(2);
    }

    [Fact]
    public void ParseComplexConditionalExpression()
    {
        var source = "result = a if x > 0 else b if x < 0 else c";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<ConditionalExpression>();
    }

    [Fact]
    public void ParseNullCoalescingChain()
    {
        var source = "value = a ?? b ?? c ?? default";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;

        // Should create nested binary operations
        assignment.Value.Should().BeOfType<BinaryOp>();
    }

    [Fact]
    public void ParseNullCoalescingAssignment()
    {
        var source = "x = a ?? b";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<BinaryOp>().Which.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    [Fact]
    public void ParseTypeCheckExpression()
    {
        var source = "result = value is int";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<TypeCheck>();
    }

    [Fact]
    public void ParseGenericClassWithDefaultTypeParameter()
    {
        var source = @"
class Container[T]:
    def __init__(self, value: T):
        self.value = value
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.TypeParameters.Should().HaveCount(1);
        classDef.Body.Should().HaveCount(1);
        classDef.Body[0].Should().BeOfType<FunctionDef>();
    }

    [Fact]
    public void ParseStructWithGenericFields()
    {
        var source = @"
struct Pair[T, U]:
    first: T
    second: U
";
        var module = Parse(source);
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;
        structDef.TypeParameters.Should().HaveCount(2);
        structDef.Body.Should().HaveCount(2);
    }

    [Fact]
    public void ParseInterfaceWithGenericMethods()
    {
        var source = @"
interface IConverter[TInput, TOutput]:
    def convert(self, input: TInput) -> TOutput:
        ...
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;
        interfaceDef.TypeParameters.Should().HaveCount(2);
    }

    [Fact]
    public void ParseNestedClassDefinitions()
    {
        var source = @"
class Outer:
    class Inner:
        pass
";
        var module = Parse(source);
        var outerClass = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        outerClass.Body[0].Should().BeOfType<ClassDef>().Which.Name.Should().Be("Inner");
    }

    [Fact]
    public void ParseDecoratorWithSimpleExpression()
    {
        var source = @"
@decorator
def func():
    pass
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Decorators.Should().HaveCount(1);
        funcDef.Decorators[0].Name.Should().Be("decorator");
    }

    [Fact]
    public void ParseError_MalformedDecorator_ThrowsError()
    {
        var source = "@\ndef func():\n    pass";
        ParseExpectingError(source);
    }

    [Fact]
    public void ParseComplexTryExceptFinally()
    {
        var source = @"
try:
    risky_operation()
except ValueError as e:
    handle_value_error(e)
except KeyError as e:
    handle_key_error(e)
except Exception as e:
    handle_generic(e)
finally:
    cleanup()
";
        var module = Parse(source);
        var tryStmt = module.Body[0].Should().BeOfType<TryStatement>().Subject;
        tryStmt.Handlers.Should().HaveCount(3);
        tryStmt.FinallyBody.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ParseSlicingWithAllComponents()
    {
        var source = "subset = array[start:stop:step]";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<SliceAccess>();
    }

    [Fact]
    public void ParseSlicingWithOmittedComponents()
    {
        var source = "subset = array[::2]";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<SliceAccess>();
    }

    [Fact]
    public void ParseMultidimensionalIndexing()
    {
        var source = "value = matrix[(i, j)]";  // Use tuple syntax
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<IndexAccess>();
    }

    [Fact]
    public void ParseComplexBitwiseExpression()
    {
        var source = "result = (a & b) | (c ^ d) << 2";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<BinaryOp>();
    }

    [Fact]
    public void ParseChainedComparisons()
    {
        var source = "valid = 0 <= x < 10";
        var module = Parse(source);
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;

        // Should parse as chained comparison
        assignment.Value.Should().NotBeNull();
    }

    [Fact]
    public void ParseAllAugmentedAssignments()
    {
        var source = @"
x += 1
y -= 2
z *= 3
a /= 4
b //= 5
c %= 6
d **= 2
e &= 0xFF
f |= 0x01
g ^= 0x0F
h <<= 1
i >>= 2
";
        var module = Parse(source);
        module.Body.Should().HaveCount(12);
        module.Body.Should().AllBeOfType<Assignment>();
    }

    [Fact]
    public void ParseFunctionWithComplexReturnType()
    {
        var source = @"
def complex_func() -> dict[str, list[tuple[int, float]]]:
    pass
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.ReturnType.Should().NotBeNull();
        funcDef.ReturnType.Name.Should().Be("dict");
    }

    [Fact]
    public void ParseFunctionReturningNullableType()
    {
        var source = @"
def find(key: str) -> int?:
    pass
";
        var module = Parse(source);
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.ReturnType.Should().NotBeNull();
        funcDef.ReturnType.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseClassWithMultipleInheritance()
    {
        var source = @"
class Child(Base1, Base2, IInterface):
    pass
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.BaseClasses.Should().HaveCount(3);
    }

    [Fact]
    public void ParseEnumWithExplicitValues()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    COMPLETE = 2
";
        var module = Parse(source);
        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.Members.Should().HaveCount(3);
        enumDef.Members.Should().AllSatisfy(m => m.Value.Should().NotBeNull());
    }

    [Fact]
    public void ParseLiteralNameAsClassMember()
    {
        var source = @"
class MyClass:
    `special-property`: int
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        classDef.Body.Should().HaveCount(1);
    }

    [Fact]
    public void ParseError_InconsistentIndentation_ThrowsLexerError()
    {
        var source = "if True:\n    x = 1\n  y = 2";  // Mixed indentation
        // Lexer or parser will report an error via diagnostics
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        var hasError = lexer.Diagnostics.HasErrors || parser.Diagnostics.HasErrors;
        hasError.Should().BeTrue("Expected a lexer or parser error for inconsistent indentation");
    }

    [Fact]
    public void ParseError_MissingGenericCloseBracket_ThrowsError()
    {
        var source = "x: list[int";
        ParseExpectingError(source);
    }

    [Fact]
    public void ParseError_InvalidTypeParameter_ThrowsError()
    {
        var source = "class Generic[123]:\n    pass";
        ParseExpectingError(source);
    }

    #endregion

    #region Try and Maybe Expressions

    [Fact]
    public void ParseTryExpression_Simple()
    {
        var module = Parse("try risky_operation()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tryExpr = exprStmt.Expression.Should().BeOfType<TryExpression>().Subject;
        tryExpr.ExceptionTypes.Should().BeEmpty();
        tryExpr.Operand.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseTryExpression_WithExceptionType()
    {
        var module = Parse("try[ValueError] int(user_input)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tryExpr = exprStmt.Expression.Should().BeOfType<TryExpression>().Subject;
        tryExpr.ExceptionTypes.Should().HaveCount(1);
        tryExpr.ExceptionTypes[0].Name.Should().Be("ValueError");
        tryExpr.Operand.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseTryExpression_WithGenericExceptionType()
    {
        var module = Parse("try[HttpError[T]] fetch_data()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tryExpr = exprStmt.Expression.Should().BeOfType<TryExpression>().Subject;
        tryExpr.ExceptionTypes.Should().HaveCount(1);
        tryExpr.ExceptionTypes[0].Name.Should().Be("HttpError");
        tryExpr.ExceptionTypes[0].TypeArguments.Should().HaveCount(1);
        tryExpr.ExceptionTypes[0].TypeArguments[0].Name.Should().Be("T");
    }

    [Fact]
    public void ParseTryExpression_WithUnionExceptionTypes()
    {
        var module = Parse("try[ValueError | KeyError | TypeError] do_something()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tryExpr = exprStmt.Expression.Should().BeOfType<TryExpression>().Subject;
        tryExpr.ExceptionTypes.Should().HaveCount(3);
        tryExpr.ExceptionTypes[0].Name.Should().Be("ValueError");
        tryExpr.ExceptionTypes[1].Name.Should().Be("KeyError");
        tryExpr.ExceptionTypes[2].Name.Should().Be("TypeError");
    }

    [Fact]
    public void ParseTryExpression_InAssignment()
    {
        var module = Parse("result = try parse_int(s)");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var tryExpr = assignment.Value.Should().BeOfType<TryExpression>().Subject;
        tryExpr.Operand.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseTryExpression_WithArithmeticOperand()
    {
        var module = Parse("try 1 / x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tryExpr = exprStmt.Expression.Should().BeOfType<TryExpression>().Subject;
        var binaryOp = tryExpr.Operand.Should().BeOfType<BinaryOp>().Subject;
        binaryOp.Operator.Should().Be(BinaryOperator.Divide);
    }

    [Fact]
    public void ParseMaybeExpression_Simple()
    {
        var module = Parse("maybe nullable_value");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var maybeExpr = exprStmt.Expression.Should().BeOfType<MaybeExpression>().Subject;
        maybeExpr.Operand.Should().BeOfType<Identifier>();
    }

    [Fact]
    public void ParseMaybeExpression_WithMemberAccess()
    {
        var module = Parse("maybe user?.name");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var maybeExpr = exprStmt.Expression.Should().BeOfType<MaybeExpression>().Subject;
        var memberAccess = maybeExpr.Operand.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.IsNullConditional.Should().BeTrue();
    }

    [Fact]
    public void ParseMaybeExpression_InAssignment()
    {
        var module = Parse("opt = maybe get_nullable()");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var maybeExpr = assignment.Value.Should().BeOfType<MaybeExpression>().Subject;
        maybeExpr.Operand.Should().BeOfType<FunctionCall>();
    }

    [Fact]
    public void ParseMaybeExpression_WithNullCoalesce()
    {
        // maybe captures up to null coalesce level, so this parses as maybe (x ?? default)
        var module = Parse("maybe x ?? default");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var maybeExpr = exprStmt.Expression.Should().BeOfType<MaybeExpression>().Subject;
        var binaryOp = maybeExpr.Operand.Should().BeOfType<BinaryOp>().Subject;
        binaryOp.Operator.Should().Be(BinaryOperator.NullCoalesce);
    }

    [Fact]
    public void ParseTryExpression_DisambiguatedFromTryStatement()
    {
        // try followed by ':' is a statement, try followed by expr is an expression
        var module = Parse("x = try foo()");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        assignment.Value.Should().BeOfType<TryExpression>();
    }

    [Fact]
    public void ParseTryStatement_StillWorks()
    {
        // Verify that try statements still parse correctly
        var source = @"try:
    risky()
except:
    handle()";
        var module = Parse(source);
        module.Body[0].Should().BeOfType<TryStatement>();
    }

    #endregion

    #region Super Expression

    [Fact]
    public void ParseSuperExpression_Simple()
    {
        var module = Parse("super()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<SuperExpression>();
    }

    [Fact]
    public void ParseSuperExpression_WithMemberAccess()
    {
        var module = Parse("super().__init__");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var memberAccess = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("__init__");
    }

    [Fact]
    public void ParseSuperExpression_WithMethodCall()
    {
        var module = Parse("super().__init__(x, y)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("__init__");
        call.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void ParseSuperExpression_InDunderMethod()
    {
        var source = @"class Child(Parent):
    def __init__(self, x: int):
        super().__init__(x)";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        var funcDef = classDef.Body[0].Should().BeOfType<FunctionDef>().Subject;
        var exprStmt = funcDef.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
    }

    [Fact]
    public void ParseSuperExpression_InOverrideMethod()
    {
        var source = @"class Child(Parent):
    @override
    def process(self, data: str) -> str:
        result = super().process(data)
        return result";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;
        var funcDef = classDef.Body[0].Should().BeOfType<FunctionDef>().Subject;
        funcDef.Decorators.Should().HaveCount(1);
        funcDef.Decorators[0].Name.Should().Be("override");
        var assignment = funcDef.Body[0].Should().BeOfType<Assignment>().Subject;
        var call = assignment.Value.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("process");
    }

    [Fact]
    public void ParseSuperExpression_WithDunderMethod()
    {
        var module = Parse("super().__str__()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("__str__");
    }

    [Fact]
    public void ParseSuperExpression_InAssignment()
    {
        var module = Parse("result = super().get_value()");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var call = assignment.Value.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("get_value");
    }

    [Fact]
    public void ParseSuperExpression_WithKeywordArguments()
    {
        var module = Parse("super().setup(name=\"test\", count=10)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var memberAccess = call.Function.Should().BeOfType<MemberAccess>().Subject;
        memberAccess.Object.Should().BeOfType<SuperExpression>();
        memberAccess.Member.Should().Be("setup");
        call.KeywordArguments.Should().HaveCount(2);
        call.KeywordArguments[0].Name.Should().Be("name");
        call.KeywordArguments[1].Name.Should().Be("count");
    }

    [Fact]
    public void ParseSuperExpression_ChainedCalls()
    {
        var module = Parse("super().get_manager().process()");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var outerCall = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var outerMember = outerCall.Function.Should().BeOfType<MemberAccess>().Subject;
        outerMember.Member.Should().Be("process");
        var innerCall = outerMember.Object.Should().BeOfType<FunctionCall>().Subject;
        var innerMember = innerCall.Function.Should().BeOfType<MemberAccess>().Subject;
        innerMember.Object.Should().BeOfType<SuperExpression>();
        innerMember.Member.Should().Be("get_manager");
    }

    #endregion
}
