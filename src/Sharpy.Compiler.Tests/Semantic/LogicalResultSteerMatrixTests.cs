using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The logical-result steer matrix — every refusal POSITION that can receive a <c>bool</c> produced
/// by <c>and</c>/<c>or</c>, crossed with the operator and the left-operand kind (#1819, R-AL; the
/// inverted conditional rewrite is #1922).
///
/// <para><b>Contract.</b> A refusal that names a refused value's type and whose value is a bool-typed
/// <c>and</c>/<c>or</c> expression names the rule and the Python-style spelling — at EVERY position,
/// whether the refusal comes from the store seam's default arm or from one of the nine sites that
/// format their own message.</para>
///
/// <para><b>Why the previous shape was inert.</b> It declared a nine-entry <c>Positions</c> array and
/// a <c>PositionCount</c> that nothing read, emitted cells for five positions, and asserted
/// <c>Count() &gt;= 18</c> — so reverting every non-seam steer left it green. Here <c>Positions</c> is
/// READ: <see cref="Totality"/> fails if any declared position has no cell, and every cell asserts
/// the base message AND the steer.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class LogicalResultSteerMatrixTests : IntegrationTestBase
{
    public LogicalResultSteerMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Every position a bool-typed <c>and</c>/<c>or</c> can be refused at. Read by
    /// <see cref="Totality"/>, which requires a cell for each — the array is the roster, not a label.
    /// </summary>
    private static readonly string[] Positions =
    {
        // Store-seam positions (TypeChecker.StoreConversion.CheckStoreAt default arm)
        "Declaration", "PlainStore", "Walrus", "IndexStore", "MemberStore", "DictStore",
        "TupleElement", "CollectionElement", "Return", "Yield", "ParameterDefault",
        "ArgumentPositional", "ArgumentKeyword",
        // Sites that format their own message and route through ReportValueTypeMismatch
        "OverloadedCallee", "CLRCallee", "AugmentedStore", "BinaryOperand", "ComparisonChain",
        "Membership", "UnionCaseField",
        "PipeForwardPositional", "PipeForwardKeyword", "PipeForwardCallPositional",
    };

    private static readonly string[] Operators = { "or", "and" };
    private static readonly string[] LeftKinds = { "nullable", "optional", "non_nullable" };

    /// <summary>The steer's nullable/optional arm — the Python-style fallback is <c>??</c>.</summary>
    private const string SteerCoalesce =
        "for Python's value-returning fallback use '??' (the left operand is nullable)";

    /// <summary>
    /// The non-nullable arms. <c>a or b</c> yields <c>a</c> when <c>a</c> is truthy, so the rewrite
    /// is <c>&lt;left&gt; if &lt;left&gt; else &lt;right&gt;</c>; <c>a and b</c> yields <c>b</c>, so it
    /// is the mirror. Both were swapped before #1922 — measured against python3 3.12.
    /// </summary>
    private const string SteerConditionalOr =
        "for Python's value-returning fallback use a conditional: '<left> if <left> else <right>'";

    private const string SteerConditionalAnd =
        "for Python's value-returning fallback use a conditional: '<right> if <left> else <left>'";

    [Theory]
    [MemberData(nameof(MatrixCells))]
    public void SteerIsPresent(
        string position, string op, string leftKind,
        string source, string expectedCode, string expectedBaseMessage, string expectedSteer)
    {
        var label = $"position={position} op={op} leftKind={leftKind}";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"{label} should be refused");

        var candidates = result.RawDiagnostics.Where(d => d.Code == expectedCode).ToList();
        candidates.Should().NotBeEmpty(
            $"expected {expectedCode} for {label}; got: "
            + string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message)));

        // The BASE message — a cell that only asserted the steer would pass if the refusal itself
        // moved to a different (wrong) shape.
        var matching = candidates
            .Where(d => d.Message.Contains(expectedBaseMessage, StringComparison.Ordinal))
            .ToList();
        matching.Should().NotBeEmpty(
            $"{label} should carry the base message '{expectedBaseMessage}'; got: "
            + string.Join(" | ", candidates.Select(d => d.Message)));

        // ... and the steer, on the SAME diagnostic.
        matching.Should().Contain(
            d => d.Message.Contains("returns bool in Sharpy (logical_operators.md §Return Type)", StringComparison.Ordinal)
                && d.Message.Contains(expectedSteer, StringComparison.Ordinal),
            $"{label} should carry the #1819 steer '{expectedSteer}'; got: "
            + string.Join(" | ", matching.Select(d => d.Message)));

        // The steer must name the OPERATOR the user wrote.
        matching.Should().Contain(
            d => d.Message.Contains($"'{op}' returns bool in Sharpy", StringComparison.Ordinal),
            $"{label} should name the '{op}' operator in the steer");
    }

    [Theory]
    [MemberData(nameof(PositiveControls))]
    public void PositiveControl_Runs(string label, string source, string expectedOutput)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(
            $"positive control '{label}' should compile and run; errors: "
            + string.Join(" | ", result.CompilationErrors));
        result.StandardOutput.TrimEnd().Should().Be(expectedOutput, $"positive control '{label}'");
    }

    /// <summary>
    /// The left operand of each kind, and the declaration that binds it. <c>nullable</c> is C#'s
    /// <c>T | None</c>, <c>optional</c> is Sharpy's strict <c>T?</c>, <c>non_nullable</c> is a plain
    /// <c>str</c> — the first two steer to <c>??</c> and the third to a conditional.
    /// </summary>
    private static string BindLeft(string leftKind) => leftKind switch
    {
        "nullable" => "    name: str | None = None\n",
        "optional" => "    name: str? = None()\n",
        "non_nullable" => "    name: str = \"Ada\"\n",
        _ => throw new ArgumentOutOfRangeException(nameof(leftKind), leftKind, "unknown left kind"),
    };

    private static string ParamLeft(string leftKind) => leftKind switch
    {
        "nullable" => "name: str | None",
        "optional" => "name: str?",
        "non_nullable" => "name: str",
        _ => throw new ArgumentOutOfRangeException(nameof(leftKind), leftKind, "unknown left kind"),
    };

    private static string SteerFor(string op, string leftKind)
        => leftKind == "non_nullable"
            ? (op == "or" ? SteerConditionalOr : SteerConditionalAnd)
            : SteerCoalesce;

    public static TheoryData<string, string, string, string, string, string, string> MatrixCells
    {
        get
        {
            var data = new TheoryData<string, string, string, string, string, string, string>();

            void Cell(string position, string op, string leftKind, string source,
                string code, string baseMessage)
                => data.Add(position, op, leftKind, source, code, baseMessage, SteerFor(op, leftKind));

            var mismatch = DiagnosticCodes.Semantic.TypeMismatch;
            var badReturn = DiagnosticCodes.Semantic.MissingReturnValue;
            var badOperator = DiagnosticCodes.Semantic.InvalidBinaryOperation;

            const string AssignToVariable = "Cannot assign type 'bool' to variable of type 'str'";
            const string AssignToSlot = "Cannot assign type 'bool' to 'str'";
            const string PassArgument = "Cannot pass argument of type 'bool' to parameter of type 'str'";

            foreach (var op in Operators)
            {
                // Every position at a NULLABLE left operand — the '??' arm.
                Cell("Declaration", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    x: str = name {op} \"z\"\n    print(x)",
                    mismatch, AssignToVariable);

                Cell("PlainStore", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    x: str = \"q\"\n    x = name {op} \"z\"\n    print(x)",
                    mismatch, AssignToVariable);

                Cell("Walrus", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    x: str = \"q\"\n    if (x := name {op} \"z\"):\n        print(x)",
                    mismatch, AssignToVariable);

                Cell("IndexStore", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    xs: list[str] = [\"a\"]\n    xs[0] = name {op} \"z\"\n    print(xs)",
                    mismatch, AssignToSlot);

                Cell("MemberStore", op, "nullable",
                    $"class C:\n    s: str = \"a\"\n\ndef main() -> None:\n{BindLeft("nullable")}    c: C = C()\n    c.s = name {op} \"z\"\n    print(c.s)",
                    mismatch, AssignToSlot);

                Cell("DictStore", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    d: dict[str, str] = {{}}\n    d[\"k\"] = name {op} \"z\"\n    print(d)",
                    mismatch, AssignToSlot);

                Cell("TupleElement", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    t: tuple[str, int] = (name {op} \"z\", 1)\n    print(t)",
                    mismatch, AssignToSlot);

                Cell("CollectionElement", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    xs: list[str] = [name {op} \"z\"]\n    print(xs)",
                    mismatch, AssignToSlot);

                Cell("Return", op, "nullable",
                    $"def f({ParamLeft("nullable")}) -> str:\n    return name {op} \"z\"\n\ndef main() -> None:\n    print(f(None))",
                    badReturn, "Cannot return type 'bool' from function expecting 'str'");

                Cell("Yield", op, "nullable",
                    $"def g({ParamLeft("nullable")}) -> Iterator[str]:\n    yield name {op} \"z\"\n\ndef main() -> None:\n    for s in g(None):\n        print(s)",
                    mismatch, "Yielded type 'bool' is not assignable to declared return type");

                Cell("ArgumentPositional", op, "nullable",
                    $"def f(s: str) -> None:\n    print(s)\n\ndef main() -> None:\n{BindLeft("nullable")}    f(name {op} \"z\")",
                    mismatch, PassArgument);

                // The keyword route names the PARAMETER, the positional route does not.
                Cell("ArgumentKeyword", op, "nullable",
                    $"def f(s: str) -> None:\n    print(s)\n\ndef main() -> None:\n{BindLeft("nullable")}    f(s=name {op} \"z\")",
                    mismatch, "Cannot pass argument of type 'bool' to parameter 's' of type 'str'");

                Cell("OverloadedCallee", op, "nullable",
                    $"class C:\n    def m(self, s: str) -> None:\n        print(s)\n\ndef main() -> None:\n{BindLeft("nullable")}    c: C = C()\n    c.m(name {op} \"z\")",
                    mismatch, PassArgument);

                Cell("CLRCallee", op, "nullable",
                    $"from System.Collections.Generic import List\n\ndef main() -> None:\n{BindLeft("nullable")}    xs: List[str] = List[str]()\n    xs.Add(name {op} \"z\")\n    print(xs.count)",
                    mismatch, "Argument 1 of 'List.Add' expects 'str' but got 'bool'");

                Cell("AugmentedStore", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    s: str = \"a\"\n    s += name {op} \"z\"\n    print(s)",
                    badOperator, "Type 'str' does not support operator '+=' with operand of type 'bool'");

                Cell("BinaryOperand", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    s: str = \"a\" + (name {op} \"z\")\n    print(s)",
                    badOperator, "Type 'str' does not support operator '+' with operand of type 'bool'");

                Cell("ComparisonChain", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    if \"a\" < (name {op} \"z\") < \"c\":\n        print(\"yes\")",
                    badOperator, "Type 'str' does not support operator '<' with operand of type 'bool'");

                Cell("Membership", op, "nullable",
                    $"def main() -> None:\n{BindLeft("nullable")}    xs: list[str] = [\"a\"]\n    if (name {op} \"z\") in xs:\n        print(\"yes\")",
                    badOperator, "Type 'bool' does not support operator 'in' with operand of type 'list[str]'");

                Cell("UnionCaseField", op, "nullable",
                    $"union Shape:\n    case Circle(radius: str)\n    case Square(side: str)\n\ndef main() -> None:\n{BindLeft("nullable")}    s: Shape = Shape.Circle(name {op} \"z\")\n    print(s)",
                    mismatch, "Argument 1 has type 'bool' but field 'radius' expects 'str'");

                Cell("PipeForwardPositional", op, "nullable",
                    $"def f(s: str) -> None:\n    print(s)\n\ndef main() -> None:\n{BindLeft("nullable")}    (name {op} \"z\") |> f",
                    mismatch, "Cannot pipe value of type 'bool' to function expecting 'str'");

                Cell("PipeForwardKeyword", op, "nullable",
                    $"def f(a: int, s: str) -> None:\n    print(s)\n\ndef main() -> None:\n{BindLeft("nullable")}    1 |> f(s=name {op} \"z\")",
                    mismatch, "Cannot pass argument of type 'bool' to parameter 's' of type 'str'");

                Cell("PipeForwardCallPositional", op, "nullable",
                    $"def tag(s: str, n: int) -> str:\n    return s\n\ndef main() -> None:\n{BindLeft("nullable")}    x: str = (name {op} \"z\") |> tag(1)\n    print(x)",
                    mismatch, "Cannot pass piped value of type 'bool' to parameter 's' of type 'str'");

                // The OPTIONAL and NON_NULLABLE left kinds. The steer's arm is chosen by the left
                // operand, so it is exercised at a seam position, at an operator position and at a
                // pipe position — one of each message shape.
                foreach (var leftKind in new[] { "optional", "non_nullable" })
                {
                    Cell("Declaration", op, leftKind,
                        $"def main() -> None:\n{BindLeft(leftKind)}    x: str = name {op} \"z\"\n    print(x)",
                        mismatch, AssignToVariable);

                    Cell("AugmentedStore", op, leftKind,
                        $"def main() -> None:\n{BindLeft(leftKind)}    s: str = \"a\"\n    s += name {op} \"z\"\n    print(s)",
                        badOperator, "Type 'str' does not support operator '+=' with operand of type 'bool'");

                    Cell("PipeForwardPositional", op, leftKind,
                        $"def f(s: str) -> None:\n    print(s)\n\ndef main() -> None:\n{BindLeft(leftKind)}    (name {op} \"z\") |> f",
                        mismatch, "Cannot pipe value of type 'bool' to function expecting 'str'");
                }

                // ParameterDefault takes only a constant left operand, so it is a non_nullable cell.
                Cell("ParameterDefault", op, "non_nullable",
                    $"def f(s: str = \"a\" {op} \"b\") -> None:\n    print(s)\n\ndef main() -> None:\n    f()",
                    mismatch, "Default value type 'bool' is not assignable to parameter type 'str'");
            }

            return data;
        }
    }

    public static TheoryData<string, string, string> PositiveControls
    {
        get
        {
            var data = new TheoryData<string, string, string>();

            // python3 3.12: `None or "z"` -> 'z'. `??` is the Sharpy spelling the steer names.
            data.Add("coalesce_nullable",
                "def main() -> None:\n    name: str | None = None\n    fallback: str = name ?? \"Anonymous\"\n    print(fallback)",
                "Anonymous");

            data.Add("coalesce_optional",
                "def main() -> None:\n    name: str? = None()\n    fallback: str = name ?? \"Anonymous\"\n    print(fallback)",
                "Anonymous");

            data.Add("coalesce_in_argument",
                "def f(s: str) -> None:\n    print(s)\n\ndef main() -> None:\n    name: str | None = None\n    f(name ?? \"z\")",
                "z");

            // python3 3.12: `bool(None or "z")` is True — a bool SLOT accepts the expression, so the
            // steer must not fire. Without this control the matrix could be satisfied by refusing
            // every `or` everywhere.
            data.Add("bool_target_accepts_or",
                "def main() -> None:\n    name: str | None = None\n    b: bool = name or \"z\"\n    print(b)",
                "True");

            data.Add("bool_target_accepts_and",
                "def main() -> None:\n    name: str | None = None\n    b: bool = name and \"z\"\n    print(b)",
                "False");

            // python3 3.12: `"Ada" or "Anonymous"` -> 'Ada' and `"Ada" and "Anonymous"` -> 'Anonymous'
            // — the conditional rewrites the non_nullable steer names, RUN.
            data.Add("conditional_rewrite_or",
                "def main() -> None:\n    name: str = \"Ada\"\n    fallback: str = name if name else \"Anonymous\"\n    print(fallback)",
                "Ada");

            data.Add("conditional_rewrite_and",
                "def main() -> None:\n    name: str = \"Ada\"\n    fallback: str = \"Anonymous\" if name else name\n    print(fallback)",
                "Anonymous");

            return data;
        }
    }

    [Fact]
    public void Totality()
    {
        var cells = MatrixCells.Cast<object[]>().ToList();

        // The axes, anchored to literal counts rather than to their own Length.
        Positions.Should().HaveCount(23, "23 refusal positions are rostered");
        Operators.Should().HaveCount(2, "'or' and 'and'");
        LeftKinds.Should().HaveCount(3, "nullable, optional, non_nullable");
        Positions.Should().OnlyHaveUniqueItems();

        // 23 positions x 2 operators = 46 nullable cells, minus ParameterDefault (whose left operand
        // must be constant, so it has no nullable cell) = 44; plus 2 operators x 2 extra left kinds
        // x 3 positions = 12; plus 2 ParameterDefault non_nullable cells. 44 + 12 + 2 = 58.
        cells.Should().HaveCount(58, "the cell roster is fixed; adding or dropping a cell is a decision");

        // Positions is READ — the previous shape declared a nine-entry array nothing consumed and
        // emitted cells for five of them.
        var covered = cells.Select(c => (string)c[0]).Distinct().ToList();
        Positions.Should().BeSubsetOf(covered,
            "every declared position needs a cell; uncovered: "
            + string.Join(", ", Positions.Except(covered)));
        covered.Should().BeSubsetOf(Positions,
            "every emitted cell names a declared position; undeclared: "
            + string.Join(", ", covered.Except(Positions)));

        Operators.Should().BeSubsetOf(cells.Select(c => (string)c[1]).Distinct().ToList());
        LeftKinds.Should().BeSubsetOf(cells.Select(c => (string)c[2]).Distinct().ToList());

        // Every declared refusal code appears, so a cell roster that quietly collapsed onto one
        // message shape is visible.
        var codes = cells.Select(c => (string)c[4]).Distinct().ToList();
        codes.Should().BeEquivalentTo(new[]
        {
            DiagnosticCodes.Semantic.TypeMismatch,
            DiagnosticCodes.Semantic.MissingReturnValue,
            DiagnosticCodes.Semantic.InvalidBinaryOperation,
        });
    }

    // ---------------------------------------------------------------------------------------
    // Source scan: the steer is a property of the FAMILY, not of the site.
    // ---------------------------------------------------------------------------------------

    private const string SteerFormatter = "DescribeLogicalResultSteer";
    private const string Reporter = "ReportValueTypeMismatch";

    /// <summary>
    /// The refusal-message phrases that can name a refused VALUE's type — the family a bool from
    /// <c>and</c>/<c>or</c> lands in. Anchored to the literals the compiler actually writes, so a
    /// new message shape has to be added here deliberately.
    /// </summary>
    private static readonly string[] FamilyPhrases =
    {
        "Cannot assign type",
        "Cannot return type",
        "Yielded type",
        "Default value type",
        "Default value of type",
        "Arrow lambda body type",
        "Cannot pass",
        "Cannot pipe value of type",
        "does not support operator",
        "' expects '",
        "has type '",
    };

    /// <summary>
    /// The refusals that share a family phrase but structurally cannot carry an <c>and</c>/<c>or</c>
    /// value, excluded BY NAME and asserted to still exist (an exemption that silently stopped
    /// matching would weaken the scan without failing it):
    /// <list type="bullet">
    ///   <item>the <c>None()</c> operand refusal — its operand is the literal call <c>None()</c>;</item>
    ///   <item>the <c>??=</c> target-shape refusal — it names the TARGET's type and fires before the
    ///   value is checked at all;</item>
    ///   <item>the two CLR <c>None</c>-argument refusals — the refused value is the <c>None</c>
    ///   literal, and they report SPY0223 (nullability), not a value/target type mismatch.</item>
    /// </list>
    /// The exemption is never the guard's subject: none of these can receive a bool-typed
    /// <c>and</c>/<c>or</c> expression, which is what the matrix cells measure.
    /// </summary>
    private static readonly string[] ExemptTexts =
    {
        "with operand 'None()'",
        "the target must be nullable",
        "Cannot pass 'None'",
    };

    /// <param name="Callee">
    /// The method the literal is passed to directly, or null when it initializes a <c>message</c>
    /// local first.
    /// </param>
    /// <param name="ReporterInScope">
    /// Whether the enclosing method calls <see cref="Reporter"/> — the route a <c>message</c> local
    /// takes to the steer.
    /// </param>
    private sealed record LiteralSite(
        string File, string Method, int Line, string Text, string? Callee, bool ReporterInScope);

    [Fact]
    public void EverySteerFamilySiteRoutesThroughTheOneReporter()
    {
        var sites = FindFamilySites();

        var exempt = sites
            .Where(s => ExemptTexts.Any(t => s.Text.Contains(t, StringComparison.Ordinal)))
            .ToList();
        exempt.Should().HaveCount(4,
            "all four named exemptions must still exist, unmoved (the None() operand, the '??=' "
            + "target shape, and the two CLR None-argument refusals); found: "
            + string.Join("; ", exempt.Select(Describe)));

        var familySites = sites.Except(exempt).ToList();

        // Positive control: the scan must FIND the family. A zero-site scan would make the
        // violation assertion below pass vacuously.
        familySites.Should().HaveCountGreaterThanOrEqualTo(20,
            "the bool-confusable refusal family has at least 20 message literals; found: "
            + string.Join("; ", familySites.Select(Describe)));

        // Three legal routes to the steer, and no fourth: passed straight to the reporter; built by
        // FormatStoreError (whose result CheckStoreAt hands to the reporter); or assigned to a
        // `message` local that the same method then reports through the reporter.
        var violations = familySites
            .Where(s => s.Callee != Reporter
                && s.Method != "FormatStoreError"
                && !(s.Callee == null && s.ReporterInScope))
            .ToList();

        violations.Should().BeEmpty(
            $"every refusal that names a refused value's type must be reported through {Reporter} — "
            + "a site calling AddError directly never appends the #1819 steer. Found: "
            + string.Join("; ", violations.Select(Describe)));

        // The FormatStoreError route is only legal because CheckStoreAt reports through the
        // reporter. Without this the exemption above would be free.
        MethodCalls("CheckStoreAt").Should().Contain(Reporter,
            $"CheckStoreAt must hand FormatStoreError's message to {Reporter}");
    }

    /// <summary>The names of the methods invoked inside <paramref name="methodName"/>.</summary>
    private static IReadOnlyList<string> MethodCalls(string methodName)
    {
        foreach (var file in SemanticSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == methodName);
            if (method == null)
                continue;

            return method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(i => i.Expression is IdentifierNameSyntax id ? id.Identifier.ValueText : null)
                .Where(n => n != null)
                .Select(n => n!)
                .ToList();
        }

        throw new InvalidOperationException($"method '{methodName}' not found in the type checker");
    }

    [Fact]
    public void TheSteerFormatterHasExactlyOneCaller()
    {
        var invocations = new List<LiteralSite>();

        foreach (var file in SemanticSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax id || id.Identifier.ValueText != SteerFormatter)
                    continue;

                invocations.Add(new LiteralSite(
                    Path.GetFileName(file),
                    EnclosingMethod(invocation)?.Identifier.ValueText ?? "<unknown>",
                    invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    invocation.ToString(), null, false));
            }
        }

        // Before the #1819 class cure the helper was pasted at six sites and missed at eight more.
        // One caller is what makes the steer un-missable: a new refusal reaches it only by going
        // through the reporter.
        invocations.Should().HaveCount(1,
            $"{SteerFormatter} must have exactly one caller ({Reporter}); found: "
            + string.Join("; ", invocations.Select(Describe)));

        invocations[0].Method.Should().Be(Reporter);
    }

    private static string Describe(LiteralSite s)
        => $"{s.File}:{s.Line} in {s.Method}() [callee={s.Callee ?? "<local>"}]: {Truncate(s.Text)}";

    /// <summary>
    /// Every message literal in the type checker that names a refused value's type, with the name of
    /// the method it is passed to (null when it is assigned to a local first, as
    /// <c>ReportUnsupportedBinaryOperator</c> and <c>ClassifyMembership</c> do).
    /// </summary>
    private static IReadOnlyList<LiteralSite> FindFamilySites()
    {
        var results = new List<LiteralSite>();

        foreach (var file in SemanticSourceFiles())
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            foreach (var node in root.DescendantNodes()
                .Where(n => n is InterpolatedStringExpressionSyntax or LiteralExpressionSyntax))
            {
                // A literal nested inside another interpolated string is reported by its outermost
                // enclosing literal only.
                if (node.Ancestors().Any(a => a is InterpolatedStringExpressionSyntax))
                    continue;

                var text = node.ToString();
                if (!FamilyPhrases.Any(p => text.Contains(p, StringComparison.Ordinal)))
                    continue;

                // Only literals that flow into a diagnostic — the message of a refusal is either an
                // argument of the reporting call or the initializer of a `message` local.
                var expression = ClimbConcatenation(node);
                var callee = expression.Parent is ArgumentSyntax
                {
                    Parent: ArgumentListSyntax
                    {
                        Parent: InvocationExpressionSyntax { Expression: IdentifierNameSyntax id }
                    }
                }
                    ? id.Identifier.ValueText
                    : null;

                var enclosing = EnclosingMethod(node);
                var enclosingName = enclosing?.Identifier.ValueText ?? "<unknown>";
                if (callee == null && !IsMessageLocalInitializer(expression) && enclosingName != "FormatStoreError")
                    continue;

                var reporterInScope = enclosing != null
                    && enclosing.DescendantNodes().OfType<InvocationExpressionSyntax>()
                        .Any(i => i.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == Reporter);

                results.Add(new LiteralSite(
                    Path.GetFileName(file), enclosingName,
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    text, callee, reporterInScope));
            }
        }

        return results;
    }

    /// <summary>Walks up a <c>"a" + "b" + steer</c> chain to the whole concatenated expression.</summary>
    private static SyntaxNode ClimbConcatenation(SyntaxNode node)
    {
        var current = node;
        while (current.Parent is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression } add)
            current = add;
        return current;
    }

    private static bool IsMessageLocalInitializer(SyntaxNode expression)
        => expression.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator }
            && declarator.Identifier.ValueText.Contains("message", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string text)
        => text.Length <= 110 ? text : text[..110] + "…";

    private static MethodDeclarationSyntax? EnclosingMethod(SyntaxNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method)
                return method;
        }

        return null;
    }

    private static IEnumerable<string> SemanticSourceFiles()
        => Directory.GetFiles(
            Path.Combine(FindRepoRoot(), "src", "Sharpy.Compiler", "Semantic"), "TypeChecker*.cs");

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current, "src", "Sharpy.Compiler", "Semantic")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("repository root not found from " + AppContext.BaseDirectory);
    }
}
