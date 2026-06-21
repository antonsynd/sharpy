using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

[Trait("Category", "Property")]
public class UnparserRoundTripTests
{
    private static void AssertRoundTrip(string source)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SParser(tokens);
        var ast1 = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors);

        var unparsed = Pretty.Unparser.Unparse(ast1);

        var lexer2 = new SLexer(unparsed);
        var tokens2 = lexer2.TokenizeAll();
        var parser2 = new SParser(tokens2);
        var ast2 = parser2.ParseModule();
        Assert.False(parser2.Diagnostics.HasErrors);

        var normalizer = Pretty.AstNormalizer.Instance;
        var norm1 = normalizer.NormalizeModule(ast1);
        var norm2 = normalizer.NormalizeModule(ast2);

        Assert.True(Pretty.StructuralEqualityComparer.Instance.Equals(norm1, norm2),
            $"Round-trip failed.\nOriginal:\n{source}\nUnparsed:\n{unparsed}");
    }

    [Fact] public void IntegerLiteral() => AssertRoundTrip("42\n");
    [Fact] public void IntegerLiteral_Suffix() => AssertRoundTrip("42L\n");
    [Fact] public void FloatLiteral() => AssertRoundTrip("3.14\n");
    [Fact] public void StringLiteral() => AssertRoundTrip("\"hello world\"\n");
    [Fact] public void RawStringLiteral() => AssertRoundTrip("r\"hello\\nworld\"\n");
    [Fact] public void BytesLiteral() => AssertRoundTrip("b\"abc\"\n");
    [Fact] public void BooleanTrue() => AssertRoundTrip("True\n");
    [Fact] public void BooleanFalse() => AssertRoundTrip("False\n");
    [Fact] public void NoneLiteral() => AssertRoundTrip("None\n");
    [Fact] public void EllipsisLiteral() => AssertRoundTrip("...\n");
    [Fact] public void Identifier() => AssertRoundTrip("x\n");
    [Fact] public void ListLiteral() => AssertRoundTrip("[1, 2, 3]\n");
    [Fact] public void DictLiteral() => AssertRoundTrip("{\"a\": 1, \"b\": 2}\n");
    [Fact] public void SetLiteral() => AssertRoundTrip("{1, 2, 3}\n");
    [Fact] public void TupleLiteral() => AssertRoundTrip("(1, 2, 3)\n");
    [Fact] public void SingleElementTuple() => AssertRoundTrip("(1,)\n");
    [Fact] public void EmptyList() => AssertRoundTrip("[]\n");
    [Fact] public void EmptyDict() => AssertRoundTrip("{}\n");

    [Fact] public void BinaryAdd() => AssertRoundTrip("1 + 2\n");
    [Fact] public void BinaryMultiply() => AssertRoundTrip("a * b\n");
    [Fact] public void BinaryPower() => AssertRoundTrip("2 ** 3\n");
    [Fact] public void UnaryMinus() => AssertRoundTrip("-x\n");
    [Fact] public void UnaryNot() => AssertRoundTrip("not x\n");
    [Fact] public void BitwiseNot() => AssertRoundTrip("~x\n");
    [Fact] public void ComparisonChain() => AssertRoundTrip("a < b < c\n");
    [Fact] public void LogicalAndOr() => AssertRoundTrip("a and b or c\n");
    [Fact] public void InOperator() => AssertRoundTrip("x in items\n");
    [Fact] public void NotInOperator() => AssertRoundTrip("x not in items\n");
    [Fact] public void IsOperator() => AssertRoundTrip("x is None\n");
    [Fact] public void IsNotOperator() => AssertRoundTrip("x is not None\n");
    [Fact] public void NullCoalesce() => AssertRoundTrip("x ?? 0\n");
    [Fact] public void QuestionMarkPostfix() => AssertRoundTrip("x?\n");
    [Fact] public void QuestionMarkAfterCall() => AssertRoundTrip("foo()?\n");
    [Fact] public void QuestionMarkChained() => AssertRoundTrip("a?.b()?\n");
    [Fact] public void QuestionMarkInExpr() => AssertRoundTrip("x? + 1\n");
    [Fact] public void QuestionMarkDouble() => AssertRoundTrip("x??\n");
    [Fact] public void QuestionMarkWithCoalesce() => AssertRoundTrip("x??? y\n");
    [Fact] public void PipeForward() => AssertRoundTrip("x |> f\n");

    [Fact] public void MemberAccess() => AssertRoundTrip("obj.method\n");
    [Fact] public void NullConditionalAccess() => AssertRoundTrip("obj?.method\n");
    [Fact] public void IndexAccess() => AssertRoundTrip("items[0]\n");
    [Fact] public void SliceAccess() => AssertRoundTrip("items[1:3]\n");
    [Fact] public void SliceWithStep() => AssertRoundTrip("items[::2]\n");
    [Fact] public void FunctionCall() => AssertRoundTrip("f(x, y)\n");
    [Fact] public void FunctionCallKeywordArgs() => AssertRoundTrip("f(a, b=1)\n");
    [Fact] public void Parenthesized() => AssertRoundTrip("(a + b)\n");

    [Fact] public void ConditionalExpr() => AssertRoundTrip("x if cond else y\n");
    [Fact] public void Lambda() => AssertRoundTrip("lambda x, y: x + y\n");
    [Fact] public void SuperExpr() => AssertRoundTrip("super()\n");
    [Fact]
    public void StarExpr() => AssertRoundTrip(
        "def foo(*args):\n    pass\n");

    [Fact] public void ListComprehension() => AssertRoundTrip("[x for x in items]\n");
    [Fact] public void ListComprehensionWithIf() => AssertRoundTrip("[x for x in items if x > 0]\n");
    [Fact] public void SetComprehension() => AssertRoundTrip("{x for x in items}\n");
    [Fact] public void DictComprehension() => AssertRoundTrip("{k: v for k, v in items}\n");

    [Fact] public void Assignment() => AssertRoundTrip("x = 42\n");
    [Fact] public void AugmentedAssignment() => AssertRoundTrip("x += 1\n");
    [Fact] public void VariableDeclaration() => AssertRoundTrip("x: int = 42\n");
    [Fact] public void VariableDeclarationNoInit() => AssertRoundTrip("x: int\n");
    [Fact] public void PassStatement() => AssertRoundTrip("pass\n");
    [Fact] public void BreakStatement() => AssertRoundTrip("break\n");
    [Fact] public void ContinueStatement() => AssertRoundTrip("continue\n");
    [Fact] public void ReturnEmpty() => AssertRoundTrip("return\n");
    [Fact] public void ReturnValue() => AssertRoundTrip("return 42\n");
    [Fact] public void RaiseStatement() => AssertRoundTrip("raise ValueError(\"oops\")\n");
    [Fact] public void AssertStatement() => AssertRoundTrip("assert x > 0\n");
    [Fact] public void AssertWithMessage() => AssertRoundTrip("assert x > 0, \"must be positive\"\n");

    [Fact]
    public void IfStatement() => AssertRoundTrip(
        "if x > 0:\n    print(x)\n");

    [Fact]
    public void IfElseStatement() => AssertRoundTrip(
        "if x > 0:\n    print(x)\nelse:\n    print(0)\n");

    [Fact]
    public void IfElifElse() => AssertRoundTrip(
        "if x > 0:\n    print(\"pos\")\nelif x == 0:\n    print(\"zero\")\nelse:\n    print(\"neg\")\n");

    [Fact]
    public void WhileLoop() => AssertRoundTrip(
        "while x > 0:\n    x -= 1\n");

    [Fact]
    public void ForLoop() => AssertRoundTrip(
        "for i in range(10):\n    print(i)\n");

    [Fact]
    public void TryExcept() => AssertRoundTrip(
        "try:\n    f()\nexcept ValueError as e:\n    print(e)\n");

    [Fact]
    public void TryFinally() => AssertRoundTrip(
        "try:\n    f()\nfinally:\n    cleanup()\n");

    [Fact]
    public void WithStatement() => AssertRoundTrip(
        "with open(\"f\") as f:\n    print(f.read())\n");

    [Fact]
    public void FunctionDef() => AssertRoundTrip(
        "def foo(x: int) -> str:\n    return str(x)\n");

    [Fact]
    public void AsyncFunctionDef() => AssertRoundTrip(
        "async def foo():\n    pass\n");

    [Fact]
    public void ClassDef() => AssertRoundTrip(
        "class Foo:\n    pass\n");

    [Fact]
    public void ClassWithBase() => AssertRoundTrip(
        "class Foo(Bar):\n    pass\n");

    [Fact]
    public void DecoratedClass() => AssertRoundTrip(
        "@dataclass\nclass Foo:\n    x: int\n");

    [Fact]
    public void InterfaceDef() => AssertRoundTrip(
        "interface IFoo:\n    pass\n");

    [Fact]
    public void EnumDef() => AssertRoundTrip(
        "enum Color:\n    RED\n    GREEN\n    BLUE\n");

    [Fact]
    public void ImportStatement() => AssertRoundTrip("import os\n");
    [Fact]
    public void FromImport() => AssertRoundTrip("from os import path\n");
    [Fact]
    public void FromImportMultiple() => AssertRoundTrip("from os import path, getcwd\n");

    [Fact]
    public void MatchStatement() => AssertRoundTrip(
        "match x:\n    case 1:\n        print(\"one\")\n    case _:\n        print(\"other\")\n");

    [Fact]
    public void FString() => AssertRoundTrip("f\"hello {name}\"\n");

    [Fact]
    public void GenericFunction() => AssertRoundTrip(
        "def identity[T](x: T) -> T:\n    return x\n");

    [Fact]
    public void GenericClass() => AssertRoundTrip(
        "class Box[T]:\n    value: T\n");

    [Fact]
    public void PropertyDef() => AssertRoundTrip(
        "class Foo:\n    property get name(self) -> str:\n        return self._name\n");

    [Fact]
    public void UnionDef() => AssertRoundTrip(
        "union Option[T]:\n    case Some(value: T)\n    case NoneVal\n");

    [Fact]
    public void TypeAlias() => AssertRoundTrip("type IntList = list[int]\n");

    [Fact]
    public void StructDef() => AssertRoundTrip(
        "struct Point:\n    x: int\n    y: int\n");

    [Fact]
    public void PrecedenceParens() => AssertRoundTrip("(a + b) * c\n");

    [Fact]
    public void NestedBinaryOps() => AssertRoundTrip("a + b * c + d\n");

    [Fact]
    public void OptionalType() => AssertRoundTrip("x: int?\n");

    [Fact] public void TypeCoercion() => AssertRoundTrip("x to int\n");
    [Fact] public void TypeCheck() => AssertRoundTrip("x is int\n");
    [Fact] public void TypeCheckNot() => AssertRoundTrip("x is not int\n");
    [Fact] public void MultiAxisAccess() => AssertRoundTrip("arr[0, 1]\n");
    [Fact] public void MultiAxisSlice() => AssertRoundTrip("arr[0:5, 1:3]\n");
    [Fact] public void MultiAxisMixed() => AssertRoundTrip("arr[0, 1:3]\n");
}
