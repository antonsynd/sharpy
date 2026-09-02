using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Lsp;

/// <summary>
/// Totality guard for <c>FoldingRangeHandler.CollectStatementRanges</c>.
/// Kinds with no body (single-line statements) produce no folding range — that is contractual.
/// </summary>
public class FoldingRangeDispatchTotalityTests
{
    private readonly ITestOutputHelper _output;

    public FoldingRangeDispatchTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> CollectStatementRangesExpected = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(IfStatement),
        nameof(ForStatement),
        nameof(WhileStatement),
        nameof(TryStatement),
        nameof(MatchStatement),
        nameof(WithStatement),
        nameof(PropertyDef),
    };

    [Fact]
    public void CollectStatementRanges_Arms()
    {
        var arms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Lsp/Handlers/FoldingRangeHandler.cs",
            "CollectStatementRanges");
        Assert.NotEmpty(arms);
        _output.WriteLine($"CollectStatementRanges arms ({arms.Count}): {string.Join(", ", arms.OrderBy(a => a))}");
        Assert.True(arms.SetEquals(CollectStatementRangesExpected),
            $"Arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", arms.Except(CollectStatementRangesExpected))}\n" +
            $"  Missing: {string.Join(", ", CollectStatementRangesExpected.Except(arms))}");
    }
}
