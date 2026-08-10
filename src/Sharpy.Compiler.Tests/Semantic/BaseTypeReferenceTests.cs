using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Unit coverage for <see cref="BaseTypeReference"/> (#1287) — the record itself, its two-channel
/// precedence, and its trip through the <see cref="SemanticBinding"/> store and materialization.
///
/// <para>
/// The record had no unit tests at all: every existing assertion about it went through a full
/// compilation, so a change to the channel precedence or a materialization miss could only be
/// caught indirectly, as a wrong verdict several phases later. These are the direct facts.
/// </para>
/// </summary>
public class BaseTypeReferenceTests
{
    private static SymbolTable CreateSymbolTable() => new(new BuiltinRegistry());

    private static TypeSymbol Type(string name, params string[] typeParameters) => new()
    {
        Name = name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        TypeParameters = typeParameters.Select(p => new TypeParameterDef { Name = p }).ToList()
    };

    [Fact]
    public void DefaultChannels_AreEmpty_NotDefault()
    {
        // Both channels are ImmutableArray, whose *default* value throws on enumeration. Every
        // consumer tests IsDefaultOrEmpty, but a caller that builds the record and then iterates
        // must not need to.
        var reference = new BaseTypeReference { Definition = Type("Box", "T") };

        reference.TypeArgAnnotations.IsDefault.Should().BeFalse();
        reference.ResolvedTypeArguments.IsDefault.Should().BeFalse();
        reference.TypeArgAnnotations.Should().BeEmpty();
        reference.ResolvedTypeArguments.Should().BeEmpty();
    }

    [Fact]
    public void BothChannels_CanBePopulatedIndependently()
    {
        // Source-declared bases fill the annotation channel; CLR-discovered bases fill the resolved
        // one. Nothing forbids both, and the record must not conflate them.
        var reference = new BaseTypeReference
        {
            Definition = Type("Box", "T"),
            TypeArgAnnotations = ImmutableArray.Create(new TypeAnnotation { Name = "int" }),
            ResolvedTypeArguments = ImmutableArray.Create<SemanticType>(SemanticType.Str)
        };

        reference.TypeArgAnnotations.Single().Name.Should().Be("int");
        reference.ResolvedTypeArguments.Single().Should().Be(SemanticType.Str);
    }

    [Fact]
    public void Store_RoundTripsThroughMaterialization()
    {
        // The store is parallel to SetBaseType and is materialized in the same pass. A reference
        // written before the freeze must be readable off the Symbol after it — this is the step
        // DualWriteAssertions guards, asserted here directly rather than through a compilation.
        var symbolTable = CreateSymbolTable();
        var binding = new SemanticBinding();

        var box = Type("Box", "T");
        var intBox = Type("IntBox");
        symbolTable.Define(box);
        symbolTable.Define(intBox);

        var reference = new BaseTypeReference
        {
            Definition = box,
            TypeArgAnnotations = ImmutableArray.Create(new TypeAnnotation { Name = "int" })
        };

        binding.SetBaseType(intBox, box);
        binding.SetBaseTypeReference(intBox, reference);

        intBox.BaseTypeRef.Should().BeNull("materialization has not run yet");

        binding.MaterializeInheritance();

        intBox.BaseTypeRef.Should().BeSameAs(reference);
        intBox.BaseTypeRef!.TypeArgAnnotations.Single().Name.Should().Be("int");
        binding.GetBaseTypeReference(intBox).Should().BeSameAs(reference);
    }

    [Fact]
    public void Store_LastWriteWins_SoLateResolutionCanRefineTheReference()
    {
        // InheritanceResolver rewrites the reference when it resolves a base that arrived
        // unresolved (from the symbol cache or a cross-module import). That overwrite has to take.
        var binding = new SemanticBinding();
        var box = Type("Box", "T");
        var derived = Type("Derived");

        binding.SetBaseTypeReference(derived, new BaseTypeReference { Definition = box });
        var refined = new BaseTypeReference
        {
            Definition = box,
            TypeArgAnnotations = ImmutableArray.Create(new TypeAnnotation { Name = "str" })
        };
        binding.SetBaseTypeReference(derived, refined);

        binding.GetBaseTypeReference(derived).Should().BeSameAs(refined);

        binding.MaterializeInheritance();
        derived.BaseTypeRef!.TypeArgAnnotations.Single().Name.Should().Be("str");
    }

    [Fact]
    public void GetBaseTypeReference_IsNullForAnUnwrittenSymbol()
    {
        // The negative control for the store: absence must read as null rather than as an empty
        // reference, because the walker distinguishes "no arguments written" from "no reference".
        var binding = new SemanticBinding();
        binding.GetBaseTypeReference(Type("Lonely")).Should().BeNull();
    }
}
