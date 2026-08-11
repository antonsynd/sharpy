using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The symbol cache carries an unresolved interface reference's written TYPE ARGUMENTS
/// (schema v24, #1403).
///
/// <para><c>class Repo(Comparable[int])</c> means Comparable AT int. A cache entry that recorded
/// only the name "Comparable" would restore an argument-less reference, and
/// <c>InheritanceResolver</c> — which is what re-resolves these on a warm build — would build an
/// <see cref="InterfaceReference"/> with no arguments. Every member reached through the interface
/// then answers with its open type parameter instead of <c>int</c>, so the warm build's verdicts
/// differ from the cold build's. That is exactly the failure #1287 fixed for the base class, whose
/// arguments ride in <c>BaseTypeArgs</c>; this is its interface half.</para>
///
/// <para>A pin rather than a repro, for the same reason #1287's CLR-base cell is: the write and the
/// read are independent paths, and a change to either alone would drop the arguments silently.</para>
/// </summary>
public class SymbolCacheInterfaceArgsTests
{
    private static TypeSymbol RepoWithComparableAtInt() => new()
    {
        Name = "Repo",
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Class,
        UnresolvedInterfaces = new List<TypeAnnotation>
        {
            new()
            {
                Name = "Comparable",
                TypeArguments = ImmutableArray.Create(new TypeAnnotation { Name = "int" })
            },
            new() { Name = "Sized" }
        }
    };

    [Fact]
    public void UnresolvedInterface_KeepsTypeArguments_ThroughJsonRoundTrip()
    {
        var cached = SymbolSerializer.Serialize(RepoWithComparableAtInt(), "lib.spy");

        // Through JSON, not just through the record: the cache is a file, and a property the
        // serializer populates but the DTO does not persist would pass an in-memory assertion.
        var reloaded = JsonSerializer.Deserialize<CachedSymbol>(JsonSerializer.Serialize(cached))!;
        var restored = (TypeSymbol)SymbolSerializer.Deserialize(reloaded, new Dictionary<string, Symbol>());

        restored.UnresolvedInterfaces.Should().HaveCount(2);

        var comparable = restored.UnresolvedInterfaces.Single(a => a.Name == "Comparable");
        comparable.TypeArguments.Select(a => a.Name).Should().Equal("int");

        // The negative half: a non-generic reference must come back argument-less rather than
        // inheriting its neighbour's arguments.
        restored.UnresolvedInterfaces.Single(a => a.Name == "Sized").TypeArguments.Should().BeEmpty();
    }

    [Fact]
    public void RestoredUnresolvedInterface_ResolvesToAReferenceCarryingTheArguments()
    {
        // The two legs joined: what the cache restores is what InheritanceResolver reads, so the
        // warm build's InterfaceReference must equal the cold build's.
        var reloaded = JsonSerializer.Deserialize<CachedSymbol>(
            JsonSerializer.Serialize(SymbolSerializer.Serialize(RepoWithComparableAtInt(), "lib.spy")))!;
        var restored = (TypeSymbol)SymbolSerializer.Deserialize(reloaded, new Dictionary<string, Symbol>());

        var symbolTable = new SymbolTable(new Sharpy.Compiler.Semantic.Registry.BuiltinRegistry());
        var comparable = new TypeSymbol
        {
            Name = "Comparable",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            TypeParameters = new List<TypeParameterDef> { new() { Name = "T" } }
        };
        var sized = new TypeSymbol { Name = "Sized", Kind = SymbolKind.Type, TypeKind = TypeKind.Interface };
        symbolTable.Define(comparable);
        symbolTable.Define(sized);
        symbolTable.Define(restored);

        var binding = new SemanticBinding();
        new InheritanceResolver(symbolTable, semanticBinding: binding).ResolveImportedTypeInheritance();

        var interfaces = binding.GetInterfaces(restored);
        interfaces.Should().HaveCount(2);
        interfaces.Single(r => r.Definition == comparable).TypeArgAnnotations
            .Select(a => a.Name).Should().Equal("int");
        interfaces.Single(r => r.Definition == sized).TypeArgAnnotations.Should().BeEmpty();
    }
}
