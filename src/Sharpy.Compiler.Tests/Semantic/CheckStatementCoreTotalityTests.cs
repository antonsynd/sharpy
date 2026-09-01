using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>TypeChecker.CheckStatementCore</c>: 31 concrete <see cref="Statement"/>
/// kinds, all handled — the default arm is loud (SPY0200 + log warning). The one kind that
/// reaches the default is <see cref="BreakWithFlagStatement"/>, which is emitter-synthesized
/// and never appears in user-facing ASTs.
/// </summary>
public class CheckStatementCoreTotalityTests
{
    private const string SourceFile = "src/Sharpy.Compiler/Semantic/TypeChecker.cs";

    private readonly ITestOutputHelper _output;

    public CheckStatementCoreTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcreteStatementNames()
    {
        var baseType = typeof(Statement);
        return baseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly HashSet<string> HandledArms = new()
    {
        nameof(FunctionDef),
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(Assignment),
        nameof(VariableDeclaration),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(RaiseStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(AssertStatement),
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(ContinueStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(TypeAlias),
        nameof(PropertyDef),
        nameof(EventDef),
        nameof(MatchStatement),
    };

    private static readonly HashSet<string> LoudDefault = new()
    {
        nameof(BreakWithFlagStatement),
    };

    [Fact]
    public void AllConcreteStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(HandledArms);
        classified.UnionWith(LoudDefault);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        _output.WriteLine($"Concrete Statement subtypes: {all.Count}");
        foreach (var name in all)
        {
            var group = HandledArms.Contains(name) ? "HANDLED"
                : LoudDefault.Contains(name) ? "LOUD DEFAULT"
                : "*** UNCLASSIFIED ***";
            _output.WriteLine($"  {name,-30} {group}");
        }

        if (unclassified.Count > 0)
            _output.WriteLine($"\nUnclassified: {string.Join(", ", unclassified)}");
        if (phantom.Count > 0)
            _output.WriteLine($"\nPhantom: {string.Join(", ", phantom)}");

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void SwitchArms_MatchHandledArms()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "CheckStatementCore");
        Assert.NotEmpty(switchArms);

        _output.WriteLine($"Switch arms found: {switchArms.Count}");
        foreach (var arm in switchArms.OrderBy(a => a, StringComparer.Ordinal))
            _output.WriteLine($"  {arm}");

        Assert.True(switchArms.SetEquals(HandledArms),
            $"CheckStatementCore switch arms differ from HandledArms.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(HandledArms))}\n" +
            $"  Missing from switch: {string.Join(", ", HandledArms.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap = HandledArms.Intersect(LoudDefault).ToList();
        Assert.Empty(overlap);
    }
}
