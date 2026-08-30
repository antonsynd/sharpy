using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Totality guard for <see cref="Sharpy.Compiler.Analysis.ControlFlow.ControlFlowGraphBuilder.BuildStatement"/>:
/// every concrete <see cref="Statement"/> subtype must be explicitly classified as either
/// control-flow-impacting (handled by a dedicated arm), no-op (type/import definitions that
/// do not affect flow), or simple (falls through to AddStatement). A new Statement subtype
/// that is not listed here will fail this test, forcing deliberate classification (#1664).
/// </summary>
public class CfgStatementTotalityTests
{
    private readonly ITestOutputHelper _output;

    public CfgStatementTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> ControlFlowStatements = new()
    {
        nameof(ReturnStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(BreakStatement),
        nameof(BreakWithFlagStatement),
        nameof(ContinueStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(RaiseStatement),
        nameof(MatchStatement),
        nameof(DecoratedStatement),
        nameof(DeferStatement),
    };

    private static readonly HashSet<string> NoOpStatements = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(PropertyDef),
        nameof(TypeAlias),
        nameof(ImportStatement),
        nameof(FromImportStatement),
    };

    private static readonly HashSet<string> SimpleStatements = new()
    {
        nameof(ExpressionStatement),
        nameof(Assignment),
        nameof(VariableDeclaration),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(YieldStatement),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(EventDef),
    };

    [Fact]
    public void AllConcreteStatementSubtypes_AreClassified()
    {
        var statementBaseType = typeof(Statement);
        var assembly = statementBaseType.Assembly;

        var concreteStatements = assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var allClassified = new HashSet<string>(ControlFlowStatements);
        allClassified.UnionWith(NoOpStatements);
        allClassified.UnionWith(SimpleStatements);

        var unclassified = concreteStatements.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteStatements.Contains(n)).ToList();

        _output.WriteLine($"Concrete Statement subtypes: {concreteStatements.Count}");
        foreach (var name in concreteStatements)
        {
            var group = ControlFlowStatements.Contains(name) ? "CONTROL-FLOW"
                : NoOpStatements.Contains(name) ? "NO-OP"
                : SimpleStatements.Contains(name) ? "SIMPLE"
                : "*** UNCLASSIFIED ***";
            _output.WriteLine($"  {name,-30} {group}");
        }

        if (unclassified.Count > 0)
            _output.WriteLine($"\nUnclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom (listed but not found): {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    /// <summary>
    /// Simple statements that have their own explicit case arm in BuildStatement rather than
    /// falling through to the default. Their bodies still call AddStatement, so they remain
    /// classified Simple — but the scanner will find them.
    /// </summary>
    private static readonly HashSet<string> ExplicitSimple = new()
    {
        nameof(YieldStatement),
    };

    [Fact]
    public void SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(
            "src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs",
            "BuildStatement");

        Assert.NotEmpty(switchArms);

        var cfNotInSwitch = ControlFlowStatements.Except(switchArms).ToList();
        Assert.True(cfNotInSwitch.Count == 0,
            $"ControlFlowStatements missing from switch: {string.Join(", ", cfNotInSwitch)}");

        var noOpNotInSwitch = NoOpStatements.Except(switchArms).ToList();
        Assert.True(noOpNotInSwitch.Count == 0,
            $"NoOpStatements missing from switch: {string.Join(", ", noOpNotInSwitch)}");

        var expected = new HashSet<string>(ControlFlowStatements);
        expected.UnionWith(NoOpStatements);
        expected.UnionWith(ExplicitSimple);
        Assert.True(switchArms.SetEquals(expected),
            $"Switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(expected))}\n" +
            $"  Missing from switch: {string.Join(", ", expected.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var cfAndNoOp = ControlFlowStatements.Intersect(NoOpStatements).ToList();
        var cfAndSimple = ControlFlowStatements.Intersect(SimpleStatements).ToList();
        var noOpAndSimple = NoOpStatements.Intersect(SimpleStatements).ToList();

        Assert.Empty(cfAndNoOp);
        Assert.Empty(cfAndSimple);
        Assert.Empty(noOpAndSimple);
    }
}
