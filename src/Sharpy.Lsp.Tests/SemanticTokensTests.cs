using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using static Sharpy.Lsp.Handlers.SharpySemanticTokensHandler;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for semantic token collection used by SharpySemanticTokensHandler.
/// Verifies token type assignment, modifier bits, and AST traversal coverage.
/// </summary>
public class SemanticTokensTests
{
    private readonly CompilerApi _api = new();

    private System.Collections.Generic.List<RawToken> CollectTokensFrom(string source)
    {
        var parseResult = _api.Parse(source);
        parseResult.Success.Should().BeTrue("source should parse without errors: {0}",
            string.Join("; ", parseResult.Diagnostics.Select(d => d.Message)));
        parseResult.Ast.Should().NotBeNull();

        var tokens = new System.Collections.Generic.List<RawToken>();
        SharpySemanticTokensHandler.CollectTokens(parseResult.Ast!.Body, tokens);
        return tokens;
    }

    #region Token type assignment

    [Fact]
    public void Function_GetsTokenTypeFunction()
    {
        var tokens = CollectTokensFrom("def greet() -> str:\n    return \"hi\"\ndef main():\n    print(greet())");
        tokens.Should().Contain(t => t.TokenType == TFunction && t.Line == 0, "greet should be a function token");
        tokens.Should().Contain(t => t.TokenType == TFunction && t.Line == 2, "main should be a function token");
    }

    [Fact]
    public void Class_GetsTokenTypeClass()
    {
        var tokens = CollectTokensFrom("class Foo:\n    x: int = 0\ndef main():\n    f: Foo = Foo()\n    print(f.x)");
        tokens.Should().Contain(t => t.TokenType == TClass && t.Line == 0);
    }

    [Fact]
    public void Struct_GetsTokenTypeStruct()
    {
        var tokens = CollectTokensFrom("struct Point:\n    x: int\n    y: int\ndef main():\n    p: Point = Point(x=1, y=2)\n    print(p.x)");
        tokens.Should().Contain(t => t.TokenType == TStruct && t.Line == 0);
    }

    [Fact]
    public void Interface_GetsTokenTypeInterface()
    {
        var tokens = CollectTokensFrom(
            "interface Drawable:\n    def draw(self) -> str:\n        pass\n" +
            "class Circle(Drawable):\n    def draw(self) -> str:\n        return \"circle\"\n" +
            "def main():\n    c: Circle = Circle()\n    print(c.draw())");
        tokens.Should().Contain(t => t.TokenType == TInterface && t.Line == 0);
    }

    [Fact]
    public void Enum_GetsTokenTypeEnum()
    {
        var tokens = CollectTokensFrom("enum Color:\n    RED = 0\n    GREEN = 1\ndef main():\n    c: Color = Color.RED\n    print(c)");
        tokens.Should().Contain(t => t.TokenType == TEnum && t.Line == 0);
    }

    [Fact]
    public void EnumMember_GetsTokenTypeEnumMember()
    {
        var tokens = CollectTokensFrom("enum Color:\n    RED = 0\n    GREEN = 1\ndef main():\n    c: Color = Color.RED\n    print(c)");
        var members = tokens.Where(t => t.TokenType == TEnumMember).ToList();
        members.Should().HaveCount(2);
        members.Should().Contain(t => t.Line == 1, "RED on line 2 (0-based: 1)");
        members.Should().Contain(t => t.Line == 2, "GREEN on line 3 (0-based: 2)");
    }

    [Fact]
    public void Variable_GetsTokenTypeVariable()
    {
        var tokens = CollectTokensFrom("x: int = 42\ndef main():\n    print(x)");
        tokens.Should().Contain(t => t.TokenType == TVariable && t.Line == 0);
    }

    [Fact]
    public void Parameter_GetsTokenTypeParameter()
    {
        var tokens = CollectTokensFrom("def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    print(add(1, 2))");
        var params_ = tokens.Where(t => t.TokenType == TParameter).ToList();
        // 2 declaration tokens (a, b) + 2 usage-site tokens (a, b in return a + b)
        params_.Should().HaveCount(4, "a and b should have declaration and usage-site tokens");
    }

    [Fact]
    public void Property_GetsTokenTypeProperty()
    {
        var tokens = CollectTokensFrom(@"
class Box:
    _value: int
    def __init__(self, v: int):
        self._value = v
    property get value(self) -> int:
        return self._value

def main():
    b: Box = Box(1)
    print(b.value)
");
        tokens.Should().Contain(t => t.TokenType == TProperty);
    }

    #endregion

    #region Modifier bits

    [Fact]
    public void Function_HasDeclarationAndDefinitionModifiers()
    {
        var tokens = CollectTokensFrom("def greet() -> str:\n    return \"hi\"\ndef main():\n    print(greet())");
        var funcToken = tokens.First(t => t.TokenType == TFunction && t.Line == 0);
        (funcToken.Modifiers & ModDeclaration).Should().NotBe(0, "function should have declaration modifier");
        (funcToken.Modifiers & ModDefinition).Should().NotBe(0, "function should have definition modifier");
    }

    [Fact]
    public void AsyncFunction_HasAsyncModifier()
    {
        var tokens = CollectTokensFrom("async def fetch() -> int:\n    return 42\ndef main():\n    print(1)");
        var funcToken = tokens.First(t => t.TokenType == TFunction && t.Line == 0);
        (funcToken.Modifiers & ModAsync).Should().NotBe(0, "async function should have async modifier");
    }

    [Fact]
    public void StaticFunction_HasStaticModifier()
    {
        var tokens = CollectTokensFrom(@"
class Util:
    @static
    def helper() -> int:
        return 1

def main():
    print(Util.helper())
");
        var funcTokens = tokens.Where(t => t.TokenType == TFunction).ToList();
        funcTokens.Should().Contain(t => (t.Modifiers & ModStatic) != 0, "static method should have static modifier");
    }

    [Fact]
    public void ConstVariable_HasReadonlyModifier()
    {
        var tokens = CollectTokensFrom("const MAX: int = 100\ndef main():\n    print(MAX)");
        var varToken = tokens.First(t => t.TokenType == TVariable);
        (varToken.Modifiers & ModReadonly).Should().NotBe(0, "const should have readonly modifier");
        (varToken.Modifiers & ModDeclaration).Should().NotBe(0, "const should have declaration modifier");
    }

    [Fact]
    public void Class_HasDeclarationAndDefinitionModifiers()
    {
        var tokens = CollectTokensFrom("class Foo:\n    x: int = 0\ndef main():\n    f: Foo = Foo()\n    print(f.x)");
        var classToken = tokens.First(t => t.TokenType == TClass);
        (classToken.Modifiers & ModDeclaration).Should().NotBe(0);
        (classToken.Modifiers & ModDefinition).Should().NotBe(0);
    }

    [Fact]
    public void Parameter_HasDeclarationModifier()
    {
        var tokens = CollectTokensFrom("def add(a: int) -> int:\n    return a\ndef main():\n    print(add(1))");
        var paramToken = tokens.First(t => t.TokenType == TParameter);
        (paramToken.Modifiers & ModDeclaration).Should().NotBe(0);
    }

    [Fact]
    public void EnumMember_HasDeclarationModifier()
    {
        var tokens = CollectTokensFrom("enum Color:\n    RED = 0\ndef main():\n    c: Color = Color.RED\n    print(c)");
        var memberToken = tokens.First(t => t.TokenType == TEnumMember);
        (memberToken.Modifiers & ModDeclaration).Should().NotBe(0);
    }

    #endregion

    #region AST traversal coverage

    [Fact]
    public void IfStatement_TraversesAllBranches()
    {
        var tokens = CollectTokensFrom(@"
def main():
    x: int = 1
    if x > 0:
        y: int = 2
        print(y)
    elif x < 0:
        z: int = 3
        print(z)
    else:
        w: int = 4
        print(w)
");
        var varTokens = tokens.Where(t => t.TokenType == TVariable).ToList();
        varTokens.Should().HaveCountGreaterThanOrEqualTo(4, "x, y, z, w should all produce variable tokens");
    }

    [Fact]
    public void ForStatement_TraversesBody()
    {
        var tokens = CollectTokensFrom(@"
def main():
    for i in range(10):
        x: int = i
        print(x)
");
        tokens.Should().Contain(t => t.TokenType == TVariable, "variable inside for body should be found");
    }

    [Fact]
    public void WhileStatement_TraversesBody()
    {
        var tokens = CollectTokensFrom(@"
def main():
    i: int = 0
    while i < 10:
        j: int = i
        i = i + 1
    print(i)
");
        var varTokens = tokens.Where(t => t.TokenType == TVariable).ToList();
        varTokens.Should().HaveCountGreaterThanOrEqualTo(2, "i and j should be found");
    }

    [Fact]
    public void TryStatement_TraversesAllParts()
    {
        var tokens = CollectTokensFrom(@"
def main():
    try:
        x: int = 1
        print(x)
    except Exception:
        y: int = 2
        print(y)
    finally:
        z: int = 3
        print(z)
");
        var varTokens = tokens.Where(t => t.TokenType == TVariable).ToList();
        varTokens.Should().HaveCountGreaterThanOrEqualTo(3, "x, y, z should all be found");
    }

    [Fact]
    public void MatchStatement_TraversesCaseBodies()
    {
        var tokens = CollectTokensFrom(@"
def main():
    x: int = 1
    match x:
        case 1:
            y: int = 10
            print(y)
        case 2:
            z: int = 20
            print(z)
        case _:
            w: int = 30
            print(w)
");
        var varTokens = tokens.Where(t => t.TokenType == TVariable).ToList();
        varTokens.Should().HaveCountGreaterThanOrEqualTo(4, "x, y, z, w should all be found");
    }

    [Fact]
    public void ClassWithMethods_TraversesBody()
    {
        var tokens = CollectTokensFrom(@"
class Animal:
    name: str
    def __init__(self, name: str):
        self.name = name
    def speak(self) -> str:
        return self.name

def main():
    a: Animal = Animal(""dog"")
    print(a.speak())
");
        tokens.Should().Contain(t => t.TokenType == TClass && t.Line == 1, "Animal class");
        var methods = tokens.Where(t => t.TokenType == TFunction).ToList();
        methods.Should().HaveCountGreaterThanOrEqualTo(2, "__init__ and speak should be found");
    }

    [Fact]
    public void SelfParameter_IsSkipped()
    {
        var tokens = CollectTokensFrom(@"
class Foo:
    x: int = 0
    def method(self, a: int) -> int:
        return a + self.x

def main():
    f: Foo = Foo()
    print(f.method(1))
");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // self should be skipped; "a" declaration + "a" usage in "return a + self.x"
        paramTokens.Should().HaveCount(2);
        paramTokens.Should().OnlyContain(t => t.Length == 1, "all parameter tokens should be for 'a' with length 1");
    }

    [Fact]
    public void Decorator_GetsTokenTypeDecorator()
    {
        var tokens = CollectTokensFrom(@"
class Foo:
    @static
    def helper() -> int:
        return 1

def main():
    print(Foo.helper())
");
        tokens.Should().Contain(t => t.TokenType == TDecorator, "decorator should produce decorator token");
    }

    [Fact]
    public void Tokens_UseZeroBasedPositions()
    {
        var tokens = CollectTokensFrom("x: int = 1\ndef main():\n    print(x)");
        // x is at line 1 col 1 in compiler (1-based) -> line 0 col 0 in LSP (0-based)
        var varToken = tokens.First(t => t.TokenType == TVariable);
        varToken.Line.Should().Be(0, "first line should be 0 in LSP coordinates");
        varToken.Col.Should().Be(0, "first column should be 0 in LSP coordinates");
    }

    [Fact]
    public void Token_LengthMatchesNameLength()
    {
        var tokens = CollectTokensFrom("def greet() -> str:\n    return \"hi\"\ndef main():\n    print(greet())");
        var greetToken = tokens.First(t => t.TokenType == TFunction && t.Line == 0);
        greetToken.Length.Should().Be(5, "greet has 5 characters");

        var mainToken = tokens.First(t => t.TokenType == TFunction && t.Line == 2);
        mainToken.Length.Should().Be(4, "main has 4 characters");
    }

    [Fact]
    public void EmptyStatementList_ProducesNoTokens()
    {
        var tokens = new System.Collections.Generic.List<RawToken>();
        SharpySemanticTokensHandler.CollectTokens(
            Enumerable.Empty<Sharpy.Compiler.Parser.Ast.Statement>(),
            tokens);
        tokens.Should().BeEmpty();
    }

    #endregion

    #region Keyword tokens for operator-keywords

    [Fact]
    public void NotKeyword_GetsTokenTypeKeyword()
    {
        var tokens = CollectTokensFrom("def main():\n    x: bool = not True\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'not' should produce a keyword token");
        keywords[0].Length.Should().Be(3, "not has 3 characters");
    }

    [Fact]
    public void AndKeyword_GetsTokenTypeKeyword()
    {
        var tokens = CollectTokensFrom("def main():\n    x: bool = True and False\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'and' should produce a keyword token");
        keywords[0].Length.Should().Be(3, "and has 3 characters");
    }

    [Fact]
    public void OrKeyword_GetsTokenTypeKeyword()
    {
        var tokens = CollectTokensFrom("def main():\n    x: bool = True or False\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'or' should produce a keyword token");
        keywords[0].Length.Should().Be(2, "or has 2 characters");
    }

    [Fact]
    public void InKeyword_InBinaryOp_GetsTokenTypeKeyword()
    {
        var tokens = CollectTokensFrom("def main():\n    items: list[int] = [1, 2, 3]\n    x: bool = 1 in items\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'in' should produce a keyword token");
        keywords[0].Length.Should().Be(2, "in has 2 characters");
    }

    [Fact]
    public void NotKeyword_InUnaryExpression_HasCorrectPosition()
    {
        // "def main():\n    x: bool = not True"
        // "not" starts at line 2 (1-based), column 15 (1-based) -> LSP line 1, col 14
        var tokens = CollectTokensFrom("def main():\n    x: bool = not True\n    print(x)");
        var keyword = tokens.First(t => t.TokenType == TKeyword);
        keyword.Line.Should().Be(1, "not is on the second line (0-based)");
        keyword.Col.Should().Be(14, "not starts at column 15 (1-based) -> 14 (0-based)");
    }

    [Fact]
    public void MultipleKeywords_InComplexExpression()
    {
        var tokens = CollectTokensFrom("def main():\n    x: bool = not True and not False\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        // "not" (first), "and", "not" (second)
        keywords.Should().HaveCount(3, "not, and, not should all produce keyword tokens");
    }

    [Fact]
    public void AndKeyword_MultiLine_HasCorrectPosition()
    {
        // "and" on a different line, using backslash line continuation
        // Line 2: "    x: bool = True \"
        // Line 3: "        and False"
        // "and" is at line 3, col 9 (1-based) -> LSP (2, 8)
        var tokens = CollectTokensFrom(
            "def main():\n    x: bool = True \\\n        and False\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'and' should produce a keyword token");
        keywords[0].Line.Should().Be(2, "and is on line 3 (0-based: 2)");
        keywords[0].Col.Should().Be(8, "and starts at column 9 (0-based: 8)");
        keywords[0].Length.Should().Be(3);
    }

    [Fact]
    public void OrKeyword_MultiLine_HasCorrectPosition()
    {
        // "or" on a different line, using backslash line continuation
        // Line 2: "    x: bool = True \"
        // Line 3: "        or False"
        // "or" is at line 3, col 9 (1-based) -> LSP (2, 8)
        var tokens = CollectTokensFrom(
            "def main():\n    x: bool = True \\\n        or False\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'or' should produce a keyword token");
        keywords[0].Line.Should().Be(2, "or is on line 3 (0-based: 2)");
        keywords[0].Col.Should().Be(8, "or starts at column 9 (0-based: 8)");
        keywords[0].Length.Should().Be(2);
    }

    [Fact]
    public void InKeyword_MultiLine_HasCorrectPosition()
    {
        // "in" on a different line, using backslash line continuation
        // Line 2: "    items: list[int] = [1, 2, 3]"
        // Line 3: "    x: bool = 1 \"
        // Line 4: "        in items"
        // "in" is at line 4, col 9 (1-based) -> LSP (3, 8)
        var tokens = CollectTokensFrom(
            "def main():\n    items: list[int] = [1, 2, 3]\n    x: bool = 1 \\\n        in items\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().ContainSingle("'in' should produce a keyword token");
        keywords[0].Line.Should().Be(3, "in is on line 4 (0-based: 3)");
        keywords[0].Col.Should().Be(8, "in starts at column 9 (0-based: 8)");
        keywords[0].Length.Should().Be(2);
    }

    [Fact]
    public void NotInKeyword_MultiLine_HasCorrectPositions()
    {
        // "not in" on a different line, using backslash line continuation
        // Line 3: "    x: bool = 1 \"
        // Line 4: "        not in items"
        // "not" at (4, 9) -> LSP (3, 8), "in" at (4, 13) -> LSP (3, 12)
        var tokens = CollectTokensFrom(
            "def main():\n    items: list[int] = [1, 2, 3]\n    x: bool = 1 \\\n        not in items\n    print(x)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().HaveCount(2, "'not' and 'in' should each produce a keyword token");
        var notToken = keywords.First(t => t.Length == 3);
        var inToken = keywords.First(t => t.Length == 2);
        notToken.Line.Should().Be(3, "not is on line 4 (0-based: 3)");
        notToken.Col.Should().Be(8, "not starts at column 9 (0-based: 8)");
        inToken.Line.Should().Be(3, "in is on line 4 (0-based: 3)");
        inToken.Col.Should().Be(12, "in starts at column 13 (0-based: 12)");
    }

    [Fact]
    public void IsNotKeyword_MultiLine_HasCorrectPositions()
    {
        // "is not" on a different line, using backslash line continuation
        // Line 2: "    x: int = 5"
        // Line 3: "    y: bool = x \"
        // Line 4: "        is not None"
        // "is" at (4, 9) -> LSP (3, 8), "not" at (4, 12) -> LSP (3, 11)
        var tokens = CollectTokensFrom(
            "def main():\n    x: int = 5\n    y: bool = x \\\n        is not None\n    print(y)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keywords.Should().HaveCount(2, "'is' and 'not' should each produce a keyword token");
        var isToken = keywords.First(t => t.Length == 2);
        var notToken = keywords.First(t => t.Length == 3);
        isToken.Line.Should().Be(3, "is is on line 4 (0-based: 3)");
        isToken.Col.Should().Be(8, "is starts at column 9 (0-based: 8)");
        notToken.Line.Should().Be(3, "not is on line 4 (0-based: 3)");
        notToken.Col.Should().Be(11, "not starts at column 12 (0-based: 11)");
    }

    [Fact]
    public void AndKeyword_SingleLine_RegressionPosition()
    {
        // Single-line: ensure stored positions work for same-line too
        // "    x: bool = True and False"
        // "and" at (2, 20) -> LSP (1, 19)
        var tokens = CollectTokensFrom("def main():\n    x: bool = True and False\n    print(x)");
        var keyword = tokens.Where(t => t.TokenType == TKeyword).ToList();
        keyword.Should().ContainSingle();
        keyword[0].Line.Should().Be(1, "and is on line 2 (0-based: 1)");
        keyword[0].Col.Should().Be(19, "and starts at column 20 (0-based: 19)");
        keyword[0].Length.Should().Be(3);
    }

    #endregion

    #region Parameter usage-site tokens

    [Fact]
    public void ParameterUsageSite_GetsTokenTypeParameter()
    {
        var tokens = CollectTokensFrom("def foo(id: int) -> int:\n    return id\ndef main():\n    print(foo(1))");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // declaration of id + usage of id in return
        paramTokens.Should().HaveCount(2, "id should have declaration and usage-site tokens");
    }

    [Fact]
    public void ParameterUsageSite_InCondition_GetsTokenTypeParameter()
    {
        var tokens = CollectTokensFrom("def check(x: int) -> bool:\n    if x > 0:\n        return True\n    return False\ndef main():\n    print(check(1))");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // declaration of x + usage in "if x > 0" + usage in neither (x is in a conditional expression)
        paramTokens.Should().HaveCountGreaterThanOrEqualTo(2, "x should appear at declaration and at least one usage site");
    }

    [Fact]
    public void ParameterUsageSite_InFunctionCall_GetsTokenTypeParameter()
    {
        var tokens = CollectTokensFrom("def greet(name: str) -> str:\n    return name.upper()\ndef main():\n    print(greet(\"test\"))");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // declaration + usage in name.upper()
        paramTokens.Should().HaveCount(2, "name should have declaration and usage-site tokens");
    }

    [Fact]
    public void ParameterUsageSite_MultipleParams_GetsTokenTypeParameter()
    {
        var tokens = CollectTokensFrom("def add(a: int, b: int) -> int:\n    return a + b\ndef main():\n    print(add(1, 2))");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // a declaration + b declaration + a usage + b usage
        paramTokens.Should().HaveCount(4, "both parameters should have declaration and usage tokens");
    }

    [Fact]
    public void ParameterUsageSite_DoesNotLeakBetweenFunctions()
    {
        var tokens = CollectTokensFrom(
            "def foo(x: int) -> int:\n    return x\n" +
            "def bar(y: int) -> int:\n    return y\n" +
            "def main():\n    print(foo(1))");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter).ToList();
        // foo: x declaration + x usage; bar: y declaration + y usage
        paramTokens.Should().HaveCount(4, "each function's parameters should be tracked independently");
    }

    #endregion

    #region String literal tokens

    [Fact]
    public void StringLiteral_GetsTokenTypeString()
    {
        var tokens = CollectTokensFrom("def main():\n    x: str = \"hello\"");
        tokens.Should().Contain(t => t.TokenType == TString, "string literal should get string token type");
    }

    #endregion

    #region ModifiedArgument tokens

    [Fact]
    public void ModifiedArgument_EmitsKeywordToken()
    {
        // Line 1: "def increment(x: ref int):"
        // Line 2: "    x = x + 1"
        // Line 3: "def main():"
        // Line 4: "    a: int = 5"
        // Line 5: "    increment(ref a)"
        // The call-site "ref" in "increment(ref a)" should emit a TKeyword token.
        // Line 5: "    increment(ref a)"
        //          123456789012345678
        // "ref" starts at column 15 (1-based) -> LSP (4, 14)
        var tokens = CollectTokensFrom(
            "def increment(x: ref int):\n    x = x + 1\ndef main():\n    a: int = 5\n    increment(ref a)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword && t.Line == 4).ToList();
        keywords.Should().Contain(t => t.Length == 3,
            "'ref' at the call site should produce a keyword token with length 3");
    }

    [Fact]
    public void ModifiedArgument_OutModifier_EmitsKeywordToken()
    {
        // Line 1: "def try_parse(s: str, result: out int) -> bool:"
        // Line 2: "    result = int(s)"
        // Line 3: "    return True"
        // Line 4: "def main():"
        // Line 5: "    value: int = 0"
        // Line 6: "    try_parse(\"42\", out value)"
        // The call-site "out" should emit a TKeyword token with length 3.
        var tokens = CollectTokensFrom(
            "def try_parse(s: str, result: out int) -> bool:\n" +
            "    result = int(s)\n" +
            "    return True\n" +
            "def main():\n" +
            "    value: int = 0\n" +
            "    try_parse(\"42\", out value)");
        var keywordsOnCallLine = tokens.Where(t => t.TokenType == TKeyword && t.Line == 5).ToList();
        keywordsOnCallLine.Should().Contain(t => t.Length == 3,
            "'out' at the call site should produce a keyword token with length 3");
    }

    [Fact]
    public void ModifiedArgument_InModifier_EmitsKeywordToken()
    {
        var tokens = CollectTokensFrom(
            "def process(x: in int):\n    print(x)\ndef main():\n    a: int = 5\n    process(in a)");
        var keywords = tokens.Where(t => t.TokenType == TKeyword && t.Line == 4).ToList();
        keywords.Should().Contain(t => t.Length == 2,
            "'in' at the call site should produce a keyword token with length 2");
    }

    [Fact]
    public void ModifiedArgument_OutInlineDeclaration_EmitsKeywordAndTypeTokens()
    {
        var tokens = CollectTokensFrom(
            "def try_parse(s: str, result: out int) -> bool:\n" +
            "    result = int(s)\n" +
            "    return True\n" +
            "def main():\n" +
            "    try_parse(\"42\", out value: int)");
        var keywordsOnCallLine = tokens.Where(t => t.TokenType == TKeyword && t.Line == 4).ToList();
        keywordsOnCallLine.Should().Contain(t => t.Length == 3,
            "'out' at the call site should produce a keyword token with length 3");
        var typeTokensOnCallLine = tokens.Where(t => t.TokenType == TType && t.Line == 4).ToList();
        typeTokensOnCallLine.Should().Contain(t => t.Length == 3,
            "'int' inline type annotation should produce a type token");
    }

    #endregion

    #region LambdaExpression tokens

    [Fact]
    public void LambdaExpression_EmitsParameterAndTypeTokens()
    {
        // "def main():" on line 1
        // "    f = (x: int, y: str) -> x" on line 2
        // Lambda parameters x, y should produce TParameter tokens.
        // Type annotations int, str should produce TType tokens.
        // Line 2: "    f = (x: int, y: str) -> x"
        //          123456789012345678901234567890
        // 'x' at col 10 (1-based) -> LSP (1, 9)
        // 'int' at col 13 (1-based) -> LSP (1, 12)
        // 'y' at col 18 (1-based) -> LSP (1, 17)
        // 'str' at col 21 (1-based) -> LSP (1, 20)
        var tokens = CollectTokensFrom("def main():\n    f = (x: int, y: str) -> x");
        var paramTokens = tokens.Where(t => t.TokenType == TParameter && t.Line == 1).ToList();
        paramTokens.Should().Contain(t => t.Length == 1 && t.Col == 9,
            "'x' lambda parameter should produce a parameter token");
        paramTokens.Should().Contain(t => t.Length == 1 && t.Col == 17,
            "'y' lambda parameter should produce a parameter token");

        var typeTokens = tokens.Where(t => t.TokenType == TType && t.Line == 1).ToList();
        typeTokens.Should().Contain(t => t.Length == 3 && t.Col == 12,
            "'int' type annotation should produce a type token");
        typeTokens.Should().Contain(t => t.Length == 3 && t.Col == 20,
            "'str' type annotation should produce a type token");
    }

    #endregion
}
