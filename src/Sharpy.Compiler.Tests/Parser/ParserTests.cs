#pragma warning disable CS0618 // ParserError is obsolete
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;
using ParserError = Sharpy.Compiler.Parser.ParserError;

namespace Sharpy.Compiler.Tests.Parser;

public partial class ParserTests
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

    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeTrue("Expected parser to report an error for input: " + source);
        return string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message));
    }

    #region Literal Expressions

    [Fact]
    public void ParseIntegerLiteral()
    {
        var module = Parse("42");
        module.Body.Should().HaveCount(1);
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;
        literal.Value.Should().Be("42");
        literal.Suffix.Should().BeNull();
    }

    [Fact]
    public void ParseIntegerLiteralWithSuffix()
    {
        var module = Parse("42L");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<IntegerLiteral>().Subject;
        literal.Value.Should().Be("42");
        literal.Suffix.Should().Be("L");
    }

    [Fact]
    public void ParseFloatLiteral()
    {
        var module = Parse("3.14");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var literal = exprStmt.Expression.Should().BeOfType<FloatLiteral>().Subject;
        literal.Value.Should().Be("3.14");
    }

    [Fact]
    public void ParseStringLiteral()
    {
        var module = Parse("x = \"hello\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<StringLiteral>().Subject;
        literal.Value.Should().Be("hello");
        literal.IsRaw.Should().BeFalse();
    }

    [Fact]
    public void ParseRawStringLiteral()
    {
        var module = Parse("x = r\"hello\\n\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var literal = assignment.Value.Should().BeOfType<StringLiteral>().Subject;
        literal.Value.Should().Be("hello\\n");
        literal.IsRaw.Should().BeTrue();
    }

    [Fact]
    public void ParseFStringLiteral()
    {
        var module = Parse("x = f\"hello {name}\"");
        var assignment = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var fstring = assignment.Value.Should().BeOfType<FStringLiteral>().Subject;
        fstring.Parts.Should().HaveCount(2);
        fstring.Parts[0].Text.Should().Be("hello ");
        fstring.Parts[0].Expression.Should().BeNull();
        fstring.Parts[1].Text.Should().BeNull();
        fstring.Parts[1].Expression.Should().BeOfType<Identifier>();
    }

    [Fact]
    public void ParseBooleanLiterals()
    {
        var trueModule = Parse("True");
        var trueExpr = trueModule.Body[0].Should().BeOfType<ExpressionStatement>().Subject.Expression;
        trueExpr.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeTrue();

        var falseModule = Parse("False");
        var falseExpr = falseModule.Body[0].Should().BeOfType<ExpressionStatement>().Subject.Expression;
        falseExpr.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeFalse();
    }

    [Fact]
    public void ParseNoneLiteral()
    {
        var module = Parse("None");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<NoneLiteral>();
    }

    [Fact]
    public void ParseEllipsisLiteral()
    {
        var module = Parse("...");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<EllipsisLiteral>();
    }

    #endregion

    #region Collection Literals

    [Fact]
    public void ParseListLiteral()
    {
        var module = Parse("[1, 2, 3]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var list = exprStmt.Expression.Should().BeOfType<ListLiteral>().Subject;
        list.Elements.Should().HaveCount(3);
        list.Elements[0].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        list.Elements[1].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
        list.Elements[2].Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
    }

    [Fact]
    public void ParseEmptyList()
    {
        var module = Parse("[]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var list = exprStmt.Expression.Should().BeOfType<ListLiteral>().Subject;
        list.Elements.Should().BeEmpty();
    }

    [Fact]
    public void ParseDictLiteral()
    {
        var module = Parse("{\"a\": 1, \"b\": 2}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var dict = exprStmt.Expression.Should().BeOfType<DictLiteral>().Subject;
        dict.Entries.Should().HaveCount(2);
        dict.Entries[0].Key.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("a");
        dict.Entries[0].Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    [Fact]
    public void ParseEmptyDict()
    {
        var module = Parse("{}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        exprStmt.Expression.Should().BeOfType<DictLiteral>().Which.Entries.Should().BeEmpty();
    }

    [Fact]
    public void ParseSetLiteral()
    {
        var module = Parse("{1, 2, 3}");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var set = exprStmt.Expression.Should().BeOfType<SetLiteral>().Subject;
        set.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void ParseTupleLiteral()
    {
        var module = Parse("(1, 2, 3)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void ParseSingleElementTuple()
    {
        var module = Parse("(1,)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var tuple = exprStmt.Expression.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().HaveCount(1);
    }

    [Fact]
    public void ParseParenthesizedExpression_NotTuple()
    {
        var module = Parse("(1)");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var paren = exprStmt.Expression.Should().BeOfType<Parenthesized>().Subject;
        paren.Expression.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
    }

    #endregion

    #region Binary and Unary Operators

    [Fact]
    public void ParseBinaryAdd()
    {
        var module = Parse("1 + 2");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Add);
        binOp.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        binOp.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    [Fact]
    public void ParseBinaryMultiply()
    {
        var module = Parse("3 * 4");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var binOp = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        binOp.Operator.Should().Be(BinaryOperator.Multiply);
    }

    [Fact]
    public void ParseOperatorPrecedence()
    {
        var module = Parse("1 + 2 * 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var add = exprStmt.Expression.Should().BeOfType<BinaryOp>().Subject;
        add.Operator.Should().Be(BinaryOperator.Add);
        add.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");

        var mult = add.Right.Should().BeOfType<BinaryOp>().Subject;
        mult.Operator.Should().Be(BinaryOperator.Multiply);
        mult.Left.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
        mult.Right.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("3");
    }

    [Fact]
    public void ParseUnaryNot()
    {
        var module = Parse("not x");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Not);
        unary.Operand.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    }

    [Fact]
    public void ParseUnaryMinus()
    {
        var module = Parse("-5");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var unary = exprStmt.Expression.Should().BeOfType<UnaryOp>().Subject;
        unary.Operator.Should().Be(UnaryOperator.Minus);
        unary.Operand.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
    }

    [Fact]
    public void ParseComparisonChain()
    {
        var module = Parse("1 < 2 <= 3");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var chain = exprStmt.Expression.Should().BeOfType<ComparisonChain>().Subject;
        chain.Operands.Should().HaveCount(3);
        chain.Operators.Should().HaveCount(2);
        chain.Operators[0].Should().Be(ComparisonOperator.LessThan);
        chain.Operators[1].Should().Be(ComparisonOperator.LessThanOrEqual);
    }

    #endregion

    #region Member Access and Indexing

    [Fact]
    public void ParseMemberAccess()
    {
        var module = Parse("obj.field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("obj");
        member.Member.Should().Be("field");
        member.IsNullConditional.Should().BeFalse();
    }

    [Fact]
    public void ParseNullConditionalMemberAccess()
    {
        var module = Parse("obj?.field");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.IsNullConditional.Should().BeTrue();
    }

    [Fact]
    public void ParseIndexAccess()
    {
        var module = Parse("arr[0]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var index = exprStmt.Expression.Should().BeOfType<IndexAccess>().Subject;
        index.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("arr");
        index.Index.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("0");
    }

    [Fact]
    public void ParseSliceAccess()
    {
        var module = Parse("arr[1:5]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var slice = exprStmt.Expression.Should().BeOfType<SliceAccess>().Subject;
        slice.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("arr");
        slice.Start.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("1");
        slice.Stop.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
        slice.Step.Should().BeNull();
    }

    [Fact]
    public void ParseSliceWithStep()
    {
        var module = Parse("arr[::2]");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var slice = exprStmt.Expression.Should().BeOfType<SliceAccess>().Subject;
        slice.Start.Should().BeNull();
        slice.Stop.Should().BeNull();
        slice.Step.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("2");
    }

    #endregion

    #region Source Span Tracking

    [Fact]
    public void ParseIdentifier_TracksSpan()
    {
        var module = Parse("myVar");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var identifier = exprStmt.Expression.Should().BeOfType<Identifier>().Subject;

        identifier.Name.Should().Be("myVar");
        identifier.Span.Should().NotBeNull();
        identifier.Span!.Value.Start.Should().Be(0);
        identifier.Span.Value.Length.Should().Be(5);
        identifier.Span.Value.End.Should().Be(5);
    }

    [Fact]
    public void ParseIdentifier_WithIndentation_TracksCorrectSpan()
    {
        // Use a full statement context to avoid indentation issues
        var module = Parse("def test():\n    x");
        // Find the identifier 'x' in the function body
        var funcDef = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        var exprStmt = funcDef.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var identifier = exprStmt.Expression.Should().BeOfType<Identifier>().Subject;

        identifier.Name.Should().Be("x");
        identifier.Span.Should().NotBeNull();
        // Position should be at offset of "x" in "def test():\n    x"
        // That's 11 (def test():) + 1 (\n) + 4 (spaces) = 16
        identifier.Span!.Value.Start.Should().Be(16);
        identifier.Span.Value.Length.Should().Be(1);
    }

    #endregion

}
