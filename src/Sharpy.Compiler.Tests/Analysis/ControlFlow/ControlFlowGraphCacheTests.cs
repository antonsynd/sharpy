using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// The shared per-compilation CFG cache (#1042): one graph per body, keyed by node identity,
/// with exhaustive-match pruning isolated so the pruned variant never leaks to consumers
/// that asked for the pure graph.
/// </summary>
public class ControlFlowGraphCacheTests
{
    [Fact]
    public void GetOrBuild_Function_ReturnsSameInstanceOnRepeat()
    {
        var cache = new ControlFlowGraphCache();
        var func = CreateFunction("f", Pass());

        var first = cache.GetOrBuild(func);
        var second = cache.GetOrBuild(func);

        Assert.Same(first, second);
    }

    [Fact]
    public void GetOrBuild_Function_KeysByIdentityNotValueEquality()
    {
        // AST nodes are records (value equality); two structurally-equal functions at
        // different source locations must not collide in the cache.
        var cache = new ControlFlowGraphCache();
        var func1 = CreateFunction("f", Pass());
        var func2 = func1 with { }; // clone: value-equal, distinct reference
        Assert.Equal(func1, func2); // sanity: value-equal…

        Assert.NotSame(cache.GetOrBuild(func1), cache.GetOrBuild(func2)); // …but distinct graphs
    }

    [Fact]
    public void GetOrBuild_StatementList_ReturnsSameInstanceOnRepeat()
    {
        var cache = new ControlFlowGraphCache();
        var body = (IReadOnlyList<Statement>)new List<Statement> { Pass() };

        Assert.Same(cache.GetOrBuild(body), cache.GetOrBuild(body));
    }

    [Fact]
    public void GetOrBuildPruned_NoExhaustiveMatches_ReturnsTheSharedPureGraph()
    {
        // With no semantic exhaustiveness in play the pruned graph is identical to the pure
        // one, so the cache must hand back the shared instance (build amortized across all
        // three validators).
        var cache = new ControlFlowGraphCache();
        var func = CreateFunction("f", Pass());

        var pure = cache.GetOrBuild(func);

        Assert.Same(pure, cache.GetOrBuildPruned(func, exhaustiveMatches: null));
        Assert.Same(pure, cache.GetOrBuildPruned(func, new HashSet<MatchStatement>()));
    }

    [Fact]
    public void GetOrBuildPruned_ExhaustiveMatchElsewhere_ReturnsTheSharedPureGraph()
    {
        // The exhaustive set names a match in a DIFFERENT function: pruning cannot change
        // this function's graph, so the shared pure graph is returned.
        var cache = new ControlFlowGraphCache();
        var func = CreateFunction("f", Pass());
        var otherMatch = CreateMatch();

        var pruned = cache.GetOrBuildPruned(func, new HashSet<MatchStatement> { otherMatch });

        Assert.Same(cache.GetOrBuild(func), pruned);
    }

    [Fact]
    public void GetOrBuildPruned_FunctionContainsExhaustiveMatch_IsDistinctFromPureAndCached()
    {
        var cache = new ControlFlowGraphCache();
        var match = CreateMatch();
        var func = CreateFunction("f", match);
        var exhaustive = new HashSet<MatchStatement> { match };

        var pure = cache.GetOrBuild(func);
        var pruned = cache.GetOrBuildPruned(func, exhaustive);

        // The pruned variant is its own graph — it must never replace the pure entry…
        Assert.NotSame(pure, pruned);
        // …and both entries are stable across repeat lookups.
        Assert.Same(pruned, cache.GetOrBuildPruned(func, exhaustive));
        Assert.Same(pure, cache.GetOrBuild(func));
    }

    [Fact]
    public void GetOrBuildPruned_FindsExhaustiveMatchNestedInControlFlow()
    {
        // ContainsExhaustiveMatch walks the whole subtree; a match nested under an if must
        // still trigger the pruned build.
        var cache = new ControlFlowGraphCache();
        var match = CreateMatch();
        var wrapped = new IfStatement
        {
            Test = new BooleanLiteral { Value = true },
            ThenBody = ImmutableArray.Create<Statement>(match),
        };
        var func = CreateFunction("f", wrapped);

        var pruned = cache.GetOrBuildPruned(func, new HashSet<MatchStatement> { match });

        Assert.NotSame(cache.GetOrBuild(func), pruned);
    }

    private static FunctionDef CreateFunction(string name, params Statement[] body) =>
        new()
        {
            Name = name,
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = body.ToImmutableArray(),
        };

    private static MatchStatement CreateMatch() =>
        new()
        {
            Scrutinee = new Identifier { Name = "x" },
            Cases = ImmutableArray.Create(new MatchCase
            {
                Pattern = new WildcardPattern(),
                Body = ImmutableArray.Create<Statement>(Pass()),
            }),
        };
}
