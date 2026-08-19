using Sharpy.Compiler.Discovery.Caching;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Unit coverage for the pure helpers behind the A1 interop conformance sweep
/// (<see cref="InteropConformanceTests"/>). These exercise the type-argument synthesis
/// rules directly so the sweep's widening (#1094) rests on a verified mapping rather than
/// only the aggregate ratchet.
///
/// Named to stay OFF the <c>InteropConformance</c> FQN substring that the main CI lane
/// excludes (#1556). These are fast helper unit tests that belong in the main lane.
/// </summary>
public class TypeArgumentSynthesisTests
{
    private static TypeParameterInfo Param(
        string name = "T",
        bool valueType = false,
        bool referenceType = false,
        bool defaultCtor = false,
        params string[] interfaces)
        => new()
        {
            Name = name,
            HasValueTypeConstraint = valueType,
            HasReferenceTypeConstraint = referenceType,
            HasDefaultConstructorConstraint = defaultCtor,
            InterfaceConstraints = interfaces.ToList(),
        };

    [Fact]
    public void NoTypeParameters_YieldsEmptyArray()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new List<TypeParameterInfo>());
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void UnconstrainedParameter_SynthesizesInt()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param() });
        Assert.Equal(new[] { "int" }, result);
    }

    [Fact]
    public void ValueTypeConstraint_SynthesizesInt()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param(valueType: true) });
        Assert.Equal(new[] { "int" }, result);
    }

    [Fact]
    public void DefaultConstructorConstraint_SynthesizesInt()
    {
        // int satisfies `new()`, so a bare new() constraint is synthesizable.
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param(defaultCtor: true) });
        Assert.Equal(new[] { "int" }, result);
    }

    [Fact]
    public void ReferenceTypeConstraint_SynthesizesStr()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param(referenceType: true) });
        Assert.Equal(new[] { "str" }, result);
    }

    [Fact]
    public void ReferenceTypePlusDefaultConstructor_IsOutOfScope()
    {
        // `class` + `new()` needs a reference type with a parameterless ctor; str (String) has
        // none, so synthesis bails rather than emit code Roslyn would reject.
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param(referenceType: true, defaultCtor: true) });
        Assert.Null(result);
    }

    [Fact]
    public void InterfaceConstraint_IsOutOfScope()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[] { Param(interfaces: "IComparable`1") });
        Assert.Null(result);
    }

    [Fact]
    public void MultipleParameters_AllSynthesizable_PreservesOrder()
    {
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[]
        {
            Param("T"),
            Param("U", referenceType: true),
            Param("V", valueType: true),
        });
        Assert.Equal(new[] { "int", "str", "int" }, result);
    }

    [Fact]
    public void MultipleParameters_OneUnsynthesizable_FailsWhole()
    {
        // One interface-constrained parameter poisons the whole synthesis: an all-or-nothing
        // call cannot be rendered, so the caller keeps the member notAttempted.
        var result = InteropConformanceTests.TrySynthesizeTypeArguments(new[]
        {
            Param("T"),
            Param("U", interfaces: "IEquatable`1"),
        });
        Assert.Null(result);
    }
}
