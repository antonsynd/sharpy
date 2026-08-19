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
        ["SelfType"] = "OutOfScope: never reaches serialization; the NotSupportedException throw at SymbolSerializer:1457 is the loud backstop",
        ["ConstructorReferenceType"] = "OutOfScope: never reaches serialization; pinned or rejected (SPY0342) in semantic analysis",
        ["LiteralStringType"] = "OutOfScope: compile-time-only distinction; emits as string, never serialized",
        ["ModuleType"] = "OutOfScope: modules are not serialized as types; the NotSupportedException throw is the backstop",
        ["UnionType"] = "OutOfScope: not yet supported in code generation; the NotSupportedException throw is the backstop",
    };

    [Fact]
    public void EveryConcreteSemanticType_IsClassified()
    {
        var concreteTypes = typeof(SemanticType).Assembly
            .GetTypes()
            .Where(t => t.IsSealed && !t.IsAbstract && typeof(SemanticType).IsAssignableFrom(t))
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
    public void RoundTripsEntries_AreActuallyRegistered()
    {
        var roundTrips = Classified.Where(kv => kv.Value == "RoundTrips").Select(kv => kv.Key).ToList();

        foreach (var typeName in roundTrips)
        {
            var concreteType = typeof(SemanticType).Assembly.GetTypes()
                .First(t => t.Name == typeName && typeof(SemanticType).IsAssignableFrom(t));

            // Verify the type has a codec registered by checking the Serialize method doesn't throw
            // for a default-constructed instance (or the singleton).
            var specimen = CreateSpecimen(concreteType);
            if (specimen == null) continue;

            var act = () => SymbolSerializer.TypeCodecRegistry.Serialize(specimen);
            act.Should().NotThrow($"{typeName} is classified as RoundTrips but has no serializer registration");
        }
    }

    private static SemanticType? CreateSpecimen(Type concreteType)
    {
        if (concreteType == typeof(UnknownType)) return SemanticType.Unknown;
        if (concreteType == typeof(VoidType)) return SemanticType.Void;
        if (concreteType == typeof(BuiltinType)) return SemanticType.Int;
        if (concreteType == typeof(GenericType)) return new GenericType { Name = "list", TypeArguments = { SemanticType.Int } };
        if (concreteType == typeof(UserDefinedType)) return new UserDefinedType { Name = "MyType" };
        if (concreteType == typeof(UnmappedClrType)) return new UnmappedClrType { ClrTypeName = "System.Test" };
        if (concreteType == typeof(NullableType)) return new NullableType { UnderlyingType = SemanticType.Int };
        if (concreteType == typeof(OptionalType)) return new OptionalType { UnderlyingType = SemanticType.Int };
        if (concreteType == typeof(ResultType)) return new ResultType { OkType = SemanticType.Int, ErrorType = SemanticType.Str };
        if (concreteType == typeof(FunctionType)) return new FunctionType { ParameterTypes = { SemanticType.Int }, ReturnType = SemanticType.Str };
        if (concreteType == typeof(TupleType)) return new TupleType { ElementTypes = { SemanticType.Int, SemanticType.Str } };
        if (concreteType == typeof(TaskType)) return new TaskType { ResultType = SemanticType.Int };
        if (concreteType == typeof(TemplateType)) return TemplateType.Instance;
        if (concreteType == typeof(TypeParameterType)) return new TypeParameterType { Name = "T" };
        return null;
    }
}
