using System.Diagnostics;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Materialization correctness assertions used by both Compiler and ProjectCompiler.
/// These verify that after MaterializeXxx() calls, SemanticBinding entries are consistent
/// with the corresponding Symbol properties. This catches bugs where materialization
/// failed to copy data from SemanticBinding stores onto Symbol properties.
/// All methods are compiled out in Release builds.
/// </summary>
internal static class DualWriteAssertions
{
    /// <summary>
    /// Verify that after MaterializeInheritance(), Symbol.BaseType and Symbol.Interfaces
    /// are consistent with SemanticBinding stores.
    /// Only checks types resolved by NameResolver and InheritanceResolver (not CLR types from ModuleRegistry).
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertInheritanceConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types (from ModuleRegistry) - they don't go through the materialization path
            if (symbol.ClrType != null)
                continue;

            if (symbol.BaseType != null)
            {
                var bindingBaseType = semanticBinding.GetBaseType(symbol);
                Debug.Assert(bindingBaseType != null,
                    $"TypeSymbol '{symbol.Name}' has BaseType '{symbol.BaseType.Name}' but SemanticBinding.GetBaseType() returned null (materialization inconsistency)");
            }

            if (symbol.Interfaces.Count > 0)
            {
                var bindingInterfaces = semanticBinding.GetInterfaces(symbol);
                Debug.Assert(bindingInterfaces != null && bindingInterfaces.Count == symbol.Interfaces.Count,
                    $"TypeSymbol '{symbol.Name}' has {symbol.Interfaces.Count} interface(s) but SemanticBinding.GetInterfaces() returned {bindingInterfaces?.Count ?? 0} (materialization inconsistency)");
            }
        }
    }

    /// <summary>
    /// Verify that after MaterializeCodeGenInfo(), Symbol.CodeGenInfo properties are
    /// consistent with SemanticBinding stores.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertCodeGenInfoConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            // Skip re-exported symbols (from other modules) - their CodeGenInfo was materialized
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            if (symbol.CodeGenInfo != null)
            {
                var bindingCodeGenInfo = semanticBinding.GetCodeGenInfo(symbol);
                Debug.Assert(bindingCodeGenInfo != null,
                    $"Symbol '{symbol.Name}' has CodeGenInfo but SemanticBinding.GetCodeGenInfo() returned null (materialization inconsistency)");
            }
        }
    }

    /// <summary>
    /// Verify that after MaterializeVariableTypes(), VariableSymbol.Type properties are
    /// consistent with SemanticBinding stores.
    /// Only checks global-scope variables (fields, module-level vars/consts). Local variables
    /// and parameters are scoped and not accessible from the global scope.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertVariableTypeConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<VariableSymbol>())
        {
            // Skip re-exported variables (from other modules) - they were materialized
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            if (symbol.Type != SemanticType.Unknown)
            {
                var bindingType = semanticBinding.GetVariableType(symbol);
                Debug.Assert(bindingType != SemanticType.Unknown,
                    $"VariableSymbol '{symbol.Name}' has Type '{symbol.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (materialization inconsistency)");
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
                if (field.Type != SemanticType.Unknown)
                {
                    var bindingType = semanticBinding.GetVariableType(field);
                    Debug.Assert(bindingType != SemanticType.Unknown,
                        $"Field '{typeSymbol.Name}.{field.Name}' has Type '{field.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (materialization inconsistency)");
                }
            }
        }
    }
}
