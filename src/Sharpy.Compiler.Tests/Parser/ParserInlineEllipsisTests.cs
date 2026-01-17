using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for Parser's handling of inline ellipsis syntax: def foo(): ...
/// </summary>
public class ParserInlineEllipsisTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void ParseFunctionDef_InlineEllipsis()
    {
        var source = "def area(self) -> float: ...";
        var module = Parse(source);

        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Name.Should().Be("area");
        func.Body.Should().HaveCount(1);
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseFunctionDef_InlineEllipsis_NoReturnType()
    {
        var source = "def do_something(self): ...";
        var module = Parse(source);

        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Name.Should().Be("do_something");
        func.ReturnType.Should().BeNull();
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseFunctionDef_InlineEllipsis_WithParameters()
    {
        var source = "def calculate(self, x: int, y: int) -> int: ...";
        var module = Parse(source);

        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Name.Should().Be("calculate");
        func.Parameters.Should().HaveCount(3);
        func.Parameters[0].Name.Should().Be("self");
        func.Parameters[1].Name.Should().Be("x");
        func.Parameters[2].Name.Should().Be("y");
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseFunctionDef_InlineEllipsis_EquivalentToMultiLine()
    {
        var inlineSource = "def area(self) -> float: ...";
        var multiLineSource = @"def area(self) -> float:
    ...";

        var inlineModule = Parse(inlineSource);
        var multiLineModule = Parse(multiLineSource);

        var inlineFunc = inlineModule.Body[0] as FunctionDef;
        var multiLineFunc = multiLineModule.Body[0] as FunctionDef;

        // Both should have the same body structure
        inlineFunc!.Body.Should().HaveCount(1);
        multiLineFunc!.Body.Should().HaveCount(1);

        inlineFunc.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
        multiLineFunc.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseClassDef_WithInlineEllipsisMethods()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...
    def perimeter(self) -> float: ...
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Body.Should().HaveCount(2);
        foreach (var stmt in classDef.Body)
        {
            var func = stmt.Should().BeOfType<FunctionDef>().Subject;
            func.Body[0].Should().BeOfType<ExpressionStatement>()
                .Which.Expression.Should().BeOfType<EllipsisLiteral>();
        }
    }

    [Fact]
    public void ParseInterfaceDef_WithInlineEllipsisMethods()
    {
        var source = @"
interface IDrawable:
    def draw(self) -> None: ...
    def get_bounds(self) -> tuple[float, float]: ...
";
        var module = Parse(source);
        var interfaceDef = module.Body[0].Should().BeOfType<InterfaceDef>().Subject;

        interfaceDef.Body.Should().HaveCount(2);
        foreach (var stmt in interfaceDef.Body)
        {
            var func = stmt.Should().BeOfType<FunctionDef>().Subject;
            func.Body[0].Should().BeOfType<ExpressionStatement>()
                .Which.Expression.Should().BeOfType<EllipsisLiteral>();
        }
    }

    [Fact]
    public void ParseFunctionDef_InlineEllipsis_WithTypeParameters()
    {
        var source = "def get[T](self, key: str) -> T: ...";
        var module = Parse(source);

        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Name.Should().Be("get");
        func.TypeParameters.Should().HaveCount(1);
        func.TypeParameters[0].Name.Should().Be("T");
        func.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    [Fact]
    public void ParseClassDef_MixedInlineAndMultiLineEllipsis()
    {
        var source = @"
@abstract
class Shape:
    def area(self) -> float: ...

    def perimeter(self) -> float:
        ...

    def describe(self) -> str:
        return ""shape""
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        classDef.Body.Should().HaveCount(3);

        // First method - inline ellipsis
        var areaFunc = classDef.Body[0] as FunctionDef;
        areaFunc!.Name.Should().Be("area");
        areaFunc.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();

        // Second method - multi-line ellipsis
        var perimeterFunc = classDef.Body[1] as FunctionDef;
        perimeterFunc!.Name.Should().Be("perimeter");
        perimeterFunc.Body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<EllipsisLiteral>();

        // Third method - real implementation
        var describeFunc = classDef.Body[2] as FunctionDef;
        describeFunc!.Name.Should().Be("describe");
        describeFunc.Body[0].Should().BeOfType<ReturnStatement>();
    }
}
