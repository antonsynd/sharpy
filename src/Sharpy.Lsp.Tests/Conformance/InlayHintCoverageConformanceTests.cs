using FluentAssertions;
// The enclosing `Sharpy` namespace exposes Sharpy.List<T> (Core's Pythonic list), which shadows
// System's List<T> here; SCG-qualify the concrete lists this harness constructs.
using SCG = System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Lsp.Handlers;
using Xunit;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// Conformance guard for the inlay-hint call walker: <b>every</b> expression form that can
/// contain a call must yield parameter-name hints for that call (#1223).
///
/// <para>
/// The walker used to be a hand-written <c>if (expr is X)</c> chain covering nine of the AST's
/// forty-one <c>Expression</c> types, so twenty-three call-containing forms — comprehensions,
/// lambdas, dict/set literals, f-strings, slices, comparison chains, await, … — silently produced
/// nothing. That list was grown by whoever hit a missing case, never derived from the node set:
/// <c>ListLiteral</c> and <c>TupleLiteral</c> were handled while their siblings
/// <c>DictLiteral</c> and <c>SetLiteral</c> were not. The fix deletes the enumeration and
/// delegates traversal to <see cref="AstVisitor"/>, whose <c>DefaultVisit</c> walks
/// <c>Node.GetChildNodes()</c>.
/// </para>
///
/// <para>
/// This guard is therefore <b>not</b> "did someone add a <c>case</c>" — under the visitor there
/// are no cases to add. It checks the two ways coverage can still regress: a hand-rolled
/// traversal creeping back into the handler, and a node whose hand-written
/// <c>GetChildNodes()</c> override failing to yield a child that can hold a call. The
/// parallel-site-completeness pattern of #1145, applied one level up
/// (<c>ModuleExportsMirrorConformanceTests</c>, <c>WrapperNodeUnwrapConformanceTests</c>).
/// </para>
///
/// <para>
/// Each case compiles a small program through the real LSP pipeline and asserts two hints:
/// <c>control:</c> from an anchor call in plain statement position — proving the document
/// analyzed and hints are being produced at all — and <c>value:</c> from a call nested one level
/// inside the shape under test. The anchor is what makes a failure attributable: without it, a
/// source that failed to analyze would look identical to a walker that failed to traverse.
/// </para>
/// </summary>
public sealed class InlayHintCoverageConformanceTests : IDisposable
{
    /// <summary>
    /// <c>Expression</c> types exempt from a coverage case because their <c>GetChildNodes()</c>
    /// is empty, so no call can be nested inside them. The claim is not taken on trust —
    /// <see cref="EveryExemption_IsJustified_AndEveryLeafReallyHasNoChildren"/> constructs each
    /// one and checks it.
    /// </summary>
    private static readonly Dictionary<string, string> Leaves = new(StringComparer.Ordinal)
    {
        ["IntegerLiteral"] = "`42` — a token's value, no sub-expression.",
        ["FloatLiteral"] = "`4.2` — a token's value, no sub-expression.",
        ["StringLiteral"] = "`\"s\"` — a plain string; interpolation holes belong to FStringLiteral/TStringLiteral, which are covered.",
        ["BytesLiteralExpression"] = "`b\"s\"` — a byte sequence, never interpolated.",
        ["BooleanLiteral"] = "`True` — a keyword.",
        ["NoneLiteral"] = "`None` — a keyword.",
        ["EllipsisLiteral"] = "`...` — a keyword.",
        ["Identifier"] = "`name` — a name; the call it may resolve to is a FunctionCall node, not a child of this one.",
        ["SuperExpression"] = "`super` — a keyword; `super().m()` nests it under MemberAccess/FunctionCall, both covered.",
    };

    /// <summary>
    /// Nodes that <i>can</i> hold a call but that no walked statement position can put one in.
    /// Unlike <see cref="Leaves"/> these are not leaves — the exemption is about reachability,
    /// not shape — so each entry states the parse evidence behind it.
    /// </summary>
    private static readonly Dictionary<string, string> UnreachableFromWalkedPositions = new(StringComparer.Ordinal)
    {
        // `*x` is parsed in exactly three places (Parser.Definitions.cs, Parser.Statements.cs),
        // all of them unpacking *targets*: an assignment target and a for/comprehension target.
        // The hint walker feeds it `Assignment.Value`, never `Assignment.Target`, and rightly so —
        // a call is not assignable. The two forms that put `*` on the value side,
        // `f(*xs)` and `[*xs]`, parse to SpreadElement, which has its own coverage case. On top of
        // that the star operand is a primary, so the operand only reaches a call through
        // parentheses at all: `*probe(1)` is SPY0103, `*(probe(1)), b = [1, 2]` parses.
        ["StarExpression"] =
            "only ever an unpacking target, and targets are not walked for call hints.",
    };

    private const string IntProbe = """
        def probe(value: int) -> int:
            return value
        """;

    private const string StrProbe = """
        def probe(value: int) -> str:
            return "x"
        """;

    private const string ObjectProbe = """
        def probe(value: int) -> object:
            return value
        """;

    private const string ListProbe = """
        def probe(value: int) -> list[int]:
            return [value]
        """;

    private const string DictProbe = """
        def probe(value: int) -> dict[str, int]:
            return {"a": value}
        """;

    private const string NullableProbe = """
        def probe(value: int) -> str | None:
            return "x"
        """;

    private const string ResultProbe = """
        def probe(value: int) -> int !ValueError:
            return Ok(value)
        """;

    private const string AsyncIntProbe = """
        async def probe(value: int) -> int:
            return value
        """;

    /// <summary>The call whose hint proves the document analyzed and the walker ran at all.</summary>
    private const string Anchor = """
        def anchor(control: int) -> int:
            return control
        """;

    /// <summary>
    /// Assembles a coverage source: the probe the shape nests, the anchor, and a body whose
    /// first statement is the anchor call. <paramref name="body"/> lines carry their own
    /// four-space function-body indentation.
    /// </summary>
    private static string Program(string probe, string body, string header = "def main() -> None:")
        => $"{probe}\n\n{Anchor}\n\n{header}\n    anchor(0)\n{body}\n";

    /// <summary>
    /// One source per non-leaf <c>Expression</c> type, each nesting <c>probe(...)</c> exactly one
    /// level inside that node so the <c>value:</c> hint is reachable only through it.
    /// <see cref="EveryExpressionNodeType_IsClassifiedAsLeafOrCovered"/> keeps this list and
    /// <see cref="Leaves"/> together exhaustive over the AST.
    /// </summary>
    private static readonly (string Node, string Source)[] Shapes =
    [
        // Literals with sub-expressions
        ("FStringLiteral", Program(IntProbe, """
            s = f"{probe(1)}"
            print(s)
        """)),
        ("TStringLiteral", Program(IntProbe, """
            s = t"{probe(1)}"
            print(s)
        """)),

        // Collections
        ("ListLiteral", Program(IntProbe, """
            xs = [probe(1)]
            print(xs)
        """)),
        ("DictLiteral", Program(IntProbe, """
            d = {1: probe(1)}
            print(d)
        """)),
        ("SetLiteral", Program(IntProbe, """
            st = {probe(1)}
            print(st)
        """)),
        ("TupleLiteral", Program(IntProbe, """
            tp = (probe(1), 2)
            print(tp)
        """)),

        // Comprehensions
        ("ListComprehension", Program(IntProbe, """
            xs = [probe(x) for x in [1, 2]]
            print(xs)
        """)),
        ("SetComprehension", Program(IntProbe, """
            st = {probe(x) for x in [1, 2]}
            print(st)
        """)),
        ("DictComprehension", Program(IntProbe, """
            d = {x: probe(x) for x in [1, 2]}
            print(d)
        """)),
        ("DictSpreadComprehension", Program(DictProbe, """
            d: dict[str, int] = {**probe(i) for i in [1, 2]}
            print(d)
        """)),

        // Primaries
        ("MemberAccess", Program(StrProbe, """
            s = probe(1).upper()
            print(s)
        """)),
        ("IndexAccess", Program(IntProbe, """
            xs = [1, 2, 3]
            n = xs[probe(0)]
            print(n)
        """)),
        ("SliceAccess", Program(IntProbe, """
            xs = [1, 2, 3]
            part = xs[probe(0):probe(2)]
            print(part)
        """)),
        // A subscript is a MultiAxisAccess only when it mixes a comma with a slice dimension.
        ("MultiAxisAccess", Program(IntProbe, """
            xs = [1, 2, 3]
            part = xs[probe(0), 1:2]
            print(part)
        """)),
        ("FunctionCall", Program(IntProbe, """
            n = probe(1)
            print(n)
        """)),

        // Operators
        ("UnaryOp", Program(IntProbe, """
            n = -probe(1)
            print(n)
        """)),
        ("BinaryOp", Program(IntProbe, """
            n = probe(1) + 1
            print(n)
        """)),
        ("ComparisonChain", Program(IntProbe, """
            flag = 0 < probe(1) < 5
            print(flag)
        """)),
        // `expr?` early-return: its operand is the call, and it needs a Result-returning function.
        ("QuestionMarkExpression", Program(ResultProbe, """
            n: int = probe(1)?
            return Ok(n)
        """, header: "def run() -> int !ValueError:")),

        // Advanced
        ("ConditionalExpression", Program(IntProbe, """
            n = probe(1) if True else 0
            print(n)
        """)),
        // The lambda is passed positionally so the call is reachable only through its body —
        // a keyword argument would conflate this with the keyword-argument traversal edge.
        ("LambdaExpression", Program(IntProbe, """
            xs: list[int] = [3, 1, 2]
            xs.sort(lambda s: probe(s))
            print(xs)
        """)),
        ("TypeCoercion", Program(ObjectProbe, """
            s = probe(1) as! str
            print(s)
        """)),
        ("TypeCheck", Program(ObjectProbe, """
            flag = probe(1) is str
            print(flag)
        """)),
        ("Parenthesized", Program(IntProbe, """
            n = (probe(1))
            print(n)
        """)),
        // The walrus sits inside a list literal because that is the position the parser accepts;
        // ListLiteral is itself covered, so the walrus is the only untested link in the chain.
        ("WalrusExpression", Program(IntProbe, """
            xs = [v := probe(1)]
            print(v)
        """)),
        ("TryExpression", Program(IntProbe, """
            r = try[ValueError] probe(1)
            print(r.is_ok)
        """)),
        ("MaybeExpression", Program(NullableProbe, """
            m: str? = maybe probe(1)
            print(m.is_some)
        """)),
        // A ref argument must be a variable, so this program has a semantic error by
        // construction. It still parses to the shape and the checker still types the nested
        // call, which is all the walker needs — the anchor assertion proves hints still flow.
        ("ModifiedArgument", $"""
        {IntProbe}

        def takes(x: ref int) -> None:
            x = x + 1

        {Anchor}

        def main() -> None:
            anchor(0)
            takes(ref probe(1))
        """),
        // StarExpression has no coverage case — see UnreachableFromWalkedPositions.
        ("SpreadElement", Program(ListProbe, """
            xs = [*probe(1), 2]
            print(xs)
        """)),

        // Future
        ("AwaitExpression", Program(AsyncIntProbe, """
            n = await probe(1)
            print(n)
        """, header: "async def main() -> None:")),
        ("MatchExpression", Program(StrProbe, """
            label: str = match 1:
                case 1: probe(1)
                case _: probe(2)
            print(label)
        """)),
    ];

    private readonly CompilerApi _api = new();
    private readonly SharpyWorkspace _workspace;
    private readonly LanguageService _languageService;
    private readonly LspConfiguration _configuration = new();
    private readonly SharpyInlayHintHandler _handler;

    public InlayHintCoverageConformanceTests()
    {
        _workspace = new SharpyWorkspace(_api, NullLogger<SharpyWorkspace>.Instance);
        _languageService = new LanguageService(_workspace, _api, NullLogger<LanguageService>.Instance);
        _handler = new SharpyInlayHintHandler(_languageService, _configuration);
    }

    [Fact]
    public void EveryExpressionNodeType_IsClassifiedAsLeafOrCovered()
    {
        var declared = typeof(Expression).Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Expression)) && !t.IsAbstract)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var classified = Shapes.Select(s => s.Node)
            .Concat(Leaves.Keys)
            .Concat(UnreachableFromWalkedPositions.Keys)
            .ToHashSet(StringComparer.Ordinal);

        declared.Except(classified).Should().BeEmpty(
            "every Expression node must either nest a call in a coverage case below or be "
            + "allowlisted — as a leaf, or as unreachable — with a justification; a new node type "
            + "that is neither is exactly how the walker fell twenty-three types behind the AST "
            + "(#1223)");

        classified.Except(declared).Should().BeEmpty(
            "a coverage case or leaf entry names an Expression type that no longer exists");

        Shapes.Select(s => s.Node).Should().OnlyHaveUniqueItems(
            "one coverage case per node type keeps the failure list readable");
    }

    [Fact]
    public void EveryExemption_IsJustified_AndEveryLeafReallyHasNoChildren()
    {
        foreach (var (name, justification) in UnreachableFromWalkedPositions)
        {
            justification.Should().NotBeNullOrWhiteSpace(
                $"the {name} exemption must say which walked position could hold a call and why "
                + "none does");
        }

        foreach (var (name, justification) in Leaves)
        {
            justification.Should().NotBeNullOrWhiteSpace(
                $"the {name} allowlist entry must say why the node cannot contain a call");

            var type = typeof(Expression).Assembly.GetType($"Sharpy.Compiler.Parser.Ast.{name}");
            type.Should().NotBeNull($"{name} must be a real AST node type");

            var instance = (Expression)Activator.CreateInstance(type!)!;
            instance.GetChildNodes().Should().BeEmpty(
                $"{name} is allowlisted as a leaf, but it now yields child nodes — it can hold a "
                + "call and needs a coverage case instead");
        }
    }

    [Fact]
    public async Task EveryCallContainingExpressionShape_ProducesAParameterHintAsync()
    {
        var failures = new SCG.List<string>();

        foreach (var (node, source) in Shapes)
        {
            var uri = $"file:///shape-{node}.spy";
            _workspace.OpenDocument(uri, source, 1);
            try
            {
                var analysis = await _languageService.GetAnalysisAsync(uri, CancellationToken.None);
                if (analysis?.Ast == null)
                {
                    failures.Add($"{node}: the coverage source did not parse — fix the source, not the walker.");
                    continue;
                }

                if (!ContainsNodeType(analysis.Ast, node))
                {
                    failures.Add($"{node}: the coverage source parses to no {node} node — fix the source, not the walker.");
                    continue;
                }

                var labels = await ParameterHintLabelsAsync(uri);

                if (!labels.Contains("control:"))
                {
                    failures.Add(
                        $"{node}: the anchor call produced no hint, so this source never analyzed — "
                        + "fix the source, not the walker.");
                    continue;
                }

                if (!labels.Contains("value:"))
                {
                    failures.Add($"{node}: the walker never reached the call nested inside it.");
                }
            }
            finally
            {
                _workspace.CloseDocument(uri);
            }
        }

        // Reported in one aggregated message rather than through a collection assertion: the
        // whole point of the guard is the list of forms that are invisible to inlay hints, and
        // FluentAssertions prints only the first item of a non-empty collection.
        Assert.True(
            failures.Count == 0,
            $"Inlay hints never reach a call inside {failures.Count} expression form(s) — every "
            + "form that can contain a call must yield parameter hints for it (#1223):"
            + Environment.NewLine + string.Join(Environment.NewLine, failures));
    }

    private async Task<IReadOnlyList<string>> ParameterHintLabelsAsync(string uri)
    {
        var request = new InlayHintParams
        {
            TextDocument = new TextDocumentIdentifier(uri),
            Range = new LspRange(new Position(0, 0), new Position(200, 0))
        };

        var hints = await _handler.Handle(request, CancellationToken.None);
        if (hints is null)
            return [];

        return hints
            .Where(h => h.Kind == InlayHintKind.Parameter)
            .Select(h => h.Label.String ?? string.Empty)
            .ToList();
    }

    /// <summary>
    /// Whether the tree holds a node of the named type. Walks <c>GetChildNodes()</c> directly
    /// rather than through <see cref="AstVisitor"/>: this check answers "does the source parse to
    /// the intended shape", which must stay independent of the traversal under test.
    /// </summary>
    private static bool ContainsNodeType(Node root, string typeName)
    {
        if (string.Equals(root.GetType().Name, typeName, StringComparison.Ordinal))
            return true;

        foreach (var child in root.GetChildNodes())
        {
            if (ContainsNodeType(child, typeName))
                return true;
        }

        return false;
    }

    public void Dispose()
    {
        _languageService.Dispose();
        _workspace.Dispose();
    }
}
