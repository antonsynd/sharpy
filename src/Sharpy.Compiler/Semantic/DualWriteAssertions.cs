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
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
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
                var bindingInterfaces = semanticBinding.GetInterfaces(symbol);
                if (bindingInterfaces == null || bindingInterfaces.Count != symbol.Interfaces.Count)
                {
                    throw new InvalidOperationException(
                        $"TypeSymbol '{symbol.Name}' has {symbol.Interfaces.Count} interface(s) but SemanticBinding.GetInterfaces() returned {bindingInterfaces?.Count ?? 0} (materialization inconsistency). " +
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

            var sbInterfaces = semanticBinding.GetInterfaces(symbol);
            if (sbInterfaces != null && sbInterfaces.Count > 0)
            {
                if (symbol.Interfaces.Count < sbInterfaces.Count)
                {
                    throw new InvalidOperationException(
                        $"SemanticBinding has {sbInterfaces.Count} interface(s) for '{symbol.Name}' but Symbol.Interfaces has {symbol.Interfaces.Count} (materialization missed). " +
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
