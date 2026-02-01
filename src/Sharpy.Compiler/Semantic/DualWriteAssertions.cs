using System.Diagnostics;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Shared dual-write consistency assertions used by both Compiler and ProjectCompiler.
/// These verify that SemanticBinding entries are consistent with Symbol properties,
/// catching bugs where one path sets the symbol but not SemanticBinding (or vice versa).
/// All methods are compiled out in Release builds.
/// </summary>
internal static class DualWriteAssertions
{
    /// <summary>
    /// Verify that SemanticBinding BaseType entries are consistent with Symbol.BaseType.
    /// Only checks types resolved by NameResolver and InheritanceResolver (not CLR types from ModuleRegistry).
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertInheritanceConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types (from ModuleRegistry) - they don't go through the dual-write path
            if (symbol.ClrType != null)
                continue;

            if (symbol.BaseType != null)
            {
                var bindingBaseType = semanticBinding.GetBaseType(symbol);
                Debug.Assert(bindingBaseType != null,
                    $"TypeSymbol '{symbol.Name}' has BaseType '{symbol.BaseType.Name}' but SemanticBinding.GetBaseType() returned null (dual-write inconsistency)");
            }

            if (symbol.Interfaces.Count > 0)
            {
                var bindingInterfaces = semanticBinding.GetInterfaces(symbol);
                Debug.Assert(bindingInterfaces != null && bindingInterfaces.Count == symbol.Interfaces.Count,
                    $"TypeSymbol '{symbol.Name}' has {symbol.Interfaces.Count} interface(s) but SemanticBinding.GetInterfaces() returned {bindingInterfaces?.Count ?? 0} (dual-write inconsistency)");
            }
        }
    }

    /// <summary>
    /// Verify that SemanticBinding CodeGenInfo entries are consistent with Symbol.CodeGenInfo.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertCodeGenInfoConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            // Skip re-exported symbols (from other modules) - their CodeGenInfo was set
            // in a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            if (symbol.CodeGenInfo != null)
            {
                var bindingCodeGenInfo = semanticBinding.GetCodeGenInfo(symbol);
                Debug.Assert(bindingCodeGenInfo != null,
                    $"Symbol '{symbol.Name}' has CodeGenInfo but SemanticBinding.GetCodeGenInfo() returned null (dual-write inconsistency)");
            }
        }
    }

    /// <summary>
    /// Verify that SemanticBinding VariableType entries are consistent with VariableSymbol.Type.
    /// Only checks global-scope variables (fields, module-level vars/consts). Local variables
    /// and parameters are scoped and not accessible from the global scope.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertVariableTypeConsistency(SymbolTable symbolTable, SemanticBinding semanticBinding)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<VariableSymbol>())
        {
            // Skip re-exported variables (from other modules) - they were typed in
            // a different compilation's SemanticBinding
            if (symbol.IsReExport)
                continue;

            if (symbol.Type != SemanticType.Unknown)
            {
                var bindingType = semanticBinding.GetVariableType(symbol);
                Debug.Assert(bindingType != SemanticType.Unknown,
                    $"VariableSymbol '{symbol.Name}' has Type '{symbol.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (dual-write inconsistency)");
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
                        $"Field '{typeSymbol.Name}.{field.Name}' has Type '{field.Type.GetDisplayName()}' but SemanticBinding.GetVariableType() returned Unknown (dual-write inconsistency)");
                }
            }
        }
    }
}
