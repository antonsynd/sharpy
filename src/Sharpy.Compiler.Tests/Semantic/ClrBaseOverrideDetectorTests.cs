using System.Collections.Generic;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for <see cref="ClrBaseOverrideDetector"/> — the D2 matching rule that decides whether a
/// <c>.spy</c> instance method overrides an abstract/virtual member of a CLR-backed base type
/// (#1122). Uses synthetic hierarchies so the rule is covered without runtime CLR discovery.
/// </summary>
public class ClrBaseOverrideDetectorTests
{
    // ─── Helpers ─────────────────────────────────────────────────────

    /// <summary>A CLR-backed base type carrying the given methods (ClrType is set).</summary>
    private static TypeSymbol MakeClrBase(string name, params FunctionSymbol[] methods)
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(object), // any non-null CLR Type marks the base as CLR-backed
            Methods = new List<FunctionSymbol>(methods)
        };
    }

    /// <summary>A pure-Sharpy base type (no ClrType) carrying the given methods.</summary>
    private static TypeSymbol MakeSharpyBase(string name, params FunctionSymbol[] methods)
    {
        return new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            Methods = new List<FunctionSymbol>(methods)
        };
    }

    private static FunctionSymbol MakeMethod(
        string name,
        int arity,
        bool isAbstract = false,
        bool isVirtual = false,
        bool isStatic = false,
        string? clrMethodName = null)
    {
        var parameters = new List<ParameterSymbol>();
        for (int i = 0; i < arity; i++)
            parameters.Add(new ParameterSymbol { Name = $"p{i}" });

        return new FunctionSymbol
        {
            Name = name,
            Kind = SymbolKind.Function,
            Parameters = parameters,
            IsAbstract = isAbstract,
            IsVirtual = isVirtual,
            IsStatic = isStatic,
            ClrMethodName = clrMethodName
        };
    }

    // ─── Abstract CLR member matched ─────────────────────────────────

    [Fact]
    public void AbstractClrMember_SameNameAndArity_IsMatched()
    {
        // Mirrors SourceGenerator.Generate(GeneratorContext) → def generate(self, context).
        var baseType = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: new[] { baseType });

        matched.Should().BeTrue();
    }

    // ─── Virtual CLR member matched ──────────────────────────────────

    [Fact]
    public void VirtualClrMember_SameNameAndArity_IsMatched()
    {
        var baseType = MakeClrBase("Widget",
            MakeMethod("render", arity: 0, isVirtual: true, clrMethodName: "Render"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "render", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 0, baseTypes: new[] { baseType });

        matched.Should().BeTrue();
    }

    [Fact]
    public void ClrMember_MatchedThroughClrMethodName_WhenBaseTypeLacksClrType()
    {
        // A bridged member can carry ClrMethodName even if its owning TypeSymbol shell has no
        // ClrType set; the scope guard accepts either signal.
        var baseType = MakeSharpyBase("Bridged",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: new[] { baseType });

        matched.Should().BeTrue();
    }

    // ─── Static method NOT matched ───────────────────────────────────

    [Fact]
    public void StaticDerivedMethod_IsNotMatched()
    {
        var baseType = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: true, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    [Fact]
    public void StaticBaseMember_IsNotMatched()
    {
        // A static base member is not overridable even if name/arity line up.
        var baseType = MakeClrBase("Factory",
            MakeMethod("create", arity: 0, isStatic: true, clrMethodName: "Create"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "create", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 0, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    // ─── Unrelated name NOT matched ──────────────────────────────────

    [Fact]
    public void UnrelatedMethodName_IsNotMatched()
    {
        var baseType = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "helper", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    [Fact]
    public void MatchingNameButDifferentArity_IsNotMatched()
    {
        var baseType = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 2, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    // ─── Pure-Sharpy base NOT matched (scope guard) ──────────────────

    [Fact]
    public void PureSharpyBase_VirtualMember_IsNotMatched()
    {
        // A same-name virtual member on a non-CLR base keeps today's decorator-driven behavior.
        var baseType = MakeSharpyBase("Animal",
            MakeMethod("speak", arity: 0, isVirtual: true));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "speak", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 0, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    // ─── Non-overridable CLR members ─────────────────────────────────

    [Fact]
    public void NonAbstractNonVirtualClrMember_IsNotMatched()
    {
        // A plain (non-virtual, non-abstract) CLR method cannot be overridden.
        var baseType = MakeClrBase("Sealed",
            MakeMethod("compute", arity: 0, clrMethodName: "Compute"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "compute", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 0, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    // ─── Guards ──────────────────────────────────────────────────────

    [Fact]
    public void AlreadyOverride_ShortCircuits_ToFalse()
    {
        // Decorator/dunder override path already handles the token — no double handling.
        var baseType = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: true,
            arity: 1, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    [Fact]
    public void InitConstructor_IsNotMatched()
    {
        var baseType = MakeClrBase("Base",
            MakeMethod("__init__", arity: 0, isVirtual: true, clrMethodName: "ctor"));

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "__init__", isStatic: false, isInitConstructor: true, isAlreadyOverride: false,
            arity: 0, baseTypes: new[] { baseType });

        matched.Should().BeFalse();
    }

    [Fact]
    public void NoBaseTypes_IsNotMatched()
    {
        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: System.Array.Empty<TypeSymbol>());

        matched.Should().BeFalse();
    }

    [Fact]
    public void DeeperClrBaseInChain_IsMatched()
    {
        // The CLR-backed base can be an ancestor further up the chain.
        var clrRoot = MakeClrBase("SourceGenerator",
            MakeMethod("generate", arity: 1, isAbstract: true, clrMethodName: "Generate"));
        var sharpyMiddle = MakeSharpyBase("Intermediate");

        var matched = ClrBaseOverrideDetector.ShouldEmitClrOverride(
            "generate", isStatic: false, isInitConstructor: false, isAlreadyOverride: false,
            arity: 1, baseTypes: new[] { sharpyMiddle, clrRoot });

        matched.Should().BeTrue();
    }
}
