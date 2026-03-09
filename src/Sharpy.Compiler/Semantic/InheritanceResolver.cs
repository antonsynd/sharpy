using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Consolidates all inheritance resolution logic into a single class.
///
/// Inheritance resolution happens in multiple stages:
/// 1. Local types: NameResolver.ResolveInheritance() resolves inheritance for types defined in the current compilation
/// 2. Transitive imports: ResolveTransitiveBaseTypes() auto-imports base types from loaded modules
/// 3. Imported types: ResolveImportedTypeInheritance() resolves string-based base names to TypeSymbol references
///
/// This class handles stages 2 and 3. Stage 1 is handled by NameResolver which has access to the AST
/// definitions needed for local type inheritance.
/// </summary>
internal class InheritanceResolver
{
    private readonly SymbolTable _symbolTable;
    private readonly ICompilerLogger _logger;
    private readonly SemanticBinding _semanticBinding;

    public InheritanceResolver(SymbolTable symbolTable, ICompilerLogger? logger = null, SemanticBinding? semanticBinding = null)
    {
        _symbolTable = symbolTable;
        _logger = logger ?? NullLogger.Instance;
        _semanticBinding = semanticBinding ?? new SemanticBinding();
    }

    /// <summary>
    /// Resolve all inheritance relationships for imported types.
    /// This should be called after all imports are registered but before type checking.
    ///
    /// Performs two operations:
    /// 1. Auto-imports transitive base types from loaded modules (fixpoint iteration)
    /// 2. Resolves string-based base/interface names to actual TypeSymbol references
    /// </summary>
    /// <param name="importResolver">Import resolver with loaded modules for transitive type discovery.</param>
    public void ResolveAll(ImportResolver? importResolver = null)
    {
        if (importResolver != null)
        {
            ResolveTransitiveBaseTypes(importResolver);
        }
        ResolveImportedTypeInheritance();
    }

    /// <summary>
    /// Resolve inheritance relationships for imported types.
    /// Imported types have their base class/interface names stored as strings;
    /// this method resolves them to actual TypeSymbol references.
    /// </summary>
    public void ResolveImportedTypeInheritance()
    {
        _logger.LogDebug("Resolving inheritance for imported types...");

        var allTypes = _symbolTable.GlobalScope.GetAllSymbols()
            .OfType<TypeSymbol>()
            .ToList();

        foreach (var type in allTypes)
        {
            // Resolve base class — check if an immediate base has been resolved yet.
            // Use a direct binding lookup (single dictionary read) rather than
            // GetAllBaseTypes, which would allocate and traverse the full chain.
            var resolvedBase = _semanticBinding.GetBaseType(type) ?? type.BaseType;
            if (resolvedBase == null && !string.IsNullOrEmpty(type.UnresolvedBaseName))
            {
                var baseType = _symbolTable.LookupType(type.UnresolvedBaseName);
                if (baseType != null)
                {
                    if (baseType.TypeKind == TypeKind.Interface)
                    {
                        if (!TypeHierarchyService.GetAllInterfaces(type, _semanticBinding).Contains(baseType))
                        {
                            _semanticBinding.AddInterface(type, baseType);
                        }
                    }
                    else
                    {
                        _semanticBinding.SetBaseType(type, baseType);
                    }
                    _logger.LogDebug($"Resolved inheritance: {type.Name} : {baseType.Name}");
                }
                else
                {
                    _logger.LogWarning($"Could not resolve base type '{type.UnresolvedBaseName}' for {type.Name}", 0, 0);
                }
            }

            // Resolve interfaces
            foreach (var ifaceName in type.UnresolvedInterfaceNames)
            {
                var ifaceType = _symbolTable.LookupType(ifaceName);
                if (ifaceType != null && !TypeHierarchyService.GetAllInterfaces(type, _semanticBinding).Contains(ifaceType))
                {
                    _semanticBinding.AddInterface(type, ifaceType);
                    _logger.LogDebug($"Resolved interface: {type.Name} : {ifaceType.Name}");
                }
                else if (ifaceType == null)
                {
                    _logger.LogWarning($"Could not resolve interface '{ifaceName}' for {type.Name}", 0, 0);
                }
            }
        }
    }

    /// <summary>
    /// Auto-import transitive base types that are referenced by imported types but not
    /// explicitly imported by the user. Iterates until stable (fixpoint) to handle
    /// multi-level inheritance chains like Entity -> NamedEntity -> User.
    /// </summary>
    public void ResolveTransitiveBaseTypes(ImportResolver importResolver)
    {
        _logger.LogDebug("Resolving transitive base types from loaded modules...");

        const int maxIterations = 100;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            bool addedNew = false;

            var allTypes = _symbolTable.GlobalScope.GetAllSymbols()
                .OfType<TypeSymbol>()
                .ToList();

            foreach (var type in allTypes)
            {
                // Check unresolved base class name
                if (!string.IsNullOrEmpty(type.UnresolvedBaseName) && _symbolTable.LookupType(type.UnresolvedBaseName) == null)
                {
                    var found = importResolver.FindTypeInLoadedModules(type.UnresolvedBaseName);
                    if (found != null && _symbolTable.TryDefine(found))
                    {
                        _logger.LogDebug($"Auto-imported transitive base type: {found.Name} (needed by {type.Name})");
                        addedNew = true;
                    }
                }

                // Check unresolved interface names
                foreach (var ifaceName in type.UnresolvedInterfaceNames)
                {
                    if (_symbolTable.LookupType(ifaceName) == null)
                    {
                        var found = importResolver.FindTypeInLoadedModules(ifaceName);
                        if (found != null && _symbolTable.TryDefine(found))
                        {
                            _logger.LogDebug($"Auto-imported transitive interface type: {found.Name} (needed by {type.Name})");
                            addedNew = true;
                        }
                    }
                }
            }

            if (!addedNew)
                break;
        }
    }
}
