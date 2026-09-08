using System.Runtime.CompilerServices;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The R-H gate: a type may reach a generic interface at ONE instantiation. C# refuses two
/// (CS0738), and Sharpy's materialization silently collapses the second by definition name, so
/// without this gate the program either ICEs behind SPY0908 or runs with one instantiation
/// quietly discarded. SPY0607 refuses it by name, before materialization (#1717, R-H ruling).
/// </summary>
/// <remarks>
/// <para><b>Placement.</b> The gate runs between inheritance resolution and
/// <c>SemanticBinding.MaterializeInheritance</c> (see
/// <c>FileCompilationPipeline.ResolveImportedInheritanceAndMaterialize</c>). Anywhere later is
/// inert for the commonest shape — two instantiations in ONE declaration's base list — because
/// the queue-vs-symbol comparison in <see cref="DualWriteAssertions"/> and the C# base list are
/// both downstream of the collapse.</para>
/// <para><b>The walk.</b> <see cref="GenericInstantiationWalker.EnumerateImplementedInterfaces"/>
/// is the one supertype-closure reader: it reads the BINDING (the pre-dedupe queue, so both
/// entries of a two-instantiation base list are visible), substitutes each level's arguments
/// through interface parents and the base chain, converts written annotations with their
/// modifiers intact (<c>IA[T?]</c> is not <c>IA[T]</c>), and reports the route it took. The gate
/// does not re-derive any of that.</para>
/// </remarks>
internal static class InterfaceInstantiationGate
{
    public static void CheckAll(
        SymbolTable symbolTable,
        SemanticBinding semanticBinding,
        SemanticInfo semanticInfo,
        ICompilerLogger logger,
        DiagnosticBag diagnostics)
    {
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);

        void CheckScope(IEnumerable<Symbol> symbols)
        {
            foreach (var symbol in symbols)
            {
                // Interfaces are checked too: `interface ID(IB, IC)` is the declaration that
                // INTRODUCES the conflict under R-H, so the diagnostic belongs there and not only
                // at every class that names ID. Reflected CLR types are not walked at all — their
                // supertype graph is not declared in Sharpy source, so there is no declaration to
                // refuse (the same reason the per-path exemption in Check exists, applied to the
                // host before paying for the walk).
                if (symbol is TypeSymbol typeSymbol &&
                    typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface &&
                    typeSymbol.ClrType == null &&
                    visited.Add(typeSymbol))
                {
                    Check(typeSymbol, semanticBinding, typeResolver, diagnostics);
                }
            }
        }

        CheckScope(symbolTable.GlobalScope.GetAllSymbols());

        // A module's declarations are checked WITH its scope entered: an instantiation's written
        // argument (`IEquatable[Foo]` synthesized from `__eq__(self, other: Foo)`, `IA[Point]`)
        // names a type that resolves only from inside the module that declared it. Looked up
        // from the global scope every such reference was Unknown, the walker skipped it, and the
        // gate was inert for every user-typed instantiation while the builtin-typed cells passed.
        var canEnter = symbolTable.CurrentScope == symbolTable.GlobalScope;
        foreach (var moduleName in symbolTable.GetModuleScopeNames())
        {
            var scope = symbolTable.GetModuleScope(moduleName);
            if (scope == null)
                continue;
            if (!canEnter)
            {
                CheckScope(scope.GetAllSymbols());
                continue;
            }
            symbolTable.EnterModuleScope(moduleName);
            try
            {
                CheckScope(scope.GetAllSymbols());
            }
            finally
            {
                symbolTable.ExitScope();
            }
        }
    }

    public static void Check(
        TypeSymbol typeSymbol,
        SemanticBinding semanticBinding,
        TypeResolver typeResolver,
        DiagnosticBag diagnostics)
    {
        // A generic host is walked at its OWN type parameters, so `class G[T](IA[T], IA[int])`
        // compares `IA[T]` against `IA[int]` rather than skipping the open arm: `T` may be `int`,
        // and C# refuses the declaration whether or not it is.
        var ownArguments = typeSymbol.TypeParameters
            .Select(tp => (SemanticType)new TypeParameterType { Name = tp.Name })
            .ToList();

        var reached = GenericInstantiationWalker
            .EnumerateImplementedInterfaces(typeSymbol, ownArguments, semanticBinding, typeResolver)
            .ToList();

        if (reached.Count < 2)
            return;

        var groups = new Dictionary<object, List<GenericInstantiationWalker.InstantiatedSupertype>>(
            DefinitionIdentityComparer.Instance);

        foreach (var entry in reached)
        {
            var key = DefinitionIdentity(entry.Definition);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<GenericInstantiationWalker.InstantiatedSupertype>();
                groups[key] = list;
            }
            list.Add(entry);
        }

        foreach (var entries in groups.Values)
        {
            if (entries.Count < 2)
                continue;

            // A non-generic interface reached twice is not a conflict — it is the same contract.
            if (entries.All(e => e.TypeArguments.Count == 0))
                continue;

            // The walker's visited set already collapses identical (definition, arguments) pairs,
            // so every entry left in a group is a DISTINCT instantiation. Two of them is a
            // conflict; the same instantiation via two paths never gets here.
            var first = entries[0];
            var second = entries[1];

            // Exempt when every contributing path is CLR-internal: a reflected type's own
            // supertype graph is not something Sharpy source can change.
            if (entries.All(e => e.FromClrDeclaration))
                continue;

            var message =
                $"Type '{typeSymbol.Name}' implements '{FormatInstantiation(first)}' ({first.Path}) " +
                $"and '{FormatInstantiation(second)}' ({second.Path}) " +
                "— one generic interface cannot have conflicting type arguments";

            diagnostics.AddError(
                message,
                line: typeSymbol.DeclarationLine,
                column: typeSymbol.DeclarationColumn,
                filePath: typeSymbol.DeclaringFilePath,
                code: DiagnosticCodes.SemanticOverflow.ConflictingInterfaceInstantiation);
        }
    }

    /// <summary>
    /// ONE identity per interface DEFINITION. Two routes can produce two distinct
    /// <see cref="TypeSymbol"/>s for the same CLR interface — <c>from system import IEquatable</c>
    /// builds one through discovery, the dunder hoist builds one through
    /// <c>ClrTypeBridge</c>, whose cache is per instance — so a reference-keyed group would split
    /// the pair and miss the conflict. The key is the CLR definition <see cref="Type"/> when the
    /// definition is CLR-backed, and the symbol reference otherwise (#1746).
    /// </summary>
    private static object DefinitionIdentity(TypeSymbol definition)
    {
        if (definition.ClrType is not { } clr)
            return definition;
        return clr.IsGenericType && !clr.IsGenericTypeDefinition
            ? clr.GetGenericTypeDefinition()
            : clr;
    }

    private sealed class DefinitionIdentityComparer : IEqualityComparer<object>
    {
        internal static readonly DefinitionIdentityComparer Instance = new();

        public new bool Equals(object? x, object? y)
            => x is Type xt && y is Type yt ? xt == yt : ReferenceEquals(x, y);

        public int GetHashCode(object obj)
            => obj is Type t ? t.GetHashCode() : RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Renders an instantiation the way Sharpy source spells it — <c>IA[int32?]</c>, not
    /// <c>IA&lt;Optional&lt;int&gt;&gt;</c> — so both arms of the message read in one notation
    /// (RULED 2026-09-07).
    /// </summary>
    private static string FormatInstantiation(GenericInstantiationWalker.InstantiatedSupertype entry)
    {
        if (entry.TypeArguments.Count == 0)
            return entry.Definition.Name;
        var argDisplay = string.Join(", ", entry.TypeArguments.Select(a => a.GetDisplayName()));
        return $"{entry.Definition.Name}[{argDisplay}]";
    }
}
