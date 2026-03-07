using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Lsp.Handlers;
using Xunit;
using static Sharpy.Lsp.Handlers.SharplySemanticTokensHandler;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Tests for semantic token collection used by SharplySemanticTokensHandler.
/// Verifies token type assignment, modifier bits, and AST traversal coverage.
/// </summary>
public class SemanticTokensTests
{
    private readonly CompilerApi _api = new();

    private System.Collections.Generic.List<RawToken> CollectTokensFrom(string source)
    {
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue("source should compile without errors: {0}",
            string.Join("; ", analysis.Diagnostics.Select(d => d.Message)));
        analysis.Ast.Should().NotBeNull();

        var tokens = new System.Collections.Generic.List<RawToken>();
        SharplySemanticTokensHandler.CollectTokens(analysis.Ast!.Body, analysis, tokens);
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
        params_.Should().HaveCount(2, "a and b should be parameter tokens");
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
        // self should be skipped, only a should appear
        paramTokens.Should().HaveCount(1);
        paramTokens[0].Length.Should().Be(1, "parameter name 'a' has length 1");
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
        var analysis = _api.Analyze("x: int = 1\ndef main():\n    print(x)");
        analysis.Success.Should().BeTrue();

        var tokens = new System.Collections.Generic.List<RawToken>();
        SharplySemanticTokensHandler.CollectTokens(
            Enumerable.Empty<Sharpy.Compiler.Parser.Ast.Statement>(),
            analysis,
            tokens);
        tokens.Should().BeEmpty();
    }

    #endregion
}
