using System;
using System.Collections.Generic;
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
/// Docstring classification is a property of a suite's parsed first <em>statement</em>, not of its
/// leading token (#1247). Seven suite parsers each carried their own
/// <c>if (Current.Type == TokenType.String)</c> peek that consumed the token before any statement
/// was built, so <c>"world" - False</c> — whose string is only the left operand of a binary
/// expression — became a docstring plus a dangling <c>-False</c>. That AST parsed fine and meant
/// something else: the SPY0222 the expression should draw was never reported, because the operator
/// it names had been split away from its operands. The mirror-image symptom was
/// <c>"a".upper()</c>, a perfectly good call that became SPY0100 at the orphaned <c>.</c>.
///
/// <para>The rule enforced here is CPython's, measured on python3 3.12.13 via
/// <c>ast.get_docstring</c>: a docstring is an expression statement whose expression is a string
/// constant. Parentheses are transparent (CPython's AST has no paren node); raw strings count
/// (<c>r"doc"</c> is a <c>str</c> constant); f-strings (<c>JoinedStr</c>) and byte strings do not.
/// Every cell below reproduces a measured CPython cell, and the matrix runs against <em>every</em>
/// suite kind — the defect existed once per suite, so a fix proven on one proves nothing about the
/// other six.</para>
///
/// <para>The one deliberate divergence is implicit concatenation: CPython joins <c>"a" "b"</c> to
/// <c>'ab'</c> before the docstring rule sees it, but Sharpy has no implicit concatenation in any
/// position, so the form is a parse error. It is pinned below precisely because the peeks used to
/// accept it here and only here, yielding docstring <c>'a'</c> plus a stray <c>"b"</c> statement.</para>
/// </summary>
public class DocStringClassificationTests
{
    // --- Harness ------------------------------------------------------------------------------

    private static (string? DocString, ImmutableArray<Statement> Body) ParseSuite(string suite, string cell)
    {
        var source = SuiteSource(suite, cell);
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            $"`{cell}` in a {suite} suite must parse cleanly; got: "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));

        if (suite == "module")
            return (module.DocString, module.Body);

        return module.Body[0] switch
        {
            FunctionDef f => (f.DocString, f.Body),
            ClassDef c => (c.DocString, c.Body),
            StructDef s => (s.DocString, s.Body),
            InterfaceDef i => (i.DocString, i.Body),
            var other => throw new InvalidOperationException($"Unexpected suite node {other.GetType().Name}")
        };
    }

    /// <summary>
    /// Each template puts <paramref name="cell"/> in the suite's docstring position and follows it
    /// with exactly one trailing statement, so the body length alone distinguishes "claimed as a
    /// docstring" (1) from "left in the body" (2).
    /// </summary>
    private static string SuiteSource(string suite, string cell) => suite switch
    {
        "module" => $"{cell}\nx: int = 1\n",
        "function" => $"def f() -> None:\n    {cell}\n    pass\n",
        "class" => $"class C:\n    {cell}\n    x: int = 1\n",
        "struct" => $"struct S:\n    {cell}\n    x: int = 1\n",
        "interface" => $"interface I:\n    {cell}\n\n    def m(self) -> int\n",
        _ => throw new ArgumentOutOfRangeException(nameof(suite), suite, "Unknown suite kind")
    };

    private static IReadOnlyList<CompilerDiagnostic> ParseErrors(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        parser.ParseModule();
        return parser.Diagnostics.GetErrors().ToList();
    }

    public static IEnumerable<string> SuiteKinds => new[] { "module", "function", "class", "struct", "interface" };

    private static TheoryData<string, string> AcrossSuites(params string[] cells)
    {
        var data = new TheoryData<string, string>();
        foreach (var suite in SuiteKinds)
            foreach (var cell in cells)
                data.Add(suite, cell);
        return data;
    }

    // --- The docstring cells: an expression statement whose expression is a string constant -----

    /// <summary>
    /// CPython cells that <em>are</em> docstrings: bare, grouped (parens are transparent), raw, and
    /// triple-quoted. The grouped and raw spellings were <em>not</em> docstrings before the fix —
    /// the peek tested one token type and could not see through a <c>(</c>.
    /// </summary>
    public static TheoryData<string, string> DocStringCells =>
        AcrossSuites("\"doc\"", "(\"doc\")", "r\"doc\"", "\"\"\"doc\"\"\"");

    [Theory]
    [MemberData(nameof(DocStringCells))]
    public void StringConstantStatement_IsTheSuiteDocString(string suite, string cell)
    {
        var (docString, body) = ParseSuite(suite, cell);

        docString.Should().Be("doc");
        body.Length.Should().Be(1, "the docstring is hoisted out of the body, leaving only the trailing statement");
    }

    // --- The non-docstring cells: a leading string token that is not a string statement ---------

    /// <summary>
    /// Cells that begin with a string token but are not string-constant statements. <c>"world" -
    /// False</c> is #1247 itself; <c>"a".upper()</c> is the same defect one token later. The
    /// expected node type is the CPython shape (<c>BinOp</c>, <c>Call</c>, <c>JoinedStr</c>,
    /// <c>Constant(bytes)</c>) spelled in Sharpy's AST vocabulary.
    /// </summary>
    public static TheoryData<string, string, Type> NonDocStringCells
    {
        get
        {
            var cells = new (string Cell, Type Node)[]
            {
                ("\"world\" - False", typeof(BinaryOp)),
                ("\"a\" + \"b\"", typeof(BinaryOp)),
                ("\"a\".upper()", typeof(FunctionCall)),
                ("-\"a\"", typeof(UnaryOp)),
                ("f\"doc\"", typeof(FStringLiteral)),
                ("b\"doc\"", typeof(BytesLiteralExpression)),
            };

            var data = new TheoryData<string, string, Type>();
            foreach (var suite in SuiteKinds)
                foreach (var (cell, node) in cells)
                    data.Add(suite, cell, node);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(NonDocStringCells))]
    public void LeadingStringTokenThatIsNotAStringStatement_IsNotADocString(string suite, string cell, Type expectedNode)
    {
        var (docString, body) = ParseSuite(suite, cell);

        docString.Should().BeNull($"`{cell}` is not a string-constant statement");
        body.Length.Should().Be(2, "the statement stays in the body, ahead of the trailing statement");
        body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType(expectedNode);
    }

    /// <summary>
    /// The #1247 repro at its finest grain: one statement, one operator, both operands intact. The
    /// pre-fix shape was <c>DocString "world"</c> plus <c>UnaryOp(Minus, False)</c> — same source,
    /// two statements' worth of meaning, no diagnostic.
    /// </summary>
    [Fact]
    public void Repro1247_IsOneBinaryOpWithBothOperands()
    {
        var (docString, body) = ParseSuite("function", "\"world\" - False");

        docString.Should().BeNull();
        body.Length.Should().Be(2);
        var binaryOp = body[0].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<BinaryOp>().Subject;
        binaryOp.Operator.Should().Be(BinaryOperator.Subtract);
        binaryOp.Left.Should().BeOfType<StringLiteral>().Which.Value.Should().Be("world");
        binaryOp.Right.Should().BeOfType<BooleanLiteral>().Which.Value.Should().BeFalse(
            "the right operand survives as the False it was written as, rather than becoming a second statement");
    }

    // --- Position matters: only the first statement can be a docstring -------------------------

    /// <summary>
    /// A string statement anywhere but first is never a docstring — unchanged by the fix, and the
    /// control that proves the classifier is position-sensitive rather than string-sensitive.
    /// </summary>
    [Theory]
    [InlineData("module", "x: int = 1\n\"not a docstring\"\n")]
    [InlineData("function", "def f() -> None:\n    pass\n    \"not a docstring\"\n")]
    [InlineData("class", "class C:\n    x: int = 1\n    \"not a docstring\"\n")]
    public void StringStatementInNonFirstPosition_IsNeverADocString(string suite, string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse();

        var (docString, body) = suite == "module"
            ? (module.DocString, module.Body)
            : module.Body[0] switch
            {
                FunctionDef f => (f.DocString, f.Body),
                ClassDef c => (c.DocString, c.Body),
                var other => throw new InvalidOperationException($"Unexpected {other.GetType().Name}")
            };

        docString.Should().BeNull();
        body.Length.Should().Be(2);
        body[1].Should().BeOfType<ExpressionStatement>()
            .Which.Expression.Should().BeOfType<StringLiteral>();
    }

    /// <summary>A docstring may be the whole body; the body is then empty, not a one-string body.</summary>
    [Fact]
    public void DocStringAsOnlyStatement_LeavesAnEmptyBody()
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer("def f() -> None:\n    \"\"\"only\"\"\"\n").TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse();

        var function = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        function.DocString.Should().Be("only");
        function.Body.Should().BeEmpty();
    }

    // --- Suites whose bodies are not statement lists --------------------------------------------

    /// <summary>
    /// <c>enum</c> and <c>union</c> parse members, not statements, so there is no first statement to
    /// classify — the parser builds one from a leading expression and asks the same authority. The
    /// docstring must still land, in both the bare and grouped spellings.
    /// </summary>
    [Theory]
    [InlineData("enum Color:\n    \"doc\"\n    RED = 1\n")]
    [InlineData("enum Color:\n    (\"doc\")\n    RED = 1\n")]
    [InlineData("enum Color:\n    r\"doc\"\n    RED = 1\n")]
    [InlineData("enum Color:\n    \"\"\"doc\"\"\"\n    RED = 1\n")]
    public void EnumDocString_IsClassifiedByTheSameAuthority(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));

        var enumDef = module.Body[0].Should().BeOfType<EnumDef>().Subject;
        enumDef.DocString.Should().Be("doc");
        enumDef.Members.Length.Should().Be(1);
    }

    [Theory]
    [InlineData("union Shape:\n    \"doc\"\n    case Circle(radius: float)\n")]
    [InlineData("union Shape:\n    (\"doc\")\n    case Circle(radius: float)\n")]
    [InlineData("union Shape:\n    \"\"\"doc\"\"\"\n    case Circle(radius: float)\n")]
    public void UnionDocString_IsClassifiedByTheSameAuthority(string source)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => d.Message)));

        var unionDef = module.Body[0].Should().BeOfType<UnionDef>().Subject;
        unionDef.DocString.Should().Be("doc");
        unionDef.Cases.Length.Should().Be(1);
    }

    /// <summary>
    /// A leading expression in an enum body that is <em>not</em> a docstring is rewound wholly
    /// unconsumed, so the member loop reports its own "Expected identifier" rather than a diagnostic
    /// invented by the docstring attempt. This is the anti-widening half: the restricted grammars
    /// gained a docstring classifier, not an expression-statement production.
    /// </summary>
    [Fact]
    public void NonDocStringLeadingExpressionInEnum_StaysAMemberError()
    {
        var errors = ParseErrors("enum Color:\n    \"a\" - False\n    RED = 1\n");

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be(DiagnosticCodes.Parser.ExpectedIdentifier);
    }

    // --- The measured divergence: Sharpy has no implicit concatenation --------------------------

    /// <summary>
    /// CPython joins adjacent string literals (<c>"a" "b"</c> → <c>'ab'</c>) before the docstring
    /// rule applies. Sharpy has no such production, so the form is SPY0103 in every position —
    /// including this one, where the peek used to accept it as docstring <c>'a'</c> plus a stray
    /// <c>"b"</c> statement. The parse error is the honest outcome of a missing feature; pinning it
    /// here means the day implicit concatenation is implemented, this test fails and says so.
    /// </summary>
    [Theory]
    [InlineData("def f() -> None:\n    \"a\" \"b\"\n    pass\n")]
    [InlineData("def f() -> None:\n    pass\n    \"a\" \"b\"\n")]
    [InlineData("\"a\" \"b\"\n")]
    public void ImplicitStringConcatenation_IsAParseErrorInEveryPosition(string source)
    {
        var errors = ParseErrors(source);

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be(DiagnosticCodes.Parser.ExpectedEndOfStatement);
    }
}
