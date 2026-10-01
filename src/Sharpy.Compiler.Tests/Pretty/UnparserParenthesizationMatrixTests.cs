using FluentAssertions;
using Sharpy.Compiler.Pretty;
using Xunit;
using SLexer = Sharpy.Compiler.Lexer.Lexer;
using SModule = Sharpy.Compiler.Parser.Ast.Module;
using SParser = Sharpy.Compiler.Parser.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// The unparser writes a parenthesis only where the grammar needs one (P22b; the 49 O7
/// <c>redundantParens</c> rows: <c>("hello").upper()</c>, <c>a @ (b)</c>, <c>2 ** (-1)</c>). The
/// parser keeps no node for grouping parentheses, so every parenthesis the formatter writes is the
/// unparser's precedence decision — and the emitter carries it into the C#.
///
/// <para><b>Generated matrix.</b> Cells are built from an operator table INDEPENDENT of the
/// unparser's (<see cref="Ops"/>: <c>docs/language_specification/operator_precedence.md</c> plus the
/// parser's operand rules — the right operand of <c>**</c> is a unary expression, the operand of
/// <c>await</c> is a power expression, of <c>-x</c> a unary expression, of <c>not</c> a comparison).
/// For each cell the FULL spelling makes the intended tree explicit and the MINIMAL spelling drops
/// every parenthesis the table says is unnecessary. Two observations per cell:
/// (1) the grammar agrees with the table — <c>parse(minimal)</c> and <c>parse(full)</c> normalise to
/// the same tree; (2) the unparser agrees — <c>unparse(normalise(parse(full)))</c> is exactly the
/// minimal spelling (normalising drops every grouping, so the unparser must re-derive each one: a
/// missing parenthesis changes the tree, a redundant one changes the text). For the operators
/// Python shares, the table's necessity verdicts were checked against python3's parser.</para>
///
/// <para>Axes: binary operator (outer) × binary operator (inner) × side; unary operator × binary
/// operator × side (and binary inside unary); postfix form × receiver kind. Hand-written axes, not
/// generated: the ternary, lambda, walrus and cast forms (their operands are not plain
/// precedence slots) and unary-in-unary — covered by <c>UnparserTests</c> and the corpus sweeps.</para>
/// </summary>
public class UnparserParenthesizationMatrixTests
{
    /// <summary>(spelling, binding level — lower binds tighter, associativity, kind).</summary>
    private sealed record Op(string Text, int Level, bool RightAssoc, bool IsComparison = false);

    // Levels from operator_precedence.md (1 = postfix … 17 = ??).
    private static readonly Op[] Ops =
    {
        new("**", 2, RightAssoc: true),
        new("*", 5, false), new("/", 5, false), new("//", 5, false), new("%", 5, false), new("@", 5, false),
        new("+", 6, false), new("-", 6, false),
        new("<<", 7, false), new(">>", 7, false),
        new("&", 8, false), new("^", 9, false), new("|", 10, false),
        new("|>", 11, false),
        new("<", 13, false, IsComparison: true), new("==", 13, false, IsComparison: true),
        new("and", 15, false), new("or", 16, false), new("??", 17, false),
    };

    /// <summary>(spelling with a trailing space where needed, level, the loosest operand level the operator accepts bare).</summary>
    private static readonly (string Text, int Level, int OperandLevel)[] Unaries =
    {
        ("-", 4, 4), ("+", 4, 4), ("~", 4, 4),  // operand: a unary expression (power, await, unary, postfix)
        ("await ", 3, 2),                        // operand: a power expression
        ("not ", 14, 14),                        // operand: a `not` or comparison expression
    };

    private const int UnaryLevel = 4;
    private const int AwaitLevel = 3;

    /// <summary>Whether an operand at <paramref name="innerLevel"/> needs parentheses on <paramref name="right"/>/left of <paramref name="outer"/>.</summary>
    private static bool BinaryOperandNeedsParens(Op outer, int innerLevel, bool right, bool innerIsUnaryPrefix)
    {
        // The right operand of `**` is a unary expression: unary prefixes and await sit there bare.
        if (outer.Text == "**" && right && innerIsUnaryPrefix)
            return false;
        if (innerLevel != outer.Level)
            return innerLevel > outer.Level;
        return right ? !outer.RightAssoc : outer.RightAssoc;
    }

    public static TheoryData<string, string, string> Cells()
    {
        var cells = new TheoryData<string, string, string>();

        foreach (var outer in Ops)
        {
            foreach (var inner in Ops)
            {
                // A comparison inside a comparison is a chain, not a nesting.
                if (outer.IsComparison && inner.IsComparison)
                    continue;
                var leftNeeds = BinaryOperandNeedsParens(outer, inner.Level, right: false, innerIsUnaryPrefix: false);
                cells.Add($"({inner.Text}) {outer.Text} ·", $"(a {inner.Text} b) {outer.Text} c",
                    leftNeeds ? $"(a {inner.Text} b) {outer.Text} c" : $"a {inner.Text} b {outer.Text} c");
                var rightNeeds = BinaryOperandNeedsParens(outer, inner.Level, right: true, innerIsUnaryPrefix: false);
                cells.Add($"· {outer.Text} ({inner.Text})", $"a {outer.Text} (b {inner.Text} c)",
                    rightNeeds ? $"a {outer.Text} (b {inner.Text} c)" : $"a {outer.Text} b {inner.Text} c");
            }

            foreach (var (u, level, _) in Unaries)
            {
                var leftNeeds = BinaryOperandNeedsParens(outer, level, right: false, innerIsUnaryPrefix: level <= UnaryLevel);
                cells.Add($"({u.Trim()}·) {outer.Text} ·", $"({u}a) {outer.Text} b", leftNeeds ? $"({u}a) {outer.Text} b" : $"{u}a {outer.Text} b");
                var rightNeeds = BinaryOperandNeedsParens(outer, level, right: true, innerIsUnaryPrefix: level <= UnaryLevel);
                cells.Add($"· {outer.Text} ({u.Trim()}·)", $"a {outer.Text} ({u}b)", rightNeeds ? $"a {outer.Text} ({u}b)" : $"a {outer.Text} {u}b");
            }
        }

        foreach (var (u, _, operandLevel) in Unaries)
        {
            foreach (var inner in Ops)
            {
                var needs = inner.Level > operandLevel;
                cells.Add($"{u.Trim()}({inner.Text})", $"{u}(a {inner.Text} b)", needs ? $"{u}(a {inner.Text} b)" : $"{u}a {inner.Text} b");
            }
        }

        // Postfix receivers: only an integer literal needs grouping (`1.m` lexes `1.` as the start of a
        // number). A float is written with its `.` or exponent, so `1.5.m` and `1e3.m` re-lex as float,
        // dot, name (measured with `emit tokens`; python3 accepts both bare too).
        var receivers = new (string Source, bool Needs)[]
        {
            ("a", false), ("f(a)", false), ("a.b", false), ("a[0]", false), ("[a]", false), ("{a: b}", false),
            ("\"s\"", false), ("b\"s\"", false), ("f\"s{a}\"", false), ("1", true), ("1.5", false), ("1e3", false),
            ("-a", true), ("a + b", true), ("not a", true), ("await a", true), ("a ** b", true),
        };
        foreach (var (receiver, needs) in receivers)
        {
            foreach (var postfix in new[] { ".m", ".m()", "[0]", "(0)" })
                cells.Add($"[{receiver}]{postfix}", $"({receiver}){postfix}", needs ? $"({receiver}){postfix}" : $"{receiver}{postfix}");
        }

        return cells;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Unparse_WritesExactlyTheParenthesesTheGrammarNeeds(string label, string full, string minimal)
    {
        var fullModule = Parse($"x = {full}\n", out var fullErrors);
        fullErrors.Should().BeEmpty($"{label}: the fully parenthesised spelling must parse");
        var minimalModule = Parse($"x = {minimal}\n", out var minimalErrors);
        minimalErrors.Should().BeEmpty($"{label}: the minimal spelling must parse — else the table is wrong about this cell");

        var normalized = AstNormalizer.Instance.NormalizeModule(fullModule);
        StructuralEqualityComparer.Instance.Equals(normalized, AstNormalizer.Instance.NormalizeModule(minimalModule))
            .Should().BeTrue($"{label}: `{minimal}` must parse to the same tree as `{full}` — the grammar and the table disagree");

        Unparser.Unparse(normalized).Should().Be($"x = {minimal}\n",
            $"{label}: the unparser must write exactly the parentheses the grammar needs");
    }

    [Fact]
    public void Matrix_CoversEveryAxis()
    {
        // Anchored literally so a shrinking generator is a visible decision:
        // 19×19 binary pairs minus 2×2 comparison pairs, × 2 sides; 19 × 5 unary × 2 sides;
        // 5 unary × 19; 17 receivers × 4 postfix forms.
        Cells().Count.Should().Be((19 * 19 - 4) * 2 + 19 * 5 * 2 + 5 * 19 + 17 * 4);
    }

    /// <summary>The left side keeps the groupings the grammar needs: a prefix operator binds looser than `**` there, and `**` is right-associative.</summary>
    [Theory]
    [InlineData("(-·) ** ·", "(-a) ** b")]
    [InlineData("(**) ** ·", "(a ** b) ** c")]
    [InlineData("(await·) ** ·", "(await a) ** b")]
    public void Matrix_PinsTheLeftOperandOfPower(string label, string minimal)
    {
        Cells().Select(c => ((string)c[0], (string)c[2])).Should().Contain((label, minimal));
    }

    private static SModule Parse(string source, out List<string> errors)
    {
        var lexer = new SLexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new SParser(tokens);
        var module = parser.ParseModule();
        errors = lexer.Diagnostics.GetErrors().Concat(parser.Diagnostics.GetErrors()).Select(d => $"{d.Code} {d.Message}").ToList();
        return module;
    }
}
