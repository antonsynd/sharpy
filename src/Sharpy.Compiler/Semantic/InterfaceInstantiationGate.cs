using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Pre-materialization gate that detects a type implementing the same generic interface at
/// two distinct type-argument instantiations. C# refuses this (CS0738), so the gate surfaces
/// it as SPY0607 before the error reaches codegen as an ICE (#1717, R-H ruling).
/// </summary>
internal static class InterfaceInstantiationGate
{
    private record struct CollectedInterface(
        TypeSymbol Definition,
        IReadOnlyList<SemanticType> ResolvedArgs,
        string SourcePath,
        bool IsClrSource);

    public static void CheckAll(
        SymbolTable symbolTable,
        SemanticBinding semanticBinding,
        SemanticInfo semanticInfo,
        ICompilerLogger logger,
        DiagnosticBag diagnostics)
    {
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols()
            .Concat(symbolTable.GetAllModuleScopeSymbols()))
        {
            if (symbol is TypeSymbol typeSymbol &&
                typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface &&
                visited.Add(typeSymbol))
            {
                Check(typeSymbol, typeResolver, diagnostics);
            }
        }
    }

    public static void Check(
        TypeSymbol typeSymbol,
        TypeResolver typeResolver,
        DiagnosticBag diagnostics)
    {
        var collected = CollectAllInterfaces(typeSymbol, typeResolver);
        if (collected.Count == 0)
            return;

        var groups = new Dictionary<TypeSymbol, List<CollectedInterface>>(
            ReferenceEqualityComparer.Instance);

        foreach (var entry in collected)
        {
            if (!groups.TryGetValue(entry.Definition, out var list))
            {
                list = new List<CollectedInterface>();
                groups[entry.Definition] = list;
            }
            list.Add(entry);
        }

        foreach (var (definition, entries) in groups)
        {
            if (entries.Count < 2)
                continue;

            // Only generic interfaces can conflict
            if (entries.All(e => e.ResolvedArgs.Count == 0))
                continue;

            // Find distinct instantiations by CanonicalKey tuple
            var distinct = new List<CollectedInterface>();
            foreach (var entry in entries)
            {
                bool isDuplicate = false;
                foreach (var existing in distinct)
                {
                    if (ArgKeysMatch(entry.ResolvedArgs, existing.ResolvedArgs))
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                if (!isDuplicate)
                    distinct.Add(entry);
            }

            if (distinct.Count < 2)
                continue;

            // CLR exemption: if ALL contributing paths for this conflict are CLR-sourced, skip
            bool allClr = entries.All(e => e.IsClrSource);
            if (allClr)
                continue;

            // Emit SPY0607 for the first pair of conflicting instantiations
            var first = distinct[0];
            var second = distinct[1];

            var firstDisplay = FormatInterfaceDisplay(definition.Name, first.ResolvedArgs);
            var secondDisplay = FormatInterfaceDisplay(definition.Name, second.ResolvedArgs);

            var message =
                $"Type '{typeSymbol.Name}' implements '{firstDisplay}' ({first.SourcePath}) " +
                $"and '{secondDisplay}' ({second.SourcePath}) " +
                "— one generic interface cannot have conflicting type arguments";

            diagnostics.AddError(
                message,
                line: typeSymbol.DeclarationLine,
                column: typeSymbol.DeclarationColumn,
                filePath: typeSymbol.DeclaringFilePath,
                code: DiagnosticCodes.SemanticOverflow.ConflictingInterfaceInstantiation);
        }
    }

    private static List<CollectedInterface> CollectAllInterfaces(
        TypeSymbol typeSymbol, TypeResolver typeResolver)
    {
        var result = new List<CollectedInterface>();

        // Direct interfaces (includes synthesized since Phase 4)
        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            var args = ResolveArgs(ifaceRef, typeResolver);
            if (args == null)
                continue;

            string sourcePath;
            if (ifaceRef.SynthesizedVia != null)
                sourcePath = $"synthesized via {ifaceRef.SynthesizedVia}";
            else
                sourcePath = "explicit";

            result.Add(new CollectedInterface(
                ifaceRef.Definition,
                args,
                sourcePath,
                IsClrSource: ifaceRef.Definition.ClrType != null));
        }

        // Walk base type chain
        var visited = new HashSet<TypeSymbol>(ReferenceEqualityComparer.Instance);
        var ancestor = typeSymbol.BaseType;
        while (ancestor != null && visited.Add(ancestor))
        {
            foreach (var ifaceRef in ancestor.Interfaces)
            {
                var args = ResolveArgs(ifaceRef, typeResolver);
                if (args == null)
                    continue;

                string sourcePath;
                if (ifaceRef.SynthesizedVia != null)
                    sourcePath = $"inherited from {ancestor.Name}, synthesized via {ifaceRef.SynthesizedVia}";
                else
                    sourcePath = $"inherited from {ancestor.Name}";

                result.Add(new CollectedInterface(
                    ifaceRef.Definition,
                    args,
                    sourcePath,
                    IsClrSource: ancestor.ClrType != null));
            }

            // Also collect from interfaces the ancestor implements (transitive)
            foreach (var ifaceRef in ancestor.Interfaces)
            {
                CollectFromInterfaceHierarchy(ifaceRef.Definition, typeResolver, result, visited);
            }

            ancestor = ancestor.BaseType;
        }

        // Collect from direct interfaces' own hierarchies
        foreach (var ifaceRef in typeSymbol.Interfaces)
        {
            CollectFromInterfaceHierarchy(ifaceRef.Definition, typeResolver, result, visited);
        }

        return result;
    }

    private static void CollectFromInterfaceHierarchy(
        TypeSymbol interfaceSymbol,
        TypeResolver typeResolver,
        List<CollectedInterface> result,
        HashSet<TypeSymbol> visited)
    {
        if (!visited.Add(interfaceSymbol))
            return;

        foreach (var ifaceRef in interfaceSymbol.Interfaces)
        {
            var args = ResolveArgs(ifaceRef, typeResolver);
            if (args == null)
                continue;

            result.Add(new CollectedInterface(
                ifaceRef.Definition,
                args,
                $"via {interfaceSymbol.Name}",
                IsClrSource: interfaceSymbol.ClrType != null));

            CollectFromInterfaceHierarchy(ifaceRef.Definition, typeResolver, result, visited);
        }
    }

    private static IReadOnlyList<SemanticType>? ResolveArgs(
        InterfaceReference ifaceRef, TypeResolver typeResolver)
    {
        if (!ifaceRef.ResolvedTypeArguments.IsDefaultOrEmpty)
            return ifaceRef.ResolvedTypeArguments;

        if (ifaceRef.TypeArgAnnotations.IsDefaultOrEmpty)
            return Array.Empty<SemanticType>();

        var resolved = new List<SemanticType>(ifaceRef.TypeArgAnnotations.Length);
        foreach (var annotation in ifaceRef.TypeArgAnnotations)
        {
            var type = typeResolver.ResolveTypeAnnotation(annotation);
            if (type is UnknownType)
                return null;
            resolved.Add(type);
        }
        return resolved;
    }

    private static bool ArgKeysMatch(
        IReadOnlyList<SemanticType> a, IReadOnlyList<SemanticType> b)
    {
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].CanonicalKey != b[i].CanonicalKey)
                return false;
        }
        return true;
    }

    private static string FormatInterfaceDisplay(
        string definitionName, IReadOnlyList<SemanticType> args)
    {
        if (args.Count == 0)
            return definitionName;
        var argDisplay = string.Join(", ", args.Select(a => a.GetDisplayName()));
        return $"{definitionName}[{argDisplay}]";
    }
}
