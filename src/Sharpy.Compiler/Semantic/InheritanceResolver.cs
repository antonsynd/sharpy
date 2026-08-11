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

        var allTypes = GetAllProjectTypeSymbols();

        foreach (var type in allTypes)
        {
            // Resolve base class — check if an immediate base has been resolved yet.
            // Use a direct binding lookup (single dictionary read) rather than
            // GetAllBaseTypes, which would allocate and traverse the full chain.
            var resolvedBase = _semanticBinding.GetBaseType(type) ?? type.BaseType;
            if (resolvedBase == null && !string.IsNullOrEmpty(type.UnresolvedBaseName))
            {
                var baseType = LookupTypeInModuleScopes(type.UnresolvedBaseName);
                if (baseType != null)
                {
                    if (baseType.TypeKind == TypeKind.Interface)
                    {
                        if (!TypeHierarchyService.GetAllInterfaces(type, _semanticBinding).Contains(baseType))
                        {
                            // The written arguments belong to the reference even when the first
                            // base turns out to be an interface — `class Repo(Comparable[int])`
                            // parks its args in UnresolvedBaseTypeArgs because extraction cannot
                            // tell class from interface, and this arm is where that is learned
                            // (#1403).
                            _semanticBinding.AddInterface(type, new InterfaceReference
                            {
                                Definition = baseType,
                                TypeArgAnnotations = type.UnresolvedBaseTypeArgs
                            });
                        }
                    }
                    else
                    {
                        _semanticBinding.SetBaseType(type, baseType);
                        _semanticBinding.SetBaseTypeReference(type, new BaseTypeReference
                        {
                            Definition = baseType,
                            TypeArgAnnotations = type.UnresolvedBaseTypeArgs
                        });
                    }
                    _logger.LogDebug($"Resolved inheritance: {type.Name} : {baseType.Name}");
                }
                else
                {
                    _logger.LogWarning($"Could not resolve base type '{type.UnresolvedBaseName}' for {type.Name}", 0, 0);
                }
            }

            // Resolve interfaces
            var resolvedInterfaces = TypeHierarchyService.GetAllInterfaces(type, _semanticBinding);
            foreach (var ifaceAnnotation in type.UnresolvedInterfaces)
            {
                var ifaceType = LookupTypeInModuleScopes(ifaceAnnotation.Name);
                if (ifaceType != null && !resolvedInterfaces.Contains(ifaceType))
                {
                    // Carry the written arguments onto the reference (#1403). Dropping them here
                    // made `class Repo(Comparable[int])` resolve to an argument-less Comparable, so
                    // members reached through the interface kept its open type parameters.
                    _semanticBinding.AddInterface(type, new InterfaceReference
                    {
                        Definition = ifaceType,
                        TypeArgAnnotations = ifaceAnnotation.TypeArguments
                    });
                    _logger.LogDebug($"Resolved interface: {type.Name} : {ifaceType.Name}");
                }
                else if (ifaceType == null)
                {
                    _logger.LogWarning($"Could not resolve interface '{ifaceAnnotation.Name}' for {type.Name}", 0, 0);
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

            var allTypes = GetAllProjectTypeSymbols();

            foreach (var type in allTypes)
            {
                // Check unresolved base class name
                if (!string.IsNullOrEmpty(type.UnresolvedBaseName) && LookupTypeInModuleScopes(type.UnresolvedBaseName) == null)
                {
                    var found = importResolver.FindTypeInLoadedModules(type.UnresolvedBaseName);
                    if (found != null && _symbolTable.TryDefine(found))
                    {
                        _logger.LogDebug($"Auto-imported transitive base type: {found.Name} (needed by {type.Name})");
                        addedNew = true;
                    }
                }

                // Check unresolved interface names
                foreach (var ifaceAnnotation in type.UnresolvedInterfaces)
                {
                    var ifaceName = ifaceAnnotation.Name;
                    if (LookupTypeInModuleScopes(ifaceName) == null)
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

    /// <summary>
    /// Returns all <see cref="TypeSymbol"/>s from all module scopes AND the global scope.
    /// In project compilation every user-defined symbol lives in a per-module child scope,
    /// so <c>GlobalScope.GetAllSymbols()</c> alone (which is non-recursive) misses them (#1309).
    /// </summary>
    private List<TypeSymbol> GetAllProjectTypeSymbols()
    {
        var types = new List<TypeSymbol>();
        types.AddRange(_symbolTable.GetAllModuleScopeSymbols().OfType<TypeSymbol>());
        types.AddRange(_symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>());
        return types;
    }

    /// <summary>
    /// Looks up a type by name, searching module scopes first (where project symbols live)
    /// and falling back to the global scope (where builtins and single-file types live).
    /// </summary>
    private TypeSymbol? LookupTypeInModuleScopes(string name)
    {
        var symbol = _symbolTable.LookupInModuleScopes(name);
        if (symbol is TypeSymbol ts)
            return ts;
        return _symbolTable.LookupType(name);
    }
}
