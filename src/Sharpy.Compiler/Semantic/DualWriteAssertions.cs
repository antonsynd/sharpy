using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Materialization correctness assertions used by both Compiler and ProjectCompiler.
/// These verify that after MaterializeXxx() calls, SemanticBinding entries are consistent
/// with the corresponding Symbol properties. This catches bugs where materialization
/// failed to copy data from SemanticBinding stores onto Symbol properties.
/// All methods are always active (not DEBUG-only) to catch issues in production.
/// </summary>
internal static class DualWriteAssertions
{
    /// <summary>
    /// Verify that after MaterializeInheritance(), Symbol.BaseType and Symbol.Interfaces
    /// are consistent with SemanticBinding stores.
    /// Only checks types resolved by NameResolver and InheritanceResolver (not CLR types from ModuleRegistry).
    /// </summary>
    internal static void AssertInheritanceConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        var allTypes = symbolTable.GetAllModuleScopeSymbols()
            .Concat(symbolTable.GlobalScope.GetAllSymbols())
            .OfType<TypeSymbol>();
        foreach (var symbol in allTypes)
        {
            // Skip CLR types (from ModuleRegistry) - they don't go through the materialization path
            if (symbol.ClrType != null)
                continue;

            // Skip re-exported types (from other modules) - their inheritance was set
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            // Forward: Symbol → SemanticBinding
            if (symbol.BaseType != null)
            {
                var bindingBaseType = semanticBinding.GetBaseType(symbol);
                if (bindingBaseType == null)
                {
                    throw new InvalidOperationException(
                        $"TypeSymbol '{symbol.Name}' has BaseType '{symbol.BaseType.Name}' but SemanticBinding.GetBaseType() returned null (materialization inconsistency). " +
                        "This is a compiler bug - please report it.");
                }
            }

            if (symbol.Interfaces.Count > 0)
            {
                var bindingInterfaceRefs = semanticBinding.GetInterfaces(symbol);
                if (bindingInterfaceRefs == null || bindingInterfaceRefs.Count != symbol.Interfaces.Count)
                {
                    throw new InvalidOperationException(
                        $"TypeSymbol '{symbol.Name}' has {symbol.Interfaces.Count} interface(s) but SemanticBinding.GetInterfaces() returned {bindingInterfaceRefs?.Count ?? 0} (materialization inconsistency). " +
                        "This is a compiler bug - please report it.");
                }
            }

            // Reverse: SemanticBinding → Symbol (catches materialization failures)
            var sbBaseType = semanticBinding.GetBaseType(symbol);
            if (sbBaseType != null)
            {
                if (symbol.BaseType == null)
                {
                    throw new InvalidOperationException(
                        $"SemanticBinding has BaseType '{sbBaseType.Name}' for '{symbol.Name}' but Symbol.BaseType is null (materialization missed). " +
                        "This is a compiler bug - please report it.");
                }
            }

            // BaseTypeRef consistency (#1287)
            var sbBaseRef = semanticBinding.GetBaseTypeReference(symbol);
            if (sbBaseRef != null && symbol.BaseTypeRef == null)
            {
                throw new InvalidOperationException(
                    $"SemanticBinding has BaseTypeReference for '{symbol.Name}' but Symbol.BaseTypeRef is null (materialization missed). " +
                    "This is a compiler bug - please report it.");
            }
            if (symbol.BaseTypeRef != null && sbBaseRef == null)
            {
                throw new InvalidOperationException(
                    $"TypeSymbol '{symbol.Name}' has BaseTypeRef but SemanticBinding.GetBaseTypeReference() returned null (materialization inconsistency). " +
                    "This is a compiler bug - please report it.");
            }

            // PRESENCE, not just parity (#1287). The two checks above compare the stores against
            // each other, so they agree perfectly when BOTH are empty — deleting the
            // SetBaseTypeReference call in NameResolver fired nothing, which is the one mutation
            // this guard exists to catch. A base that DECLARES type parameters cannot have been
            // written without arguments: the base list is arity-validated at resolution time
            // (ValidateBaseReferenceArity) and a bare generic base is refused outright (#1286).
            // So for a source-declared class, "generic base present, no reference" is a drop.
            //
            // Scoped to symbols that came from source. A synthesized type (union/Optional/Result
            // backing) has no base annotation to carry and is exempt by construction — it has no
            // declaring file — and CLR-discovered and re-exported symbols were already skipped
            // above, so a legitimate empty never reaches here.
            if (symbol.BaseType is { } declaredBase
                && declaredBase.TypeParameters.Count > 0
                && symbol.DeclaringFilePath != null
                && symbol.BaseTypeRef == null)
            {
                throw new InvalidOperationException(
                    $"TypeSymbol '{symbol.Name}' inherits the generic base '{declaredBase.Name}' " +
                    $"(declaring {declaredBase.TypeParameters.Count} type parameter(s)) but carries no " +
                    "BaseTypeReference, so its base-type arguments were dropped. The supertype walker " +
                    "would answer from a positional copy instead of the written arguments (#1287). " +
                    "This is a compiler bug - please report it.");
            }

            var sbInterfaceRefs = semanticBinding.GetInterfaces(symbol);
            if (sbInterfaceRefs != null && sbInterfaceRefs.Count > 0)
            {
                if (symbol.Interfaces.Count < sbInterfaceRefs.Count)
                {
                    throw new InvalidOperationException(
                        $"SemanticBinding has {sbInterfaceRefs.Count} interface(s) for '{symbol.Name}' but Symbol.Interfaces has {symbol.Interfaces.Count} (materialization missed). " +
                        "This is a compiler bug - please report it.");
                }
            }
        }
    }

    /// <summary>
    /// Verify that after MaterializeCodeGenInfo(), Symbol.CodeGenInfo properties are
    /// consistent with SemanticBinding stores.
    /// </summary>
    internal static void AssertCodeGenInfoConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            // Skip re-exported symbols (from other modules) - their CodeGenInfo was materialized
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            // Forward: Symbol → SemanticBinding
            if (symbol.CodeGenInfo != null)
            {
                var bindingCodeGenInfo = semanticBinding.GetCodeGenInfo(symbol);
                if (bindingCodeGenInfo == null)
                {
                    throw new InvalidOperationException(
                        $"Symbol '{symbol.Name}' has CodeGenInfo but SemanticBinding.GetCodeGenInfo() returned null (materialization inconsistency). " +
                        "This is a compiler bug - please report it.");
                }
            }

            // Reverse: SemanticBinding → Symbol (catches materialization failures)
            var sbCodeGenInfo = semanticBinding.GetCodeGenInfo(symbol);
            if (sbCodeGenInfo != null)
            {
                if (symbol.CodeGenInfo == null)
                {
                    throw new InvalidOperationException(
                        $"SemanticBinding has CodeGenInfo for '{symbol.Name}' but Symbol.CodeGenInfo is null (materialization missed). " +
                        "This is a compiler bug - please report it.");
                }
            }
        }
    }

    /// <summary>
    /// Verify that after MaterializeVariableTypes(), VariableSymbol.Type properties are
    /// consistent with SemanticBinding stores.
    /// Only checks global-scope variables (fields, module-level vars/consts). Local variables
    /// and parameters are scoped and not accessible from the global scope.
    /// </summary>
    internal static void AssertVariableTypeConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<VariableSymbol>())
        {
            // Skip re-exported variables (from other modules) - they were materialized
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            // Forward: Symbol → SemanticBinding
            if (symbol.Type != SemanticType.Unknown)
            {
                var bindingType = semanticBinding.GetVariableType(symbol);
                if (bindingType == SemanticType.Unknown)
                {
                    throw new InvalidOperationException(
                        $"VariableSymbol '{symbol.Name}' has Type '{symbol.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (materialization inconsistency). " +
                        "This is a compiler bug - please report it.");
                }
            }

            // Reverse: SemanticBinding → Symbol (catches materialization failures)
            var sbType = semanticBinding.GetVariableType(symbol);
            if (sbType != SemanticType.Unknown)
            {
                if (symbol.Type == SemanticType.Unknown)
                {
                    throw new InvalidOperationException(
                        $"SemanticBinding has Type '{sbType.GetDisplayName()}' for '{symbol.Name}' but Symbol.Type is Unknown (materialization missed). " +
                        "This is a compiler bug - please report it.");
                }
            }
        }

        // Also check fields on locally-defined type symbols
        foreach (var typeSymbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types (from ModuleRegistry) and imported types (from other modules)
            // - they have their fields typed in a different compilation's SemanticBinding
            if (typeSymbol.ClrType != null || typeSymbol.DefiningModule != null)
                continue;

            foreach (var field in typeSymbol.Fields)
            {
                // Forward: Symbol → SemanticBinding
                if (field.Type != SemanticType.Unknown)
                {
                    var bindingType = semanticBinding.GetVariableType(field);
                    if (bindingType == SemanticType.Unknown)
                    {
                        throw new InvalidOperationException(
                            $"Field '{typeSymbol.Name}.{field.Name}' has Type '{field.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (materialization inconsistency). " +
                            "This is a compiler bug - please report it.");
                    }
                }

                // Reverse: SemanticBinding → Symbol (catches materialization failures)
                var sbFieldType = semanticBinding.GetVariableType(field);
                if (sbFieldType != SemanticType.Unknown)
                {
                    if (field.Type == SemanticType.Unknown)
                    {
                        throw new InvalidOperationException(
                            $"SemanticBinding has Type '{sbFieldType.GetDisplayName()}' for field '{typeSymbol.Name}.{field.Name}' but Symbol.Type is Unknown (materialization missed). " +
                            "This is a compiler bug - please report it.");
                    }
                }
            }
        }
    }
}
