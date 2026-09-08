extern alias SharpyRT;
using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Describes a synthesized interface that should be added to a class declaration
/// based on its dunder method definitions.
/// </summary>
/// <param name="InterfaceName">Short interface name, e.g., "ISized", "IEnumerator", "IEquatable"</param>
/// <param name="Namespace">Fully qualified namespace, e.g., "Sharpy", "System.Collections.Generic", "System"</param>
/// <param name="TypeArgs">Type arguments for generic interfaces; empty for non-generic</param>
/// <param name="TriggeringDunder">The dunder method that triggered this synthesis, e.g., "__len__", "__eq__"</param>
public record SynthesizedInterfaceInfo(
    string InterfaceName,
    string Namespace,
    SemanticType[] TypeArgs,
    string TriggeringDunder);

/// <summary>
/// The ONE classifier for dunder-driven interface synthesis (#1746). It reads the AST — the
/// dunders' declarations and their written annotations — and yields the rows that
/// <see cref="NameResolver"/> enqueues as flagged <see cref="InterfaceReference"/>s during
/// inheritance resolution. From there the synthesized interfaces are ordinary members of the
/// supertype closure: the type checker sees them, the SPY0607 gate sees them, and
/// <c>CodeGenInfoComputer</c> READS them back off the materialized closure rather than deciding
/// anything a second time.
/// </summary>
/// <remarks>
/// There is deliberately no resolved-type twin of this classifier. Two earlier ones existed —
/// one over <c>TypeSymbol.ProtocolMethods</c> for the cold path and one over restored
/// <c>Methods</c> for the warm path — and each disagreed with this one about at least one row
/// (the BCL rows, and the explicit/synthesized overlap), which is what emitted a base list with
/// <c>ISized</c> twice (CS0528). One classifier, one owner.
/// </remarks>
internal static class SynthesisAnalyzer
{
    /// <summary>
    /// The non-generic protocol dunders that synthesize a Sharpy.Core interface, in the order
    /// their base-list entries are emitted (the emitted order is snapshot-pinned, so this is an
    /// ordered roster and not a set). Each row's interface name comes from
    /// <see cref="ProtocolRegistry"/>, so the dunder → interface correspondence is written once.
    /// Generic Sharpy.Core interfaces (<c>IReverseEnumerable[T]</c>) need a type argument and are
    /// classified separately below.
    /// </summary>
    private static readonly string[] _coreProtocolDunders = { DunderNames.Len, DunderNames.Bool };

    /// <summary>
    /// Non-generic Sharpy.Core interfaces synthesized from a protocol dunder — derived from
    /// <see cref="_coreProtocolDunders"/>. Extend the dunder roster, not this set.
    /// </summary>
    public static readonly HashSet<string> SynthesizableSharpyCoreInterfaces =
        _coreProtocolDunders
            .Select(d => ProtocolRegistry.GetProtocol(d)?.SharpyCoreInterface)
            .Where(name => name != null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The CLR definition behind each synthesizable interface name (#1746).
    ///
    /// <para>The hoist resolves a definition symbol from the symbol table first, so an imported
    /// <c>IEquatable</c> and the synthesized one are the SAME <see cref="TypeSymbol"/>. Three of
    /// the seven rows (<c>IEquatable</c>, <c>IEnumerator</c>, <c>IEnumerable</c>) are not
    /// nameable in Sharpy source without an import, and the earlier hoist silently DROPPED them
    /// whenever the lookup missed — which is how <c>__eq__</c> at two instantiations escaped the
    /// closure gate. This roster is the import-free fallback: the definition symbol is obtained
    /// from <c>ClrTypeBridge.GetOrCreateClrDefinitionSymbol</c>.</para>
    ///
    /// <para>That bridge cache is per INSTANCE, so the two routes can hand back two distinct
    /// symbols for one CLR interface. The chosen identity for a definition is therefore its
    /// <see cref="TypeSymbol.ClrType"/> when non-null and the symbol reference otherwise — every
    /// grouping in <see cref="InterfaceInstantiationGate"/> keys on that, and materialization's
    /// own dedupe (by <c>Definition.Name</c>) collapses the pair regardless of which symbol won.
    /// </para>
    /// </summary>
    internal static Type? ClrDefinitionFor(string interfaceName) => interfaceName switch
    {
        "ISized" => typeof(SharpyRT::Sharpy.ISized),
        "IBoolConvertible" => typeof(SharpyRT::Sharpy.IBoolConvertible),
        "IReverseEnumerable" => typeof(SharpyRT::Sharpy.IReverseEnumerable<>),
        "IEnumerator" => typeof(System.Collections.Generic.IEnumerator<>),
        "IEnumerable" => typeof(System.Collections.Generic.IEnumerable<>),
        "IEquatable" => typeof(IEquatable<>),
        _ => null
    };

    /// <summary>
    /// The ONE rule for a synthesized interface's definition symbol (#1746): the symbol already
    /// in scope when it IS the CLR interface (an imported <c>IEquatable</c>, a Core <c>ISized</c>
    /// reached through the symbol table — so the explicit and the synthesized reference are the
    /// SAME symbol and materialization collapses them to one base-list entry), else the CLR
    /// definition through the bridge. The in-scope symbol is taken only when its
    /// <see cref="TypeSymbol.ClrType"/> equals the roster's: <c>IEnumerable</c> in scope is the
    /// non-generic <c>System.Collections.IEnumerable</c> the prelude registers for iteration, and
    /// taking it emitted <c>System.Collections.IEnumerable&lt;int&gt;</c> (CS0308). Used by the
    /// cold hoist (<c>NameResolver</c>) and the warm re-resolution (<c>InheritanceResolver</c>);
    /// returns null only if the roster gains a name with no CLR definition behind it.
    /// </summary>
    internal static TypeSymbol? ResolveInterfaceDefinition(
        string interfaceName, Func<string, TypeSymbol?> lookup, Discovery.ClrTypeBridge bridge)
    {
        var clrDefinition = ClrDefinitionFor(interfaceName);
        if (clrDefinition == null)
            return null;

        if (lookup(interfaceName) is { TypeKind: TypeKind.Interface } inScope
            && inScope.ClrType == clrDefinition)
        {
            return inScope;
        }

        return bridge.GetOrCreateClrDefinitionSymbol(clrDefinition);
    }

    /// <summary>
    /// AST-level classifier: examines FunctionDef nodes in a class/struct body to determine
    /// which interfaces should be synthesized, BEFORE type checking runs. Returns tuples of
    /// (InterfaceName, Namespace, TypeArgAnnotations, TriggeringDunder).
    /// </summary>
    internal static List<(string InterfaceName, string Namespace, ImmutableArray<TypeAnnotation> TypeArgAnnotations, string TriggeringDunder, int Line, int Column)>
        ClassifyDundersFromAst(IReadOnlyList<Statement> body)
    {
        var result = new List<(string, string, ImmutableArray<TypeAnnotation>, string, int, int)>();

        var dunders = new Dictionary<string, FunctionDef>();
        foreach (var stmt in body)
        {
            if (stmt is FunctionDef funcDef && DunderDetector.IsDunderMethod(funcDef.Name)
                && !dunders.ContainsKey(funcDef.Name))
            {
                dunders[funcDef.Name] = funcDef;
            }
        }

        // __len__ → ISized, __bool__ → IBoolConvertible, in roster order (base-list order is
        // snapshot-pinned, so the roster is ordered).
        foreach (var dunderName in _coreProtocolDunders)
        {
            if (!dunders.TryGetValue(dunderName, out var protocolFunc))
                continue;
            var interfaceName = ProtocolRegistry.GetProtocol(dunderName)?.SharpyCoreInterface;
            if (interfaceName == null)
                continue;
            result.Add((interfaceName, "Sharpy", ImmutableArray<TypeAnnotation>.Empty,
                dunderName, protocolFunc.LineStart, protocolFunc.ColumnStart));
        }

        // __reversed__ → IReverseEnumerable<T>
        if (dunders.TryGetValue(DunderNames.Reversed, out var reversedFunc))
        {
            var typeArg = reversedFunc.ReturnType ?? new TypeAnnotation { Name = "object" };
            result.Add(("IReverseEnumerable", "Sharpy",
                ImmutableArray.Create(typeArg), DunderNames.Reversed, reversedFunc.LineStart, reversedFunc.ColumnStart));
        }

        // __next__ → IEnumerator<T>; __next__ + __iter__ → IEnumerable<T>
        if (dunders.TryGetValue(DunderNames.Next, out var nextFunc))
        {
            var typeArg = nextFunc.ReturnType ?? new TypeAnnotation { Name = "object" };
            result.Add(("IEnumerator", "System.Collections.Generic",
                ImmutableArray.Create(typeArg), DunderNames.Next, nextFunc.LineStart, nextFunc.ColumnStart));

            if (dunders.TryGetValue(DunderNames.Iter, out var iterForNext))
            {
                result.Add(("IEnumerable", "System.Collections.Generic",
                    ImmutableArray.Create(typeArg), DunderNames.Iter, iterForNext.LineStart, iterForNext.ColumnStart));
            }
        }

        // __iter__ without __next__, if generator → IEnumerable<T>
        if (!dunders.ContainsKey(DunderNames.Next)
            && dunders.TryGetValue(DunderNames.Iter, out var iterFunc))
        {
            bool isGenerator = StatementWalker.Any(iterFunc.Body, stmt => stmt is YieldStatement);
            if (isGenerator)
            {
                var typeArg = iterFunc.ReturnType ?? new TypeAnnotation { Name = "object" };
                result.Add(("IEnumerable", "System.Collections.Generic",
                    ImmutableArray.Create(typeArg), DunderNames.Iter, iterFunc.LineStart, iterFunc.ColumnStart));
            }
        }

        // __eq__ → IEquatable<T>
        if (dunders.TryGetValue(DunderNames.Eq, out var eqFunc))
        {
            var otherParam = eqFunc.Parameters
                .FirstOrDefault(p => p.Name != PythonNames.Self);
            if (otherParam?.Type != null && otherParam.Type.Name != "object")
            {
                result.Add(("IEquatable", "System",
                    ImmutableArray.Create(otherParam.Type), DunderNames.Eq, eqFunc.LineStart, eqFunc.ColumnStart));
            }
        }

        return result;
    }
}
