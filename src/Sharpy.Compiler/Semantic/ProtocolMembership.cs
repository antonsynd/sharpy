using System;
using System.Collections.Generic;
using System.Linq;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The full names of the Sharpy protocol interfaces a CLR type can carry. One roster, so a probe
/// cannot spell an interface differently from the synthesis that produced it (#1808).
/// </summary>
internal static class SharpyProtocolInterfaces
{
    public const string Sized = "Sharpy.ISized";
    public const string BoolConvertible = "Sharpy.IBoolConvertible";
    public const string ReverseEnumerable = "Sharpy.IReverseEnumerable`1";
}

/// <summary>
/// THE protocol-membership authority (#1808, Design Decision 7). "Does this receiver answer this
/// dunder?" is one question, and before this type it had five hand-rolled answers — the validator's
/// private copy (whose own comment read "This duplicates logic"), <c>ClassifyTruthiness</c>,
/// <c>InferIterableElementType</c>, <c>InferReversedElementType</c> and
/// <c>ResolveMembershipElementType</c> — which disagreed by receiver TYPING rather than by receiver:
/// a class declaring <c>__len__</c> was sized everywhere, a class INHERITING it was sized in one
/// place, and a receiver typed as <c>ISized</c> itself was sized in none (an interface's
/// <c>GetInterfaces()</c> never contains itself).
///
/// <para>Every arm answers for the receiver AS WRITTEN: the loose <c>T | None</c> payload, the
/// builtin rows, the symbol's own <c>ProtocolMethods</c>/<c>Methods</c>, the BASE CHAIN, the
/// explicit and synthesized interfaces on the binding, and the CLR shape. The strict <c>T?</c> is
/// deliberately not unwrapped — it is refused by its callers with an actionable message.</para>
/// </summary>
internal sealed class ProtocolMembership
{
    private readonly IGlobalSymbolTable _symbolTable;
    private readonly BuiltinRegistry _builtins;

    public ProtocolMembership(IGlobalSymbolTable symbolTable, BuiltinRegistry builtins)
    {
        _symbolTable = symbolTable;
        _builtins = builtins;
    }

    /// <summary>
    /// Whether a CLR type IS a Sharpy protocol interface or implements one. Both halves matter: an
    /// interface-typed receiver is its own implementor, which <c>GetInterfaces()</c> alone never
    /// reports (#1808). Generic protocol interfaces are matched on the open definition's full name,
    /// so <c>IReverseEnumerable&lt;int&gt;</c> answers for <c>Sharpy.IReverseEnumerable`1</c>.
    /// </summary>
    public static bool HasClrProtocolInterface(Type clrType, string interfaceFullName)
    {
        bool Matches(Type t)
            => t.FullName == interfaceFullName
               || (t.IsGenericType && t.GetGenericTypeDefinition().FullName == interfaceFullName);

        return Matches(clrType) || clrType.GetInterfaces().Any(Matches);
    }

    /// <summary>
    /// Walks a type symbol's own members and then its base chain
    /// (<see cref="TypeSymbol.BaseType"/>) for a dunder. Inherited dunders are the same fact as
    /// declared ones: <c>class Sack(Bag)</c> where <c>Bag</c> declares <c>__len__</c> is sized.
    /// </summary>
    public static bool HasDunderInChain(TypeSymbol symbol, string dunderName)
        => FindDunderInChain(symbol, dunderName) != null || DeclaresDunder(symbol, dunderName);

    /// <summary>
    /// The <see cref="FunctionSymbol"/> a dunder resolves to on a symbol or anywhere up its base
    /// chain, or <c>null</c>. The element-TYPE consumers (<c>reversed</c>, membership) need the
    /// method itself, not just the membership answer, and walking the chain here is what makes an
    /// inherited <c>__reversed__</c> resolve like a declared one.
    /// </summary>
    public static FunctionSymbol? FindDunderInChain(TypeSymbol? symbol, string dunderName)
    {
        for (var current = symbol; current != null; current = current.BaseType)
        {
            var method = current.Methods.FirstOrDefault(m => m.Name == dunderName);
            if (method != null)
                return method;
        }

        return null;
    }

    /// <summary>
    /// Whether a dunder is declared anywhere up the base chain, counting the
    /// <see cref="TypeSymbol.ProtocolMethods"/> table (which records a protocol a type answers
    /// without a <see cref="FunctionSymbol"/> behind it).
    /// </summary>
    private static bool DeclaresDunder(TypeSymbol? symbol, string dunderName)
    {
        for (var current = symbol; current != null; current = current.BaseType)
        {
            if (current.ProtocolMethods.ContainsKey(dunderName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether <paramref name="type"/> answers <paramref name="dunderName"/> — the ONE protocol
    /// question in the compiler. Every arm below is a spelling of the same fact: a builtin row, a
    /// symbol's own members, an INHERITED member (the base chain), an explicit or synthesized
    /// interface, or a CLR shape.
    /// </summary>
    public bool Has(SemanticType type, string dunderName)
    {
        // T | None (C# nullable, .NET interop) exposes the protocols of its underlying type —
        // protocol ops are permitted and a null receiver fails at runtime (Python-parity).
        // OptionalType (T?) is deliberately NOT unwrapped: it is strict and must be narrowed or
        // unwrapped first (reported with an actionable message by the Validate* callers).
        if (type is NullableType nullableType)
            return Has(nullableType.UnderlyingType, dunderName);

        // Check Sharpy built-in types first
        if (TypeChecker.OperandView(type) == SemanticType.Str)
        {
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.Contains or DunderNames.GetItem;
        }

        // Check TupleType — tuples are iterable, sized, indexable, and contain-testable (#1771).
        if (type is TupleType)
        {
            return dunderName is DunderNames.Len or DunderNames.Iter or DunderNames.GetItem or DunderNames.Contains;
        }

        // Check generic container types — use TypeSymbol metadata (populated by discovery)
        if (type is GenericType generic)
        {
            // Arrays support __len__, __iter__, __getitem__, __setitem__, __contains__
            if (generic.Name == BuiltinNames.Array)
            {
                return dunderName is DunderNames.Len or DunderNames.Iter
                    or DunderNames.GetItem or DunderNames.SetItem or DunderNames.Contains;
            }

            // For defaultdict, check dict protocols since it inherits from Dict
            var lookupName = string.Equals(generic.Name, BuiltinNames.DefaultDict, StringComparison.OrdinalIgnoreCase)
                ? BuiltinNames.Dict : generic.Name;
            var typeSymbol = _builtins.GetType(lookupName);
            if (typeSymbol != null)
                return typeSymbol.ProtocolMethods.ContainsKey(dunderName);

            // Fallback: check the resolved generic definition, then the SymbolTable for
            // discovery-loaded generic types (e.g., Counter, DefaultDict). The GenericDefinition is
            // preferred because module-qualified types (collections.Counter) are not registered under
            // their bare name in the top-level SymbolTable, so the by-name lookups below miss them.
            // Try the original name first, then PascalCase, then case-insensitive match
            // for Python-style names that don't split cleanly (e.g., "defaultdict" → "DefaultDict")
            var symTableType = generic.GenericDefinition
                ?? _symbolTable.Lookup(generic.Name) as TypeSymbol
                ?? _symbolTable.Lookup(NameMangler.ToPascalCase(generic.Name)) as TypeSymbol
                ?? _symbolTable.LookupCaseInsensitive(generic.Name) as TypeSymbol;
            if (symTableType != null)
            {
                if (symTableType.ProtocolMethods.ContainsKey(dunderName))
                    return true;

                if (symTableType.Methods.Any(m => m.Name == dunderName))
                    return true;

                if (symTableType.ClrType != null && HasClrProtocol(symTableType.ClrType, dunderName))
                    return true;

                // Open generic definitions (typeof(HashSet<>)) fail IsAssignableFrom checks in
                // HasClrProtocol. Construct the closed type and re-check (#1517 honest borders).
                if (symTableType.ClrType is { IsGenericTypeDefinition: true } openDef
                    && openDef.GetGenericArguments().Length == generic.TypeArguments.Count)
                {
                    try
                    {
                        var closedClrArgs = generic.TypeArguments
                            .Select(ta => ta.ClrType ?? typeof(object))
                            .ToArray();
                        var closedClr = openDef.MakeGenericType(closedClrArgs);
                        if (HasClrProtocol(closedClr, dunderName))
                            return true;
                    }
                    catch (ArgumentException)
                    {
                        // Constraint violation — the closed form cannot be constructed here.
                    }
                }
            }

            // The GenericDefinition path is null when the return type came through the overload
            // index (ConvertTypeSignature) for a non-module CLR type. Resolve the CLR generic
            // definition from ClrOriginTypeName and check its closed form (#1517 honest borders).
            // When both GenericDefinition and ClrOriginTypeName are missing (the return type lost
            // its CLR identity through the overload-index → builtin-substitution chain), fall back
            // to the display-name scan. Both lookups go through ClrTypeHelper's null-cached
            // resolvers — a negative protocol probe must not rescan loaded assemblies each time.
            if (generic.ClrOriginTypeName is { Length: > 0 } || symTableType == null)
            {
                var arity = generic.TypeArguments.Count;
                var resolvedDef = generic.ClrOriginTypeName is { Length: > 0 } originName
                    ? Discovery.ClrTypeHelper.ResolveClrTypeByStoredName(originName)
                    : Discovery.ClrTypeHelper.ResolveGenericDefinitionByDisplayName(generic.Name, arity);

                if (resolvedDef is { IsGenericTypeDefinition: true }
                    && resolvedDef.GetGenericArguments().Length == arity)
                {
                    try
                    {
                        var closedClrArgs = generic.TypeArguments
                            .Select(ta => ta.ClrType ?? typeof(object))
                            .ToArray();
                        var closedClr = resolvedDef.MakeGenericType(closedClrArgs);
                        if (HasClrProtocol(closedClr, dunderName))
                            return true;
                    }
                    catch (ArgumentException)
                    {
                        // A constraint the placeholder arguments violate — the closed form cannot
                        // be constructed, so the protocol stays unproven here.
                    }
                }
            }
        }

        // Enum types support __iter__ (via Enum.GetValues<T>())
        if (type is UserDefinedType { Symbol.TypeKind: TypeKind.Enum } && dunderName == DunderNames.Iter)
            return true;

        // Check Sharpy user-defined types — walk the base chain for inherited dunders (#1808)
        if (type is UserDefinedType udt)
        {
            // If Symbol is null (e.g., return type from CLR module discovery),
            // resolve it from the SymbolTable by name
            var symbol = udt.Symbol ?? _symbolTable.Lookup(udt.Name) as TypeSymbol;
            if (symbol != null)
            {
                if (HasDunderInChain(symbol, dunderName))
                    return true;

                // Check CLR type if available
                if (symbol.ClrType != null && HasClrProtocol(symbol.ClrType, dunderName))
                    return true;
            }
        }

        // Check builtin types with CLR backing
        if (type is BuiltinType builtin && builtin.ClrType != null)
        {
            if (HasClrProtocol(builtin.ClrType, dunderName))
                return true;
        }

        // For other types (including int, bool, etc.), default to false for most protocols
        // except __str__ and __hash__ which all objects have
        if (dunderName is DunderNames.Str or DunderNames.Hash)
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a CLR type supports a protocol by examining its interfaces.
    /// </summary>
    public bool HasClrProtocol(System.Type clrType, string dunderName)
    {
        // Check for IEnumerable<T> or IEnumerable -> __iter__
        if (dunderName == DunderNames.Iter)
        {
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(clrType))
                return true;

            // Check for Sharpy.Iterator<T> base class
            if (ClrTypeHelper.GetIteratorElementType(clrType) != null)
                return true;
        }

        // ICollection -> __len__, __contains__
        if (dunderName is DunderNames.Len or DunderNames.Contains)
        {
            if (typeof(System.Collections.ICollection).IsAssignableFrom(clrType))
                return true;
        }

        // Generic ICollection<T> / IReadOnlyCollection<T> -> __len__
        // (mirrors what a `Count` property provides; ISized exposes the same surface).
        // Needed for CLR types like collections.Deque that implement only the generic
        // read-only collection interface, which is neither non-generic ICollection nor ISized.
        if (dunderName == DunderNames.Len)
        {
            if (clrType.GetInterfaces().Any(i => i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.ICollection<>)
                    || i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IReadOnlyCollection<>))))
                return true;
        }

        // IList -> __getitem__, __setitem__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem)
        {
            if (typeof(System.Collections.IList).IsAssignableFrom(clrType))
                return true;
        }

        // Plain parameterized indexer (this[...]) -> __getitem__ (getter) / __setitem__ (setter).
        // Mirrors OverloadIndexBuilder.DiscoverTypeProtocols so CLR types that expose a C#
        // indexer without implementing IList/IDictionary (e.g. collections.Counter, OrderedDict,
        // ChainMap) are recognized as subscriptable. The setter check keeps read-only indexers
        // (e.g. frozendict / IReadOnlyDictionary) from being treated as supporting __setitem__.
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem)
        {
            var indexers = clrType.GetProperties()
                .Where(p => p.GetIndexParameters().Length > 0);
            if (dunderName == DunderNames.GetItem && indexers.Any(p => p.GetGetMethod() != null))
                return true;
            if (dunderName == DunderNames.SetItem && indexers.Any(p => p.GetSetMethod() != null))
                return true;
        }

        // IDictionary -> __getitem__, __setitem__, __contains__, __len__
        if (dunderName is DunderNames.GetItem or DunderNames.SetItem or DunderNames.Contains or DunderNames.Len)
        {
            if (typeof(System.Collections.IDictionary).IsAssignableFrom(clrType))
                return true;

            // Also check generic IDictionary<,> (Sharpy's Dict<K,V> implements this but not the non-generic)
            if (clrType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IDictionary<,>)))
                return true;
        }

        // IReadOnlyDictionary<,> -> __getitem__, __contains__, __len__ (read-only mapping; no __setitem__)
        if (dunderName is DunderNames.GetItem or DunderNames.Contains or DunderNames.Len)
        {
            if (clrType.GetInterfaces().Any(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IReadOnlyDictionary<,>)))
                return true;
        }

        // A generic IEnumerable<T> answers __contains__ through LINQ's Enumerable.Contains, which is
        // exactly what the emitter's `right.Contains(left)` binds — acceptance and lowering are one
        // decision (#1808). Without this arm a receiver TYPED as `IEnumerable[int]` iterated fine
        // and then refused `in` with "missing __contains__", while the emitted C# would have
        // compiled.
        //
        // NOT REACHED TODAY for that receiver (#1860): the GenericType arm above answers from the
        // builtin registry's protocol table with an early `return`, and the table denies
        // `__contains__` for `IEnumerable`, so the question never gets here. Measured — `2 in xs`
        // on an `IEnumerable[int]` receiver is SPY0320 with this arm present. The arm is kept
        // because it is the correct rule for every OTHER path that reaches the CLR arms; #1860
        // decides whether the registry may deny what the CLR shape proves.
        if (dunderName == DunderNames.Contains)
        {
            bool IsGenericEnumerable(Type t)
                => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>);

            if (IsGenericEnumerable(clrType) || clrType.GetInterfaces().Any(IsGenericEnumerable))
                return true;
        }

        // A public instance Contains(T) method proves __contains__ — shared predicate with
        // OverloadIndexBuilder.DiscoverTypeProtocols so the two cannot diverge (#1778).
        if (dunderName == DunderNames.Contains)
        {
            if (Discovery.ClrTypeHelper.HasPublicContainsMethod(clrType))
                return true;
        }

        // #1808: Sharpy protocol interfaces — check both the type's own identity AND its
        // implemented interfaces. The original GetInterfaces() missed interface-typed receivers
        // because an interface's GetInterfaces() never contains itself. By checking FullName
        // on the type itself too, ISized-typed receivers resolve correctly.
        if (dunderName == DunderNames.Len)
        {
            if (HasClrProtocolInterface(clrType, SharpyProtocolInterfaces.Sized))
                return true;
        }

        if (dunderName == DunderNames.Bool)
        {
            if (HasClrProtocolInterface(clrType, SharpyProtocolInterfaces.BoolConvertible))
                return true;
        }

        if (dunderName == DunderNames.Reversed)
        {
            if (HasClrProtocolInterface(clrType, SharpyProtocolInterfaces.ReverseEnumerable))
                return true;
        }

        return false;
    }
}
