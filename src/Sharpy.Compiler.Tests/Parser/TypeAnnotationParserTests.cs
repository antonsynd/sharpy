using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for type annotation parsing: T?, T | None, and T !E syntax.
/// </summary>
public class TypeAnnotationParserTests
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

    private static TypeAnnotation ParseType(string typeSource)
    {
        var source = $"x: {typeSource}";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        return varDecl.Type!;
    }

    #region Basic Types

    [Fact]
    public void Parse_SimpleType_NoModifiers()
    {
        var type = ParseType("int");

        type.Name.Should().Be("int");
        type.IsOptional.Should().BeFalse();
        type.IsCSharpNullable.Should().BeFalse();
        type.IsResult.Should().BeFalse();
    }

    [Fact]
    public void Parse_GenericType_NoModifiers()
    {
        var type = ParseType("list[int]");

        type.Name.Should().Be("list");
        type.TypeArguments.Should().HaveCount(1);
        type.TypeArguments[0].Name.Should().Be("int");
    }

    #endregion

    #region Optional (T?) Syntax

    [Fact]
    public void Parse_OptionalType_SetsIsOptional()
    {
        var type = ParseType("int?");

        type.Name.Should().Be("int");
        type.IsOptional.Should().BeTrue();
        type.IsCSharpNullable.Should().BeFalse();
        type.IsResult.Should().BeFalse();
    }

    [Fact]
    public void Parse_OptionalGenericType_Works()
    {
        var type = ParseType("list[int]?");

        type.Name.Should().Be("list");
        type.IsOptional.Should().BeTrue();
        type.TypeArguments.Should().HaveCount(1);
    }

    #endregion

    #region C# Nullable (T | None) Syntax

    [Fact]
    public void Parse_CSharpNullable_SetsIsCSharpNullable()
    {
        var type = ParseType("str | None");

        type.Name.Should().Be("str");
        type.IsOptional.Should().BeFalse();
        type.IsCSharpNullable.Should().BeTrue();
        type.IsResult.Should().BeFalse();
    }

    [Fact]
    public void Parse_CSharpNullableGeneric_Works()
    {
        var type = ParseType("list[str] | None");

        type.Name.Should().Be("list");
        type.IsCSharpNullable.Should().BeTrue();
    }

    [Fact]
    public void Parse_FreeUnion_ThrowsError()
    {
        var act = () => ParseType("int | str");
        act.Should().Throw<ParserError>();
    }

    #endregion

    #region Result (T !E) Syntax

    [Fact]
    public void Parse_ResultType_SetsErrorType()
    {
        var type = ParseType("int !ValueError");

        type.Name.Should().Be("int");
        type.IsResult.Should().BeTrue();
        type.ErrorType.Should().NotBeNull();
        type.ErrorType!.Name.Should().Be("ValueError");
    }

    [Fact]
    public void Parse_ResultTypeGenericError_Works()
    {
        var type = ParseType("int !IOError[str]");

        type.IsResult.Should().BeTrue();
        type.ErrorType!.Name.Should().Be("IOError");
        type.ErrorType.TypeArguments.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_GenericResultType_Works()
    {
        var type = ParseType("list[int] !ParseError");

        type.Name.Should().Be("list");
        type.IsResult.Should().BeTrue();
        type.ErrorType!.Name.Should().Be("ParseError");
    }

    #endregion

    #region Combined Modifiers

    [Fact]
    public void Parse_ResultWithCSharpNullable_Works()
    {
        // int !ValueError | None -> Result[int, ValueError] | None
        var type = ParseType("int !ValueError | None");

        type.Name.Should().Be("int");
        type.IsResult.Should().BeTrue();
        type.IsCSharpNullable.Should().BeTrue();
        type.ErrorType!.Name.Should().Be("ValueError");
    }

    [Fact]
    public void Parse_Precedence_BangBindsTighterThanPipe()
    {
        // int !E | None should be (int !E) | None, not int !(E | None)
        var type = ParseType("int !ValueError | None");

        // The error type should be just "ValueError", not "ValueError | None"
        type.ErrorType!.IsCSharpNullable.Should().BeFalse();
        type.IsCSharpNullable.Should().BeTrue(); // The outer type is nullable
    }

    #endregion

    #region Position Tracking

    [Fact]
    public void Parse_ResultType_TracksPosition()
    {
        var type = ParseType("int !ValueError");

        type.LineStart.Should().BeGreaterThan(0);
        type.ErrorType.Should().NotBeNull();
    }

    #endregion
}
