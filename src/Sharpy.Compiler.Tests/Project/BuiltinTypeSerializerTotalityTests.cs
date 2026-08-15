using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The serializer's <see cref="BuiltinType"/> channel must be TOTAL: every builtin the compiler can
/// produce has to survive <c>Serialize</c> → <c>Deserialize</c> as a record-equal value.
///
/// <para>#1474: the encoder writes a builtin as its <c>Name</c> and the decoder read it back through
/// a hand-written switch spelled in a DIFFERENT naming channel — seven arms against fourteen
/// singletons plus the catalog's alias spellings, with <c>_ =&gt; SemanticType.Unknown</c> swallowing
/// everything else. `x: long` in a cached file came back <c>Unknown</c> on the next incremental
/// build, silently. The two channels could drift because nothing held them against each other.</para>
///
/// <para>This is that instrument. It enumerates the specimen set BY REFLECTION over
/// <see cref="SemanticType"/>'s public static fields plus every <see cref="PrimitiveCatalog"/> name —
/// deliberately NOT a hand-kept list, so it cannot fall behind the thing it measures, and so it
/// survives #1356's singleton collapse unchanged (the census follows the new singleton set on its
/// own).</para>
///
/// <para>The contract is IDEMPOTENCE, not merely "decodes to something": record equality on
/// <see cref="BuiltinType"/> includes <c>Name</c>, so decoding <c>"long"</c> to the <c>"int64"</c>-named
/// singleton is a failure, not a near-miss. A warm build that renames a type has not round-tripped
/// it.</para>
/// </summary>
public class BuiltinTypeSerializerTotalityTests
{
    private const string SpecimenPath = "/project/lib.spy";

    private readonly ITestOutputHelper _output;

    public BuiltinTypeSerializerTotalityTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every <see cref="BuiltinType"/> the compiler can name, discovered rather than listed:
    /// the singleton fields on <see cref="SemanticType"/>, plus each <see cref="PrimitiveCatalog"/>
    /// name materialized as the builtin a producer would build for it. Aliases (<c>long</c>,
    /// <c>byte</c>, <c>double</c>, …) are exactly the spellings the deleted switch could not read.
    /// </summary>
    private static IEnumerable<(string Label, BuiltinType Type)> Specimens()
    {
        foreach (var field in typeof(SemanticType).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is BuiltinType singleton)
                yield return ($"singleton:SemanticType.{field.Name}", singleton);
        }

        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
            yield return ($"catalog:{name}", new BuiltinType { Name = name, ClrType = info.ClrType });
    }

    /// <summary>
    /// Round-trips a type exactly as the incremental cache does: through a symbol, since
    /// <c>SerializeType</c>/<c>ResolveTypeFromId</c> are private and a test that reached past them
    /// would be measuring a path no build takes.
    /// </summary>
    private static SemanticType RoundTrip(SemanticType type)
    {
        var symbol = new FunctionSymbol
        {
            Name = "probe",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>(),
            ReturnType = type,
        };

        var cached = SymbolSerializer.Serialize(symbol, SpecimenPath);
        var restored = (FunctionSymbol)SymbolSerializer.Deserialize(
            cached, new Dictionary<string, Symbol>(StringComparer.Ordinal));
        return restored.ReturnType;
    }

    [Fact]
    public void EveryBuiltinType_SurvivesTheSymbolCache_RecordEqual()
    {
        var specimens = Specimens().ToList();
        specimens.Should().NotBeEmpty("reflection must find the singleton set — an empty census "
            + "would pass this guard vacuously");

        var lost = new List<string>();
        foreach (var (label, type) in specimens)
        {
            var restored = RoundTrip(type);

            if (restored is UnknownType)
            {
                lost.Add($"{label} ('{type.Name}') decoded to UnknownType — the cache forgot the type");
                continue;
            }

            if (restored != type)
            {
                lost.Add($"{label} ('{type.Name}') decoded to "
                    + $"{restored.GetType().Name} '{restored.GetDisplayName()}' — not record-equal to "
                    + "what was encoded");
            }
        }

        _output.WriteLine($"specimens: {specimens.Count}, lost: {lost.Count}");
        foreach (var entry in lost)
            _output.WriteLine("  " + entry);

        lost.Should().BeEmpty(
            "a warm --incremental build must read back exactly the builtin a cold build wrote. A "
            + "name the decoder cannot resolve becomes UnknownType with no diagnostic, and a name it "
            + "resolves to a DIFFERENTLY-NAMED singleton silently renames the type — both make the "
            + "second build disagree with the first about what the source said (#1474).\nLost:\n  "
            + string.Join("\n  ", lost));
    }

    /// <summary>
    /// The census must be non-vacuous in the direction that matters: the specimen set has to contain
    /// the alias spellings, or the totality assertion above could pass while the alias channel stays
    /// broken. Names the #1474 report calls dead, plus the two non-idempotent arms' spellings.
    /// </summary>
    [Fact]
    public void TheCensus_CoversTheNamesThatWereDead()
    {
        var names = Specimens().Select(s => s.Type.Name).ToHashSet(StringComparer.Ordinal);

        var required = new[]
        {
            // The nine encoded names with no switch arm (#1474's report).
            "int64", "float64", "int8", "uint8", "int16", "uint16", "uint32", "uint64", "decimal",
            // The alias spellings the switch answered with a different singleton's name.
            "long", "double",
        };

        names.Should().Contain(required,
            "these are the spellings the deleted switch got wrong; a census that does not carry them "
            + "cannot see the bug it exists to catch");
    }
}
