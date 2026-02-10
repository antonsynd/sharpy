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
/// Single source of truth for computing which interfaces a type should synthesize
/// based on its dunder methods. Used by both codegen (RoslynEmitter) and validation
/// (InterfaceConflictValidator) to avoid dual-source-of-truth issues.
/// Operates on TypeSymbol data (not AST), which has resolved types from the TypeChecker.
/// </summary>
internal static class SynthesisAnalyzer
{
    /// <summary>
    /// Set of Sharpy.Core interfaces that are ready for implicit synthesis.
    /// When a dunder method triggers an interface listed here (via ProtocolRegistry),
    /// the interface is automatically added to the class's base type list.
    /// Extend this set as new interfaces are added to Sharpy.Core.
    /// </summary>
    public static readonly HashSet<string> SynthesizableSharpyCoreInterfaces = new()
    {
        "ISized",           // __len__ → int Count { get; }
        "IBoolConvertible", // __bool__ → bool __Bool__()
    };

    /// <summary>
    /// Computes the list of interfaces that should be synthesized for the given type symbol.
    /// This is the authoritative computation — both codegen and validation call this.
    /// </summary>
    public static List<SynthesizedInterfaceInfo> ComputeSynthesizedInterfaces(TypeSymbol typeSymbol)
    {
        var result = new List<SynthesizedInterfaceInfo>();

        // Phase 1: Non-generic Sharpy.Core interfaces from ProtocolMethods
        foreach (var kvp in typeSymbol.ProtocolMethods)
        {
            var dunderName = kvp.Key;
            var protocol = ProtocolRegistry.GetProtocol(dunderName);
            if (protocol?.SharpyCoreInterface == null)
                continue;

            if (SynthesizableSharpyCoreInterfaces.Contains(protocol.SharpyCoreInterface))
            {
                result.Add(new SynthesizedInterfaceInfo(
                    protocol.SharpyCoreInterface,
                    "Sharpy",
                    Array.Empty<SemanticType>(),
                    dunderName));
            }
        }

        // Phase 2: IEnumerator<T> from __next__, IEnumerable<T> from __iter__+__next__
        if (typeSymbol.ProtocolMethods.TryGetValue(DunderNames.Next, out var nextOverloads))
        {
            var nextFunc = nextOverloads.FirstOrDefault();
            if (nextFunc != null)
            {
                var elementType = nextFunc.ReturnType is not UnknownType
                    ? nextFunc.ReturnType
                    : new UserDefinedType { Name = "object" };

                result.Add(new SynthesizedInterfaceInfo(
                    "IEnumerator",
                    "System.Collections.Generic",
                    new[] { elementType },
                    DunderNames.Next));

                if (typeSymbol.ProtocolMethods.ContainsKey(DunderNames.Iter))
                {
                    result.Add(new SynthesizedInterfaceInfo(
                        "IEnumerable",
                        "System.Collections.Generic",
                        new[] { elementType },
                        DunderNames.Iter));
                }
            }
        }

        // Phase 3: IEquatable<T> from __eq__(self, other: T) where T is not object
        if (typeSymbol.OperatorMethods.TryGetValue(DunderNames.Eq, out var eqOverloads))
        {
            foreach (var overload in eqOverloads)
            {
                var otherParam = overload.Parameters
                    .FirstOrDefault(p => p.Name != "self");

                if (otherParam == null)
                    continue;

                // Skip if parameter type is object — that generates override Equals(object), not IEquatable
                if (otherParam.Type is UserDefinedType { Name: "object" })
                    continue;

                // Skip Unknown types (unresolved)
                if (otherParam.Type is UnknownType)
                    continue;

                result.Add(new SynthesizedInterfaceInfo(
                    "IEquatable",
                    "System",
                    new[] { otherParam.Type },
                    DunderNames.Eq));
            }
        }

        return result;
    }
}
