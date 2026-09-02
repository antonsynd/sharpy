using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guards for <c>InlayHintHandler</c> dispatch switches.
/// <c>CollectInlayHints</c> recurses into compound statement bodies; simple statements
/// (assignments, returns, etc.) produce hints from the expression check above the switch.
/// <c>MarkPatternBound</c> walks pattern bindings for deduplication; kinds that bind no
/// name (WildcardPattern, LiteralPattern, etc.) are silently skipped.
/// </summary>
public class InlayHintDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public InlayHintDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> CollectInlayHintsExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(MatchStatement),
    };

    [Fact]
    public void CollectInlayHints_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/InlayHintHandler.cs",
            "CollectInlayHints");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectInlayHints arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CollectInlayHintsExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CollectInlayHintsExpected))}\n" +
            $"  Missing: {string.Join(", ", CollectInlayHintsExpected.Except(arms))}");
    }

    private static readonly HashSet<string> MarkPatternBoundExpected = new()
    {
        nameof(BindingPattern),
        nameof(StarPattern),
        nameof(TuplePattern),
        nameof(ListPattern),
        nameof(PositionalPattern),
        nameof(PropertyPattern),
        nameof(UnionCasePattern),
        nameof(OrPattern),
        nameof(AndPattern),
        nameof(AsPattern),
        nameof(GuardPattern),
    };

    [Fact]
    public void MarkPatternBound_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/InlayHintHandler.cs",
            "MarkPatternBound");
        Assert.NotEmpty(arms);
        _output.WriteLine($"MarkPatternBound arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(MarkPatternBoundExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(MarkPatternBoundExpected))}\n" +
            $"  Missing: {string.Join(", ", MarkPatternBoundExpected.Except(arms))}");
    }
}
