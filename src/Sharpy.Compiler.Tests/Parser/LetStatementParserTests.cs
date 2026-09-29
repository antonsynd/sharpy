using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// P21a (#1974): <c>let</c> is a keyword, and <c>let &lt;target&gt;[: T] = e</c> /
/// <c>out let x[: T]</c> parse to the existing store nodes carrying <c>IsLet</c> — never to a new
/// node (Design Decision 1). The matrix is spelling × asserted AST shape (node type, <c>IsLet</c>,
/// canonical target, statement position at the <c>let</c> token, name position at the name), and
/// every other <c>let</c> spelling × asserted diagnostic code and <c>@line:col</c>.
///
/// <para>The parser is the one construction site: every statement dispatcher (plain, <c>@suppress</c>,
/// inline <c>defer</c>, keyword-qualifier, <c>try</c>/<c>maybe</c> expression) ends in
/// <c>ParseSimpleStatement</c>, so the <c>@suppress</c> and <c>defer</c> spellings below are the
/// proof that no dispatcher needed its own arm.</para>
/// </summary>
public class LetStatementParserTests
{
    private static (Module Module, ImmutableArray<CompilerDiagnostic> Errors) ParseWithErrors(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the source must lex cleanly: " + source);
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        return (module, parser.Diagnostics.GetErrors().ToImmutableArray());
    }

    private static Module ParseClean(string source)
    {
        var (module, errors) = ParseWithErrors(source);
        errors.Should().BeEmpty("the source must parse: " + string.Join(" | ", errors.Select(e => $"{e.Code} {e.Message} @{e.Line}:{e.Column}")));
        AstValidator.ValidateTree(module);
        return module;
    }

    private static Statement Only(Module module)
    {
        module.Body.Should().ContainSingle();
        return module.Body[0];
    }

    private static void AssertAt(Node node, int line, int column)
    {
        node.LineStart.Should().Be(line);
        node.ColumnStart.Should().Be(column);
    }

    private static Identifier AssertName(Expression expr, string name, int line, int column)
    {
        var id = expr.Should().BeOfType<Identifier>().Subject;
        id.Name.Should().Be(name);
        AssertAt(id, line, column);
        return id;
    }

    #region Spelling × AST shape

    [Fact]
    public void LetName_IsAnAssignmentWithIsLet_AtTheLetToken()
    {
        var assign = Only(ParseClean("let x = 5\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        assign.Operator.Should().Be(AssignmentOperator.Assign);
        AssertAt(assign, 1, 1);
        AssertName(assign.Target, "x", 1, 5);
        assign.Value.Should().BeOfType<IntegerLiteral>();
    }

    [Fact]
    public void KeywordlessName_IsNotLet_Control()
    {
        // Positive control for every IsLet assertion in this class: the same store without the
        // keyword parses to the same node with the flag clear, starting at the target.
        var assign = Only(ParseClean("x = 5\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeFalse();
        AssertAt(assign, 1, 1);
        AssertName(assign.Target, "x", 1, 1);

        var decl = Only(ParseClean("x: int = 5\n")).Should().BeOfType<VariableDeclaration>().Subject;
        decl.IsLet.Should().BeFalse();
    }

    [Fact]
    public void LetAnnotated_IsAVariableDeclarationWithIsLet_NamePositionAtTheName()
    {
        var decl = Only(ParseClean("let x: int = 5\n")).Should().BeOfType<VariableDeclaration>().Subject;
        decl.IsLet.Should().BeTrue();
        decl.IsConst.Should().BeFalse();
        decl.Name.Should().Be("x");
        AssertAt(decl, 1, 1);
        decl.NameLineStart.Should().Be(1);
        decl.NameColumnStart.Should().Be(5);
        decl.NameColumnEnd.Should().Be(6);
        decl.Type!.Name.Should().Be("int");
        decl.InitialValue.Should().BeOfType<IntegerLiteral>();
    }

    [Fact]
    public void LetTuple_IsAnAssignmentWithIsLet_OverATupleOfNames()
    {
        var assign = Only(ParseClean("let a, b = 1, 2\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 1, 1);
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        tuple.Elements.Should().HaveCount(2);
        AssertName(tuple.Elements[0], "a", 1, 5);
        AssertName(tuple.Elements[1], "b", 1, 8);
    }

    [Fact]
    public void LetNestedTuple_KeepsTheNestedTupleOfNames()
    {
        var assign = Only(ParseClean("let a, (b, c) = 1, (2, 3)\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 1, 1);
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        AssertName(tuple.Elements[0], "a", 1, 5);
        var inner = tuple.Elements[1].Should().BeOfType<TupleLiteral>().Subject;
        AssertName(inner.Elements[0], "b", 1, 9);
        AssertName(inner.Elements[1], "c", 1, 12);
    }

    [Fact]
    public void LetStarredTuple_StarLast_IsAStarExpressionOverAName()
    {
        var assign = Only(ParseClean("let first, *rest = xs\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 1, 1);
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        AssertName(tuple.Elements[0], "first", 1, 5);
        var star = tuple.Elements[1].Should().BeOfType<StarExpression>().Subject;
        AssertAt(star, 1, 12);
        AssertName(star.Operand, "rest", 1, 13);
    }

    [Fact]
    public void LetStarredTuple_StarFirst_IsAStarExpressionOverAName()
    {
        var assign = Only(ParseClean("let *init, last = xs\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 1, 1);
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        var star = tuple.Elements[0].Should().BeOfType<StarExpression>().Subject;
        AssertName(star.Operand, "init", 1, 6);
        AssertName(tuple.Elements[1], "last", 1, 12);
    }

    [Fact]
    public void LetParenthesizedName_CanonicalizesToTheName()
    {
        // `(x)` is a redundant group; the store target is canonical (#1170), so `let (x) = 5`
        // binds `x` exactly like `let x = 5`.
        var assign = Only(ParseClean("let (x) = 5\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 1, 1);
        AssertName(assign.Target, "x", 1, 6);
    }

    [Fact]
    public void LetListDisplayTarget_CanonicalizesToATupleOfNames()
    {
        var assign = Only(ParseClean("let [a, b] = xs\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        var tuple = assign.Target.Should().BeOfType<TupleLiteral>().Subject;
        tuple.IsListDisplay.Should().BeTrue();
        AssertName(tuple.Elements[0], "a", 1, 6);
        AssertName(tuple.Elements[1], "b", 1, 9);
    }

    [Fact]
    public void LetBacktickedLet_BindsTheIdentifierLet()
    {
        // The backtick escape is the one way to name an identifier `let` (positive control for the
        // keyword refusals below).
        var assign = Only(ParseClean("let `let` = 5\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        var id = AssertName(assign.Target, "let", 1, 5);
        id.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void KeywordlessBacktickedLet_IsAnOrdinaryStore()
    {
        var assign = Only(ParseClean("`let` = 5\n")).Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeFalse();
        AssertName(assign.Target, "let", 1, 1).IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void SuppressDecoratedLet_WrapsTheLetAssignment()
    {
        var decorated = Only(ParseClean("@suppress(SPY0451)\nlet x = 5\n")).Should().BeOfType<DecoratedStatement>().Subject;
        var assign = decorated.Statement.Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 2, 1);
        AssertName(assign.Target, "x", 2, 5);
    }

    [Fact]
    public void SuppressDecoratedAnnotatedLet_CarriesTheDecorator()
    {
        var decl = Only(ParseClean("@suppress(SPY0451)\nlet x: int = 5\n")).Should().BeOfType<VariableDeclaration>().Subject;
        decl.IsLet.Should().BeTrue();
        decl.Decorators.Should().ContainSingle();
        AssertAt(decl, 2, 1);
        decl.NameColumnStart.Should().Be(5);
    }

    [Fact]
    public void InlineDeferLet_ParsesThroughTheSameConstructionSite()
    {
        var func = Only(ParseClean("def main():\n    defer let x = 5\n")).Should().BeOfType<FunctionDef>().Subject;
        var defer = func.Body.Should().ContainSingle().Subject.Should().BeOfType<DeferStatement>().Subject;
        var assign = defer.Body.Should().ContainSingle().Subject.Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 2, 11);
        AssertName(assign.Target, "x", 2, 15);
    }

    [Fact]
    public void LetInANestedBlock_PositionsAreTheLetTokenAndTheName()
    {
        var func = Only(ParseClean("def main():\n    if True:\n        let x = 5\n")).Should().BeOfType<FunctionDef>().Subject;
        var ifStmt = func.Body.Should().ContainSingle().Subject.Should().BeOfType<IfStatement>().Subject;
        var assign = ifStmt.ThenBody.Should().ContainSingle().Subject.Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeTrue();
        AssertAt(assign, 3, 9);
        AssertName(assign.Target, "x", 3, 13);
    }

    private static ModifiedArgument OnlyArgument(string source)
    {
        var stmt = Only(ParseClean(source)).Should().BeOfType<ExpressionStatement>().Subject;
        var call = stmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        return call.Arguments.Should().ContainSingle().Subject.Should().BeOfType<ModifiedArgument>().Subject;
    }

    [Fact]
    public void OutLet_IsAnInlineDeclarationWithoutAType()
    {
        var arg = OnlyArgument("f(out let x)\n");
        arg.IsLet.Should().BeTrue();
        arg.Modifier.Should().Be(ParameterModifier.Out);
        arg.InlineName.Should().Be("x");
        arg.InlineType.Should().BeNull();
        AssertAt(arg, 1, 3);
        AssertName(arg.Argument, "x", 1, 11);
    }

    [Fact]
    public void OutLetAnnotated_IsAnInlineDeclarationWithTheType()
    {
        var arg = OnlyArgument("f(out let x: int)\n");
        arg.IsLet.Should().BeTrue();
        arg.InlineName.Should().Be("x");
        arg.InlineType!.Name.Should().Be("int");
        AssertAt(arg, 1, 3);
        AssertName(arg.Argument, "x", 1, 11);
    }

    [Fact]
    public void OutLetBacktickedName_KeepsTheEscape()
    {
        var arg = OnlyArgument("f(out let `let`)\n");
        arg.IsLet.Should().BeTrue();
        arg.InlineName.Should().Be("let");
        arg.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void OutInlineDeclarationWithoutLet_IsUnchanged_Control()
    {
        var annotated = OnlyArgument("f(out x: int)\n");
        annotated.IsLet.Should().BeFalse();
        annotated.InlineName.Should().Be("x");

        var bare = OnlyArgument("f(out x)\n");
        bare.IsLet.Should().BeFalse();
        bare.InlineName.Should().BeNull();
    }

    #endregion

    #region Refusals × code and position

    /// <summary>
    /// Every <c>let</c> spelling that is not a declaration, and every identifier position that
    /// used to accept the bare name <c>let</c> (the only programs this change newly rejects).
    /// Each row: source, code, line, column of the FIRST error.
    /// </summary>
    public static TheoryData<string, string, string, int, int> Refusals => new()
    {
        // label, source, code, line, col
        { "let without initializer", "let x\n", "SPY0104", 1, 6 },
        { "annotated let without initializer", "let x: int\n", "SPY0104", 1, 11 },
        { "tuple let without initializer", "let a, b\n", "SPY0104", 1, 9 },
        { "let with augmented operator", "let x += 1\n", "SPY0100", 1, 7 },
        { "tuple let with augmented operator", "let a, b += 1, 2\n", "SPY0100", 1, 10 },
        { "annotated let with augmented operator", "let x: int += 1\n", "SPY0100", 1, 12 },
        { "let with walrus", "let x := 1\n", "SPY0100", 1, 5 },
        { "let member target", "let x.y = 1\n", "SPY0107", 1, 5 },
        { "let index target", "let xs[0] = 1\n", "SPY0107", 1, 5 },
        { "let self member target", "let self.x = 1\n", "SPY0107", 1, 5 },
        { "let call target", "let f() = 1\n", "SPY0107", 1, 5 },
        { "let tuple with a member element", "let a, b.c = 1, 2\n", "SPY0107", 1, 8 },
        { "annotated let member target", "let x.y: int = 1\n", "SPY0107", 1, 5 },
        { "annotated let tuple target", "let a, b: int = 1, 2\n", "SPY0107", 1, 9 },
        { "bare identifier let assigned", "let = 5\n", "SPY0101", 1, 1 },
        { "bare let", "let\n", "SPY0101", 1, 1 },
        { "let followed by a literal", "let 5 = x\n", "SPY0101", 1, 1 },
        { "static-decorated let", "@static\nlet x = 1\n", "SPY0105", 2, 1 },
        { "ref let", "f(ref let x)\n", "SPY0100", 1, 7 },
        { "in let", "f(in let x)\n", "SPY0100", 1, 6 },
        { "out let without a name", "f(out let)\n", "SPY0104", 1, 10 },
        { "out let with a literal", "f(out let 5)\n", "SPY0104", 1, 11 },
        { "out let with a dotted backticked name", "f(out let `a.b`)\n", "SPY0104", 1, 11 },
        { "let as a parameter name", "def f(let: int) -> None:\n    pass\n", "SPY0101", 1, 7 },
        { "let as a keyword-argument name", "f(let=1)\n", "SPY0100", 1, 3 },
        { "let as a for target", "for let in xs:\n    pass\n", "SPY0100", 1, 5 },
        { "let read as an expression", "print(let)\n", "SPY0100", 1, 7 },
        { "let as an import alias", "import os as let\n", "SPY0101", 1, 14 },
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void LetRefusal_ReportsTheNamedCodeAtItsPosition(string label, string source, string code, int line, int column)
    {
        _ = label;
        var (_, errors) = ParseWithErrors(source);
        errors.Should().NotBeEmpty();
        var first = errors[0];
        (first.Code, first.Line, first.Column).Should().Be((code, line, column),
            "first error: " + first.Message);
    }

    [Fact]
    public void LetRefusalMessages_NameTheRule()
    {
        static string FirstMessage(string source) => ParseWithErrors(source).Errors[0].Message;

        FirstMessage("let x\n").Should().Contain("'let' requires an initializer");
        FirstMessage("let x += 1\n").Should().Contain("'let' takes '=' only");
        FirstMessage("let x.y = 1\n").Should().Contain("'let' target must be a name or a tuple of names");
        FirstMessage("let = 5\n").Should().Contain("write `let` in backticks");
        FirstMessage("f(ref let x)\n").Should().Contain("'ref let' is not valid");
        FirstMessage("f(out let 5)\n").Should().Contain("'out let' requires a name");
    }

    [Fact]
    public void LetDotMember_IsTheKeywordQualifierRule_AnIdentifierNamedLet()
    {
        // `let.x = 1` is claimed by the keyword-qualifier rule (#1091) before ParseSimpleStatement
        // sees a Let token: it is a store to member `x` of the IDENTIFIER `let`, never a `let`
        // statement — so it stays SPY0200 (undefined `let`), exactly as before `let` was a keyword.
        var func = Only(ParseClean("def main():\n    let.x = 1\n")).Should().BeOfType<FunctionDef>().Subject;
        var assign = func.Body.Should().ContainSingle().Subject.Should().BeOfType<Assignment>().Subject;
        assign.IsLet.Should().BeFalse();
        var member = assign.Target.Should().BeOfType<MemberAccess>().Subject;
        member.Member.Should().Be("x");
        AssertName(member.Object, "let", 2, 5);

        var analysis = new CompilerApi().Analyze("def main():\n    let.x = 1\n");
        var error = analysis.Diagnostics.Where(d => d.IsError).Should().ContainSingle().Subject;
        (error.Code, error.Line, error.Column).Should().Be(("SPY0200", 2, 5), error.Message);
    }

    [Fact]
    public void LetAfterABrokenStatement_IsASyncPoint_BothErrorsReported()
    {
        // The block header's missing indent is reported with Current on the `let` token right after
        // a newline. `let` is a statement-starting sync token, so recovery resumes AT it and its own
        // refusal (no initializer) is reported too. Without Let in IsSyncToken, recovery skips the
        // whole `let y` line and only the first error survives.
        var (_, errors) = ParseWithErrors("def main():\n    if True:\n    let y\n");
        errors.Select(e => (e.Code, e.Line, e.Column)).Should().Equal(
            ("SPY0104", 3, 5),
            ("SPY0104", 3, 10));
    }

    #endregion
}
