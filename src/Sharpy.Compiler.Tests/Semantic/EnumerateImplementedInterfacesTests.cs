using System;
using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit coverage for <see cref="GenericInstantiationWalker.EnumerateImplementedInterfaces"/> (#1342)
/// — the interface-channel counterpart to <see cref="GenericInstantiationWalker.EnumerateBaseChain"/>
/// for a <see cref="TypeSymbol"/> receiver. These are the direct facts the Self-in-interface bridge
/// reads: which interfaces a (possibly non-generic) class implements, instantiated at the arguments
/// its base clause pinned, including interfaces reached through the base chain.
/// </summary>
public class EnumerateImplementedInterfacesTests
{
    private static TypeSymbol Iface(string name, params string[] typeParameters) => new()
    {
        Name = name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Interface,
        TypeParameters = typeParameters.Select(p => new TypeParameterDef { Name = p }).ToList()
    };

    private static TypeSymbol Class(string name, params string[] typeParameters) => new()
    {
        Name = name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        TypeParameters = typeParameters.Select(p => new TypeParameterDef { Name = p }).ToList()
    };

    private static InterfaceReference Implements(TypeSymbol iface, params SemanticType[] args) => new()
    {
        Definition = iface,
        ResolvedTypeArguments = args.Length == 0
            ? ImmutableArray<SemanticType>.Empty
            : ImmutableArray.CreateRange(args)
    };

    private static string Display(GenericInstantiationWalker.InstantiatedSupertype s)
        => s.TypeArguments.Count == 0
            ? s.Definition.Name
            : $"{s.Definition.Name}[{string.Join(",", s.TypeArguments.Select(a => a.GetDisplayName()))}]";

    [Fact]
    public void DirectGenericInterface_InstantiatedAtBaseClauseArguments()
    {
        // `class Box(IBuilder[int])` implements IBuilder[int], not the open IBuilder[T].
        var builder = Iface("IBuilder", "T");
        var box = Class("Box");
        box.Interfaces.Add(Implements(builder, SemanticType.Int));

        var result = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(box, Array.Empty<SemanticType>())
            .Select(Display)
            .ToList();

        result.Should().ContainSingle().Which.Should().Be("IBuilder[int32]");
    }

    [Fact]
    public void InterfaceThroughBaseClass_IsImplementedByTheDerivedClass()
    {
        // `class Derived(Base)` over `class Base(IBuilder[int])` — Derived implements IBuilder[int]
        // even though it names no interface of its own.
        var builder = Iface("IBuilder", "T");
        var baseClass = Class("Base");
        baseClass.Interfaces.Add(Implements(builder, SemanticType.Int));

        var derived = Class("Derived");
        derived.BaseType = baseClass;
        derived.BaseTypeRef = new BaseTypeReference { Definition = baseClass };

        var result = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(derived, Array.Empty<SemanticType>())
            .Select(Display)
            .ToList();

        result.Should().Contain("IBuilder[int32]");
    }

    [Fact]
    public void TwoLevelComposition_ForwardsThroughAGenericMiddle()
    {
        // `class Mid[T](IBuilder[T])`, `class Leaf(Mid[int])` reaches IBuilder[int] only because
        // Leaf's `{T -> int}` map is applied to Mid's written interface argument `T`.
        var builder = Iface("IBuilder", "T");
        var mid = Class("Mid", "T");
        mid.Interfaces.Add(Implements(builder, new TypeParameterType { Name = "T" }));

        var leaf = Class("Leaf");
        leaf.BaseType = mid;
        leaf.BaseTypeRef = new BaseTypeReference
        {
            Definition = mid,
            ResolvedTypeArguments = ImmutableArray.Create<SemanticType>(SemanticType.Int)
        };

        var result = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(leaf, Array.Empty<SemanticType>())
            .Select(Display)
            .ToList();

        result.Should().Contain("IBuilder[int32]");
        result.Should().NotContain("IBuilder[T]");
    }

    [Fact]
    public void GenericReceiver_SubstitutesItsOwnArguments()
    {
        // A generic implementing class is instantiated at its own arguments: `Box[int]` over
        // `class Box[T](IBuilder[T])` implements IBuilder[int].
        var builder = Iface("IBuilder", "T");
        var box = Class("Box", "T");
        box.Interfaces.Add(Implements(builder, new TypeParameterType { Name = "T" }));

        var result = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(box, new SemanticType[] { SemanticType.Int })
            .Select(Display)
            .ToList();

        result.Should().Contain("IBuilder[int32]");
    }

    [Fact]
    public void CycleInTheInterfaceGraph_Terminates()
    {
        // A malformed hierarchy (IA : IB, IB : IA) must not loop forever.
        var ia = Iface("IA");
        var ib = Iface("IB");
        ia.Interfaces.Add(Implements(ib));
        ib.Interfaces.Add(Implements(ia));

        var box = Class("Box");
        box.Interfaces.Add(Implements(ia));

        var result = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(box, Array.Empty<SemanticType>())
            .Select(Display)
            .ToList();

        result.Should().Contain("IA");
        result.Should().Contain("IB");
    }

    [Fact]
    public void ArityMismatch_YieldsNothing_RatherThanAWrongAnswer()
    {
        // Type parameters that do not match the supplied arguments produce no interfaces, mirroring
        // EnumerateBaseChain's "no wrong answer" discipline.
        var box = Class("Box", "T");

        GenericInstantiationWalker
            .EnumerateImplementedInterfaces(box, Array.Empty<SemanticType>())
            .Should().BeEmpty();
    }
}
