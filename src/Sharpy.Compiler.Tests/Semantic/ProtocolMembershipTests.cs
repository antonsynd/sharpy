using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1860, R-AN, Design Decision 5: <see cref="ProtocolMembership.Has"/>'s registry table only ADDS —
/// a POSITIVE table answer is authoritative, but a NEGATIVE one must fall through to the CLR shape
/// (<see cref="ProtocolMembership.HasClrProtocol"/>) rather than denying what it proves.
///
/// <para>This enumerates every registered GENERIC builtin (<see cref="BuiltinRegistry.GetAllTypes"/>
/// where <c>IsGeneric</c>) × <c>{__contains__, __len__, __getitem__, __setitem__, __reversed__,
/// __iter__}</c> and asserts <c>Has(G[int...]) == table ∪ HasClrProtocol(closed CLR type)</c> — the
/// fall-through must never make <c>Has</c> answer something NEITHER side proves, and it must never
/// still deny something the CLR side DOES prove. The (table ∌, CLR ∋) delta — the rows the table used
/// to wrongly deny — is pinned to an explicit two-row list: any OTHER delta is an unexamined
/// widening, not this decision's.</para>
/// </summary>
public class ProtocolMembershipTests
{
    private static readonly string[] Dunders =
    {
        DunderNames.Contains, DunderNames.Len, DunderNames.GetItem,
        DunderNames.SetItem, DunderNames.Reversed, DunderNames.Iter,
    };

    /// <summary>
    /// The rows the registry table DENIES that the CLR shape PROVES, pinned @ this commit (#1860):
    /// IEnumerable/Iterator both answer <c>__contains__</c> through LINQ's <c>Enumerable.Contains</c>
    /// (the arm in <see cref="ProtocolMembership.HasClrProtocol"/>), which the builtin registry's own
    /// protocol table has never recorded for either.
    /// </summary>
    private static readonly HashSet<(string TypeName, string Dunder)> PinnedDelta = new()
    {
        (BuiltinNames.IEnumerable, DunderNames.Contains),
        (BuiltinNames.Iterator, DunderNames.Contains),
    };

    [Fact]
    public void RegistryTable_NeverDeniesWhatTheClrShapeProves()
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var membership = new ProtocolMembership(symbolTable, builtins);

        var delta = new HashSet<(string TypeName, string Dunder)>();
        var genericTypesChecked = 0;

        foreach (var (name, typeSymbol) in builtins.GetAllTypes())
        {
            if (!typeSymbol.IsGeneric)
                continue;

            // A backtick-suffixed name (`EnumerateIterator`1`, `MapIterator`2`, …) is a CLR
            // reflection artifact leaked into `_types` by discovery caching an internal Sharpy.Core
            // helper class (enumerate/filter/map/zip's iterator machinery) — never a name the parser
            // can spell as a type annotation, and not the "registered generic builtin" roster Design
            // Decision 5 measured its delta against. Every INTENTIONAL `RegisterType` call in
            // `BuiltinRegistry.LoadBuiltins` names a clean Sharpy spelling ("list", "IEnumerable",
            // BuiltinNames.Iterator, …) with no backtick.
            if (name.Contains('`'))
                continue;

            // A generic builtin's registered ClrType is always the OPEN generic definition
            // (RegisterType stores exactly what it was given) — close it with `int` in every slot to
            // ask HasClrProtocol the same question the fall-through arms ask (Has's own "construct
            // the closed type" arms do the identical thing for the same reason: HasClrProtocol's
            // IsAssignableFrom checks do not answer for an open definition).
            if (typeSymbol.ClrType is not { IsGenericTypeDefinition: true } openClr)
                continue;

            var arity = openClr.GetGenericArguments().Length;
            Type closedClr;
            try
            {
                closedClr = openClr.MakeGenericType(Enumerable.Repeat(typeof(int), arity).ToArray());
            }
            catch (ArgumentException)
            {
                continue; // a generic constraint `int` violates — not this test's question
            }

            genericTypesChecked++;
            var genericType = new GenericType
            {
                Name = name,
                TypeArguments = Enumerable.Repeat(SemanticType.Int, arity).ToList<SemanticType>(),
            };

            foreach (var dunder in Dunders)
            {
                var table = typeSymbol.ProtocolMethods.ContainsKey(dunder);
                var clr = membership.HasClrProtocol(closedClr, dunder);
                var has = membership.Has(genericType, dunder);

                has.Should().Be(table || clr,
                    $"{name}[int] x {dunder}: Has()={has} must equal table({table}) OR CLR({clr})");

                if (!table && clr)
                    delta.Add((name, dunder));
            }
        }

        genericTypesChecked.Should().BeGreaterThan(0,
            "the walk must find generic builtins to test — an empty walk would pass vacuously");

        delta.Should().BeEquivalentTo(PinnedDelta,
            "the (table doesn't, CLR does) delta is EXACTLY the pinned rows — any other delta is an "
            + "unexamined widening the fall-through introduced");
    }
}
