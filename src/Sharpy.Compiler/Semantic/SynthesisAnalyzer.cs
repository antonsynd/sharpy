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
/// Single source of truth for computing which interfaces a type should synthesize
/// based on its dunder methods. Used by both codegen (RoslynEmitter) and validation
/// (InterfaceConflictValidator) to avoid dual-source-of-truth issues.
/// Operates on TypeSymbol data (not AST), which has resolved types from the TypeChecker.
/// </summary>
internal static class SynthesisAnalyzer
{
    /// <summary>
    /// Non-generic Sharpy.Core interfaces synthesized via Phase 1.
    /// Generic Sharpy.Core interfaces (e.g. IReverseEnumerable&lt;T&gt;) are handled
    /// in Phase 2a below because they require type argument inference.
    /// Extend this set when adding new non-generic Sharpy.Core interfaces.
    /// </summary>
    public static readonly HashSet<string> SynthesizableSharpyCoreInterfaces = new()
    {
        "ISized",           // __len__ → int Count { get; }
        "IBoolConvertible", // __bool__ → bool IsTrue { get; }
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

        // Phase 2a: IReverseEnumerable<T> from __reversed__
        if (typeSymbol.ProtocolMethods.TryGetValue(DunderNames.Reversed, out var reversedOverloads))
        {
            var reversedFunc = reversedOverloads.FirstOrDefault();
            if (reversedFunc != null)
            {
                var elementType = reversedFunc.ReturnType is not UnknownType
                    ? reversedFunc.ReturnType
                    : new UserDefinedType { Name = "object" };

                result.Add(new SynthesizedInterfaceInfo(
                    "IReverseEnumerable",
                    "Sharpy",
                    new[] { elementType },
                    DunderNames.Reversed));
            }
        }

        // Phase 2b: IEnumerator<T> from __next__, IEnumerable<T> from __iter__+__next__
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

        // Phase 2c: IEnumerable<T> from generator __iter__ (without __next__)
        if (!typeSymbol.ProtocolMethods.ContainsKey(DunderNames.Next)
            && typeSymbol.ProtocolMethods.TryGetValue(DunderNames.Iter, out var iterOverloads))
        {
            var iterFunc = iterOverloads.FirstOrDefault();
            if (iterFunc is { IsGenerator: true })
            {
                var elementType = iterFunc.ReturnType is not (UnknownType or VoidType)
                    ? iterFunc.ReturnType
                    : SemanticType.Object;
                result.Add(new SynthesizedInterfaceInfo(
                    "IEnumerable",
                    "System.Collections.Generic",
                    new[] { elementType },
                    DunderNames.Iter));
            }
        }

        // Phase 3: IEquatable<T> from __eq__(self, other: T) where T is not object
        if (typeSymbol.OperatorMethods.TryGetValue(DunderNames.Eq, out var eqOverloads))
        {
            foreach (var overload in eqOverloads)
            {
                var otherParam = overload.Parameters
                    .FirstOrDefault(p => p.Name != PythonNames.Self);

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

    /// <summary>
    /// Computes synthesized interfaces from a pre-built method dictionary.
    /// Used by the warm-restore path where ProtocolMethods is not populated (#1746).
    /// </summary>
    public static List<SynthesizedInterfaceInfo> ComputeSynthesizedInterfacesFromMethods(
        Dictionary<string, FunctionSymbol> dunders)
    {
        var result = new List<SynthesizedInterfaceInfo>();

        foreach (var (name, _) in dunders)
        {
            var protocol = ProtocolRegistry.GetProtocol(name);
            if (protocol?.SharpyCoreInterface != null
                && SynthesizableSharpyCoreInterfaces.Contains(protocol.SharpyCoreInterface))
            {
                result.Add(new SynthesizedInterfaceInfo(
                    protocol.SharpyCoreInterface, "Sharpy",
                    Array.Empty<SemanticType>(), name));
            }
        }

        if (dunders.TryGetValue(DunderNames.Reversed, out var reversedFunc))
        {
            var elementType = reversedFunc.ReturnType is not UnknownType
                ? reversedFunc.ReturnType
                : new UserDefinedType { Name = "object" };
            result.Add(new SynthesizedInterfaceInfo(
                "IReverseEnumerable", "Sharpy", new[] { elementType }, DunderNames.Reversed));
        }

        if (dunders.TryGetValue(DunderNames.Next, out var nextFunc))
        {
            var elementType = nextFunc.ReturnType is not UnknownType
                ? nextFunc.ReturnType
                : new UserDefinedType { Name = "object" };
            result.Add(new SynthesizedInterfaceInfo(
                "IEnumerator", "System.Collections.Generic", new[] { elementType }, DunderNames.Next));

            if (dunders.ContainsKey(DunderNames.Iter))
            {
                result.Add(new SynthesizedInterfaceInfo(
                    "IEnumerable", "System.Collections.Generic", new[] { elementType }, DunderNames.Iter));
            }
        }

        if (!dunders.ContainsKey(DunderNames.Next)
            && dunders.TryGetValue(DunderNames.Iter, out var iterFunc)
            && iterFunc.IsGenerator)
        {
            var elementType = iterFunc.ReturnType is not (UnknownType or VoidType)
                ? iterFunc.ReturnType
                : SemanticType.Object;
            result.Add(new SynthesizedInterfaceInfo(
                "IEnumerable", "System.Collections.Generic", new[] { elementType }, DunderNames.Iter));
        }

        if (dunders.TryGetValue(DunderNames.Eq, out var eqFunc))
        {
            var otherParam = eqFunc.Parameters
                .FirstOrDefault(p => p.Name != PythonNames.Self);
            if (otherParam?.Type is not (null or UserDefinedType { Name: "object" } or UnknownType))
            {
                result.Add(new SynthesizedInterfaceInfo(
                    "IEquatable", "System", new[] { otherParam.Type }, DunderNames.Eq));
            }
        }

        return result;
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

        // __len__ → ISized
        if (dunders.TryGetValue(DunderNames.Len, out var lenFunc))
        {
            result.Add(("ISized", "Sharpy", ImmutableArray<TypeAnnotation>.Empty, DunderNames.Len, lenFunc.LineStart, lenFunc.ColumnStart));
        }

        // __bool__ → IBoolConvertible
        if (dunders.TryGetValue(DunderNames.Bool, out var boolFunc))
        {
            result.Add(("IBoolConvertible", "Sharpy", ImmutableArray<TypeAnnotation>.Empty, DunderNames.Bool, boolFunc.LineStart, boolFunc.ColumnStart));
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
