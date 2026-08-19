using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Every concrete <see cref="SemanticType"/> subclass must have an entry in the serializer's
/// <c>TypeCodecRegistry</c> — either channel-registered and round-tripping, documented-lossy
/// with a rationale, or out-of-scope with a rationale (#1555). A new subclass with no entry
/// fails the census.
/// </summary>
public class SemanticTypeSerializerTotalityTests
{
    private static readonly Dictionary<string, string> Classified = new()
    {
        ["UnknownType"] = "RoundTrips",
        ["VoidType"] = "RoundTrips",
        ["BuiltinType"] = "RoundTrips",
        ["GenericType"] = "RoundTrips",
        ["UserDefinedType"] = "RoundTrips",
        ["UnmappedClrType"] = "RoundTrips",
        ["NullableType"] = "RoundTrips",
        ["OptionalType"] = "RoundTrips",
        ["ResultType"] = "RoundTrips",
        ["FunctionType"] = "RoundTrips",
        ["TupleType"] = "RoundTrips",
        ["TaskType"] = "RoundTrips",
        ["TemplateType"] = "RoundTrips",
        ["TypeParameterType"] = "RoundTrips",
        ["GenericFunctionType"] = "DocumentedLossy: decodes to GenericType by design (SymbolSerializer:1408); the function signature is reconstructed at use site",
        ["ModuleType"] = "DocumentedLossy: registered channel encodes Symbol.Name only and decodes to a placeholder name-only ModuleSymbol (SymbolSerializer Register<ModuleType>); record equality compares Symbol by reference, so a round trip cannot attest it",
        ["UnionType"] = "DocumentedLossy: registered placeholder channel for v0.2.x tagged unions decodes Name+CaseTypes structurally, but UnionType has no structural Equals over CaseTypes, so record equality cannot attest it; revisit when tagged unions land",
        ["SelfType"] = "OutOfScope: never reaches serialization; the NotSupportedException throw in SymbolSerializer's Serialize is the loud backstop",
        ["ConstructorReferenceType"] = "OutOfScope: never reaches serialization; pinned or rejected (SPY0342) in semantic analysis",
        ["LiteralStringType"] = "OutOfScope: compile-time-only distinction; emits as string, never serialized",
    };

    [Fact]
    public void EveryConcreteSemanticType_IsClassified()
    {
        // Deliberately NOT filtered to sealed types: an unsealed concrete subclass added tomorrow
        // must fail the census, not slip past it.
        var concreteTypes = typeof(SemanticType).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(SemanticType).IsAssignableFrom(t))
            .Select(t => t.Name)
            .OrderBy(n => n)
            .ToList();

        concreteTypes.Should().NotBeEmpty("reflection must find the sealed SemanticType subclasses");

        var unclassified = concreteTypes.Where(n => !Classified.ContainsKey(n)).ToList();
        unclassified.Should().BeEmpty(
            "every concrete SemanticType subclass must be classified in this census — "
            + "add it as RoundTrips (with a serializer channel), DocumentedLossy (with a rationale), "
            + "or OutOfScope (with a rationale). Unclassified:\n  "
            + string.Join("\n  ", unclassified));

        var ghosts = Classified.Keys.Where(k => !concreteTypes.Contains(k)).ToList();
        ghosts.Should().BeEmpty(
            "every entry in the census must name a real concrete SemanticType subclass. "
            + "Ghosts (deleted types still in the census):\n  "
            + string.Join("\n  ", ghosts));
    }

    [Fact]
    public void RoundTripsEntries_SurviveSerializerRoundTrip()
    {
        var roundTrips = Classified.Where(kv => kv.Value == "RoundTrips").Select(kv => kv.Key).ToList();
        var failed = new List<string>();

        foreach (var typeName in roundTrips)
        {
            var specimen = CreateSpecimen(typeName);
            if (specimen == null)
            {
                // A RoundTrips entry with no specimen would pass vacuously — that is a census hole,
                // not a pass.
                failed.Add($"{typeName}: classified RoundTrips but CreateSpecimen has no arm for it");
                continue;
            }

            var symbol = new FunctionSymbol
            {
                Name = "probe",
                Kind = SymbolKind.Function,
                Parameters = new List<ParameterSymbol>(),
                ReturnType = specimen,
            };

            try
            {
                var cached = SymbolSerializer.Serialize(symbol, "/test.spy");
                var restored = (FunctionSymbol)SymbolSerializer.Deserialize(
                    cached, new Dictionary<string, Symbol>(StringComparer.Ordinal));
                if (!Equals(specimen, restored.ReturnType))
                    failed.Add($"{typeName}: decoded to "
                        + $"{restored.ReturnType?.GetType().Name ?? "null"} "
                        + $"'{restored.ReturnType?.GetDisplayName()}', not record-equal to the specimen");
            }
            catch (Exception ex)
            {
                failed.Add($"{typeName}: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        failed.Should().BeEmpty("all RoundTrips entries must survive serialization record-equal");
    }

    private static SemanticType? CreateSpecimen(string typeName) => typeName switch
    {
        "UnknownType" => SemanticType.Unknown,
        "VoidType" => SemanticType.Void,
        "BuiltinType" => SemanticType.Int,
        "GenericType" => new GenericType { Name = "list", TypeArguments = { SemanticType.Int } },
        "UserDefinedType" => new UserDefinedType { Name = "MyType" },
        // A RESOLVABLE name: the decoder restores ClrType via the shared origin resolver, and
        // record equality (which includes ClrType) only holds if resolution actually ran.
        "UnmappedClrType" => new UnmappedClrType
        {
            ClrTypeName = "System.Text.StringBuilder",
            ClrType = typeof(System.Text.StringBuilder)
        },
        "NullableType" => new NullableType { UnderlyingType = SemanticType.Int },
        "OptionalType" => new OptionalType { UnderlyingType = SemanticType.Int },
        "ResultType" => new ResultType { OkType = SemanticType.Int, ErrorType = SemanticType.Str },
        "FunctionType" => new FunctionType { ParameterTypes = { SemanticType.Int }, ReturnType = SemanticType.Str },
        "TupleType" => new TupleType { ElementTypes = { SemanticType.Int, SemanticType.Str } },
        "TaskType" => new TaskType { ResultType = SemanticType.Int },
        "TemplateType" => TemplateType.Instance,
        "TypeParameterType" => new TypeParameterType { Name = "T" },
        _ => null,
    };
}
