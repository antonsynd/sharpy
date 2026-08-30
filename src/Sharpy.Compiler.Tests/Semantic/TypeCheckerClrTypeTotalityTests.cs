using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <c>TypeChecker.TryGetClrType</c>: every concrete
/// <see cref="SemanticType"/> subtype must be classified as either explicitly handled
/// (has a dedicated case arm) or default-handled (falls through to
/// <c>type.ClrType ?? type.DeclaringSymbol?.ClrType</c>). A new SemanticType subtype
/// that is not listed here will fail this test (#1694).
/// <para>
/// <c>ExtractNarrowedTypes</c> is N/A for this guard: it uses an if-chain, not a switch,
/// so SwitchArmScan cannot derive its dispatch roster. The if-chain's totality is covered
/// by the narrowing flow analysis tests.
/// </para>
/// </summary>
public class TypeCheckerClrTypeTotalityTests
{
    private const string SourceFile =
        "src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs";

    private readonly ITestOutputHelper _output;

    public TypeCheckerClrTypeTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly HashSet<string> Handled = new()
    {
        nameof(BuiltinType),
        nameof(UnmappedClrType),
        nameof(UserDefinedType),
        nameof(NullableType),
        nameof(OptionalType),
        nameof(TupleType),
        nameof(GenericType),
    };

    private static readonly HashSet<string> Default = new()
    {
        nameof(UnknownType),
        nameof(VoidType),
        nameof(ResultType),
        nameof(FunctionType),
        nameof(ModuleType),
        nameof(TypeParameterType),
        nameof(SelfType),
        nameof(GenericFunctionType),
        nameof(ConstructorReferenceType),
        nameof(UnionType),
        nameof(TaskType),
        nameof(TemplateType),
        nameof(LiteralStringType),
    };

    [Fact]
    public void AllConcreteSemanticTypeSubtypes_AreClassified()
    {
        var semanticTypeBase = typeof(SemanticType);
        var assembly = semanticTypeBase.Assembly;

        var concreteTypes = assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(semanticTypeBase)
                        && !t.IsAbstract
                        && t.IsPublic)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var allClassified = new HashSet<string>(Handled);
        allClassified.UnionWith(Default);

        var unclassified = concreteTypes.Where(n => !allClassified.Contains(n)).ToList();
        var phantom = allClassified.Where(n => !concreteTypes.Contains(n)).ToList();

        _output.WriteLine($"Concrete SemanticType subtypes: {concreteTypes.Count}");
        foreach (var name in concreteTypes)
        {
            var group = Handled.Contains(name) ? "HANDLED"
                : Default.Contains(name) ? "DEFAULT"
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
    public void SwitchArms_MatchHandledSet()
    {
        var switchArms = SwitchArmScan.CaseTypeNames(SourceFile, "TryGetClrType");
        Assert.NotEmpty(switchArms);

        var missing = Handled.Except(switchArms).ToList();
        Assert.True(missing.Count == 0,
            $"Handled types missing from TryGetClrType switch: {string.Join(", ", missing)}");

        Assert.True(switchArms.SetEquals(Handled),
            $"TryGetClrType switch arms differ from expected.\n" +
            $"  Extra in switch: {string.Join(", ", switchArms.Except(Handled))}\n" +
            $"  Missing from switch: {string.Join(", ", Handled.Except(switchArms))}");
    }

    [Fact]
    public void ClassificationSets_AreDisjoint()
    {
        var overlap = Handled.Intersect(Default).ToList();
        Assert.Empty(overlap);
    }
}
