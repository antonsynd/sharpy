using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>NameResolver.ResolveDeclaration</c> and
/// <c>NameResolver.ResolveNestedTypeDeclaration</c>: every concrete
/// <see cref="Statement"/> subtype must be classified as either handled (has a dedicated
/// case arm) or skipped (falls through, handled in later passes). A new Statement subtype
/// that is not listed here will fail this test (#1694).
/// </summary>
public class NameResolverDeclarationsTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/NameResolver.Declarations.cs";

    private readonly ITestOutputHelper _output;

    public NameResolverDeclarationsTotalityTests(ITestOutputHelper output) => _output = output;

    private static List<string> GetConcreteStatementNames()
    {
        var statementBaseType = typeof(Statement);
        return statementBaseType.Assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(statementBaseType)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    // --- ResolveDeclaration ---

    private static readonly HashSet<string> ResolveDeclaration_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(FunctionDef),
        nameof(VariableDeclaration),
        nameof(TypeAlias),
        nameof(PropertyDef),
    };

    private static readonly HashSet<string> ResolveDeclaration_Skipped = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(EventDef),
    };

    [Fact]
    public void ResolveDeclaration_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ResolveDeclaration_Handled);
        classified.UnionWith(ResolveDeclaration_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        _output.WriteLine($"Concrete Statement subtypes: {all.Count}");
        foreach (var name in all)
        {
            var group = ResolveDeclaration_Handled.Contains(name) ? "HANDLED"
                : ResolveDeclaration_Skipped.Contains(name) ? "SKIPPED"
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

    [Fact]
    public void ResolveDeclaration_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ResolveDeclaration");
        Assert.NotEmpty(switchArms);

        var missing = ResolveDeclaration_Handled.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"ResolveDeclaration_Handled types missing from switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(ResolveDeclaration_Handled),
            $"ResolveDeclaration switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ResolveDeclaration_Handled))}\n" +
            $"  Missing: {string.Join(", ", ResolveDeclaration_Handled.Except(switchArms))}");
    }

    // --- ResolveNestedTypeDeclaration ---

    // The seven type-declaring kinds route through this dispatcher (#1729, R-I): union and delegate
    // join NestedTypes, a type alias joins NestedTypeAliases. UnionDef/DelegateDef/TypeAlias moved
    // out of the Skipped set below when the #1729 refusal drained.
    private static readonly HashSet<string> ResolveNestedTypeDeclaration_Handled = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(TypeAlias),
    };

    private static readonly HashSet<string> ResolveNestedTypeDeclaration_Skipped = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(VariableDeclaration),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(FunctionDef),
        nameof(PropertyDef),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
        nameof(EventDef),
    };

    [Fact]
    public void ResolveNestedTypeDeclaration_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ResolveNestedTypeDeclaration_Handled);
        classified.UnionWith(ResolveNestedTypeDeclaration_Skipped);

        var unclassified = all.Where(n => !classified.Contains(n)).ToList();
        var phantom = classified.Where(n => !all.Contains(n)).ToList();

        Assert.Empty(unclassified);
        Assert.Empty(phantom);
    }

    [Fact]
    public void ResolveNestedTypeDeclaration_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "ResolveNestedTypeDeclaration");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ResolveNestedTypeDeclaration_Handled),
            $"ResolveNestedTypeDeclaration switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ResolveNestedTypeDeclaration_Handled))}\n" +
            $"  Missing: {string.Join(", ", ResolveNestedTypeDeclaration_Handled.Except(switchArms))}");
    }

    // --- ResolveTypeBodyMember (P21g D2, #2360) ---

    private const string MembersSourceFile =
        "src/Sharpy.Compiler/Semantic/NameResolver.Members.cs";

    // The one member dispatcher every type-body walk (class, struct, interface, union) hands its
    // statements to. Its case arms are the member kinds; the seven type-declaring kinds reach
    // ResolveNestedTypeDeclaration through the default arm's classifier (TryGetNestedDeclaration).
    private static readonly HashSet<string> ResolveTypeBodyMember_Handled = new()
    {
        nameof(FunctionDef),
        nameof(VariableDeclaration),
        nameof(PropertyDef),
        nameof(EventDef),
    };

    private static readonly HashSet<string> ResolveTypeBodyMember_ViaNestedClassifier = new()
    {
        nameof(ClassDef),
        nameof(StructDef),
        nameof(InterfaceDef),
        nameof(EnumDef),
        nameof(UnionDef),
        nameof(DelegateDef),
        nameof(TypeAlias),
    };

    private static readonly HashSet<string> ResolveTypeBodyMember_Skipped = new()
    {
        nameof(ExpressionStatement),
        nameof(DecoratedStatement),
        nameof(Assignment),
        nameof(AssertStatement),
        nameof(PassStatement),
        nameof(BreakStatement),
        nameof(ContinueStatement),
        nameof(ReturnStatement),
        nameof(YieldStatement),
        nameof(RaiseStatement),
        nameof(IfStatement),
        nameof(WhileStatement),
        nameof(ForStatement),
        nameof(TryStatement),
        nameof(WithStatement),
        nameof(DeferStatement),
        nameof(ImportStatement),
        nameof(FromImportStatement),
        nameof(MatchStatement),
    };

    [Fact]
    public void ResolveTypeBodyMember_AllStatementSubtypes_AreClassified()
    {
        var all = GetConcreteStatementNames();
        var classified = new HashSet<string>(ResolveTypeBodyMember_Handled);
        classified.UnionWith(ResolveTypeBodyMember_ViaNestedClassifier);
        classified.UnionWith(ResolveTypeBodyMember_Skipped);

        Assert.Empty(all.Where(n => !classified.Contains(n)).ToList());
        Assert.Empty(classified.Where(n => !all.Contains(n)).ToList());
    }

    [Fact]
    public void ResolveTypeBodyMember_SwitchArms_MatchClassification()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(MembersSourceFile, "ResolveTypeBodyMember");
        Assert.NotEmpty(switchArms);

        Assert.True(switchArms.SetEquals(ResolveTypeBodyMember_Handled),
            $"ResolveTypeBodyMember switch arms differ from expected.\n" +
            $"  Extra: {string.Join(", ", switchArms.Except(ResolveTypeBodyMember_Handled))}\n" +
            $"  Missing: {string.Join(", ", ResolveTypeBodyMember_Handled.Except(switchArms))}");
    }

    [Fact]
    public void ResolveTypeBodyMember_NestedClassifierKinds_AreTheNestedDispatchersKinds()
    {
        // The default arm hands a statement to ResolveNestedTypeDeclaration when the classifier says
        // it declares a type; the kinds that dispatcher resolves are exactly the ones this one routes.
        Assert.True(ResolveTypeBodyMember_ViaNestedClassifier.SetEquals(ResolveNestedTypeDeclaration_Handled));
        Assert.Empty(ResolveTypeBodyMember_Handled.Intersect(ResolveTypeBodyMember_ViaNestedClassifier).ToList());
        Assert.Empty(ResolveTypeBodyMember_Handled.Intersect(ResolveTypeBodyMember_Skipped).ToList());
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap1 = ResolveDeclaration_Handled.Intersect(ResolveDeclaration_Skipped).ToList();
        Assert.Empty(overlap1);

        var overlap2 = ResolveNestedTypeDeclaration_Handled
            .Intersect(ResolveNestedTypeDeclaration_Skipped).ToList();
        Assert.Empty(overlap2);
    }

    [Fact]
    public void ResolveNestedTypeDeclaration_IsSubsetOf_ResolveDeclaration()
    {
        var notInResolve = ResolveNestedTypeDeclaration_Handled
            .Except(ResolveDeclaration_Handled).ToList();
        Assert.True(notInResolve.Count == 0,
            $"ResolveNestedTypeDeclaration handles types not in ResolveDeclaration: " +
            $"{string.Join(", ", notInResolve)}");
    }
}
