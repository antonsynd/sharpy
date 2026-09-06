using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality guard for <see cref="ConstEligibility.IsConstEligibleSemanticType"/> (#1791, #1782).
///
/// <para><b>Contract.</b> The const-eligible type set is every <c>PrimitiveCatalog</c> primitive
/// except <c>object</c> and <c>void</c>, plus enum types. This test enumerates the catalog, asserts
/// the eligible/excluded sets by name, and verifies that enum types pass.</para>
///
/// <para><b>Exclusion set.</b> The two excluded primitives (<c>object</c>, <c>void</c>/<c>None</c>)
/// are named explicitly so adding a primitive to the catalog without classifying it here fails the
/// test -- the new name appears in neither the eligible nor the excluded set.</para>
/// </summary>
public class ConstEligibleTypeTotalityTests
{
    // Anchored counts so the test is not "the enum against itself".
    // Distinct canonical primitives (not counting aliases):
    //   int8, int16, int32, int64, uint8, uint16, uint32, uint64,
    //   float32, float64, decimal, bool, char, str, object, void = 16
    // The catalog registers aliases (int, long, sbyte, short, byte, ushort, uint, ulong,
    // float, double, string, None) that map to the same CLR types -- those are NOT
    // double-counted here because the predicate is on SemanticType, not on catalog names.
    private const int EligibleCanonicalCount = 14; // 16 - object - void
    private const int ExcludedCanonicalCount = 2;  // object, void

    /// <summary>
    /// The excluded names: object and void/None. Every catalog entry whose CLR type is one of
    /// these must NOT be const-eligible; every other catalog entry MUST be.
    /// </summary>
    private static readonly HashSet<Type> ExcludedClrTypes = new()
    {
        typeof(object),
        typeof(void),
    };

    [Fact]
    public void EligibleSet_CoversEveryPrimitiveCatalogEntryExceptObjectAndVoid()
    {
        // Group by CLR type to deduplicate aliases.
        var grouped = PrimitiveCatalog.GetAllPrimitives()
            .GroupBy(p => p.Info.ClrType)
            .Select(g => (ClrType: g.Key, Rep: g.First()))
            .ToList();

        var eligible = grouped.Where(g => !ExcludedClrTypes.Contains(g.ClrType)).ToList();
        var excluded = grouped.Where(g => ExcludedClrTypes.Contains(g.ClrType)).ToList();

        eligible.Count.Should().Be(EligibleCanonicalCount,
            "the eligible count is anchored to a literal, not derived from the catalog");
        excluded.Count.Should().Be(ExcludedCanonicalCount,
            "the excluded count is anchored to a literal");
        (eligible.Count + excluded.Count).Should().Be(grouped.Count,
            "every catalog entry is in exactly one set");

        foreach (var (_, (name, info)) in eligible)
        {
            var builtinType = new BuiltinType { Name = name };
            ConstEligibility.IsConstEligibleSemanticType(builtinType).Should().BeTrue(
                $"'{name}' (CLR: {info.ClrType.Name}) is a const-eligible primitive");
        }

        foreach (var (_, (name, _)) in excluded)
        {
            var builtinType = new BuiltinType { Name = name };
            ConstEligibility.IsConstEligibleSemanticType(builtinType).Should().BeFalse(
                $"'{name}' is excluded from const eligibility");
        }
    }

    [Fact]
    public void EnumTypes_AreConstEligible()
    {
        var enumSymbol = new TypeSymbol { Name = "TestEnum", TypeKind = TypeKind.Enum };
        var enumType = new UserDefinedType { Name = "TestEnum", Symbol = enumSymbol };

        ConstEligibility.IsConstEligibleSemanticType(enumType).Should().BeTrue(
            "enum types are const-eligible (C# admits const for enums)");
    }

    [Fact]
    public void NonPrimitiveNonEnumTypes_AreNotConstEligible()
    {
        var classSymbol = new TypeSymbol { Name = "MyClass", TypeKind = TypeKind.Class };
        var classType = new UserDefinedType { Name = "MyClass", Symbol = classSymbol };

        ConstEligibility.IsConstEligibleSemanticType(classType).Should().BeFalse(
            "class types are not const-eligible");

        var structSymbol = new TypeSymbol { Name = "MyStruct", TypeKind = TypeKind.Struct };
        var structType = new UserDefinedType { Name = "MyStruct", Symbol = structSymbol };

        ConstEligibility.IsConstEligibleSemanticType(structType).Should().BeFalse(
            "struct types are not const-eligible");

        ConstEligibility.IsConstEligibleSemanticType(SemanticType.Unknown).Should().BeFalse(
            "UnknownType is not const-eligible");
    }
}
