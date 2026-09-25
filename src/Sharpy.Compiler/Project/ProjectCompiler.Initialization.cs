using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

internal partial class ProjectCompiler
{
    /// <summary>
    /// Phase 2: Initialize shared state (symbol table, semantic info)
    /// </summary>
    private void InitializeSharedState()
    {
        var builtinRegistry = _sharedBuiltinRegistry ?? new BuiltinRegistry(_logger);
        _symbolTableBacking = new SymbolTable(builtinRegistry);
        _semanticInfoBacking = new SemanticInfo();
        _semanticInfoBacking.SetSymbolTable(_symbolTableBacking);
        // Create SemanticBinding for storing semantic data separate from AST
        var semanticBinding = new SemanticBinding();

        // Store in ProjectModel
        _projectModel!.GlobalSymbols = SymbolTable;
        _projectModel.SemanticInfo = SemanticInfo;
        _projectModel.SemanticBinding = semanticBinding;

        // Initialize dependency graph builder
        _graphBuilderBacking = new DependencyGraphBuilder();

        // Create import resolver with all dependencies injected via constructor
        _importResolverBacking = new ImportResolver(_logger, _moduleRegistry,
            semanticBinding: semanticBinding, dependencyRecorder: GraphBuilder);
        // An import of a file this compilation owns must export THIS compilation's symbols, not
        // ModuleLoader's re-extraction of them (#1366, #1407, #1410).
        _importResolverBacking.OwnSymbolResolver = ResolveOwnExportedSymbol;
        // …and the overload list beside it, which is a second channel with its own dictionary and
        // was left holding extractions when the single-symbol channel was re-pointed (#1491).
        _importResolverBacking.OwnOverloadResolver = ResolveOwnExportedOverloads;

        // Register all parsed files in the dependency graph
        foreach (var sourceFile in _projectModel!.Units.Keys)
        {
            GraphBuilder.AddFile(sourceFile);
        }

        // Restore cached symbols for skipped files (incremental compilation)
        if (_incremental && _incrementalCache != null && _filesToSkip.Count > 0)
        {
            RestoreCachedSymbols();
        }
    }

    /// <summary>
    /// Restore symbols from cache for files that were skipped during incremental compilation.
    /// </summary>
    /// <remarks>
    /// Four steps: decode every skipped file's entry, relink the decoded signature types to their
    /// symbols (#2027 — the decode is symbol-less and a signature can name a type from a file
    /// restored after it), derive each restored type's method-indexed tables, then define each
    /// file's symbols in its module scope. The binding's variable types are written in the last
    /// step, so they carry the relinked types.
    /// </remarks>
    private void RestoreCachedSymbols()
    {
        if (_incrementalCache == null)
            return;

        var semanticBinding = _projectModel!.SemanticBinding;
        var restoredByFile = new List<(string FilePath, List<Symbol> Symbols)>();

        foreach (var filePath in _filesToSkip)
        {
            // Snapshot keys before restoring this file's symbols so we only
            // define the newly-added ones — the old code iterated the accumulated
            // _restoredSymbols.Values, re-TryDefine-ing earlier files' symbols
            // into every later file's module scope (#1309).
            var keysBefore = new HashSet<string>(_restoredSymbols.Keys);
            if (_incrementalCache.RestoreSymbols(filePath, _restoredSymbols, semanticBinding))
            {
                restoredByFile.Add((filePath, _restoredSymbols
                    .Where(kv => !keysBefore.Contains(kv.Key))
                    .Select(kv => kv.Value)
                    .ToList()));
            }
        }

        var relinker = new RestoredTypeRelinker(CreateCacheOriginResolver());
        foreach (var symbol in restoredByFile.SelectMany(file => file.Symbols))
            relinker.RelinkSymbol(symbol);

        // The method-indexed tables are not on the wire; derive them as the declaring build did.
        // A nested type is reachable both as a restored entry and through its declaring type.
        var derived = new HashSet<TypeSymbol>();
        void DeriveTables(TypeSymbol type)
        {
            if (!derived.Add(type))
                return;
            type.DeriveMethodTables();
            foreach (var nested in type.NestedTypes)
                DeriveTables(nested);
        }
        foreach (var type in restoredByFile.SelectMany(file => file.Symbols).OfType<TypeSymbol>())
            DeriveTables(type);

        foreach (var (filePath, newSymbols) in restoredByFile)
        {
            // Enter the file's module scope so restored symbols register in the correct scope
            var unit = _projectModel!.GetUnit(filePath);
            if (unit != null)
                SymbolTable.EnterModuleScope(unit.ModulePath);

            try
            {
                foreach (var symbol in newSymbols)
                {
                    if (symbol is TypeSymbol typeSymbol)
                    {
                        SymbolTable.TryDefine(symbol);

                        // Write resolved inheritance into SemanticBinding so Phase 4c's
                        // MaterializeInheritance handles it — restore runs at Phase 2,
                        // freeze at 4c, so this is pre-freeze and safe (#1309).
                        if (typeSymbol.BaseType != null)
                        {
                            semanticBinding.SetBaseType(typeSymbol, typeSymbol.BaseType);
                            if (typeSymbol.BaseTypeRef != null)
                            {
                                semanticBinding.SetBaseTypeReference(typeSymbol, typeSymbol.BaseTypeRef);
                            }
                        }
                        // The FULL reference: a restored `class R(IA[int?])` or a dunder-
                        // synthesized entry keeps its type arguments and SynthesizedVia on the
                        // binding side too, so the closure gate and the synthesized-interface
                        // read see on a warm build exactly what they saw cold (#1746, #1717).
                        foreach (var iface in typeSymbol.Interfaces)
                        {
                            semanticBinding.AddInterface(typeSymbol, iface);
                        }

                        // Register variable types for fields
                        foreach (var field in typeSymbol.Fields)
                        {
                            if (field.Type != SemanticType.Unknown)
                            {
                                semanticBinding.SetVariableType(field, field.Type);
                            }
                        }
                    }
                    else if (symbol is FunctionSymbol)
                    {
                        SymbolTable.TryDefine(symbol);
                    }
                    else if (symbol is VariableSymbol vs && !vs.IsParameter)
                    {
                        SymbolTable.TryDefine(symbol);

                        // Register variable type in SemanticBinding
                        if (vs.Type != SemanticType.Unknown)
                        {
                            semanticBinding.SetVariableType(vs, vs.Type);
                        }
                    }
                }
            }
            finally
            {
                if (unit != null)
                    SymbolTable.ExitScope();
            }
        }

        if (restoredByFile.Count > 0)
        {
            _logger.LogInfo($"Restored symbols from {restoredByFile.Count} cached file(s)");
        }
    }

    /// <summary>
    /// Binds a cache-decoded type to the symbol its origin names (#2027, <see cref="CachedTypeOrigin"/>):
    /// a <c>file:</c> origin to the type restored from that file (by its dotted name, so a nested
    /// <c>H.C</c> never binds to a top-level <c>C</c>); <c>module:</c> through the module registry;
    /// <c>clr:</c> to the builtins registry's type of that name when its CLR type is the carried one,
    /// else to the CLR type's own symbol. Null when the origin names nothing — the type stays
    /// symbol-less, exactly as before the origin travelled.
    /// </summary>
    private Func<UserDefinedType, TypeSymbol?> CreateCacheOriginResolver()
    {
        var restoredTypes = new Dictionary<(string File, string Name), TypeSymbol>();
        void Index(TypeSymbol type, string? declaringFile)
        {
            var file = type.DefiningFilePath ?? type.DeclaringFilePath ?? declaringFile;
            if (file is { Length: > 0 })
                restoredTypes.TryAdd((PathNormalizer.Normalize(file), CachedTypeOrigin.QualifiedName(type)), type);
            foreach (var nested in type.NestedTypes)
                Index(nested, file);
        }
        foreach (var type in _restoredSymbols.Values.OfType<TypeSymbol>())
            Index(type, null);

        var registry = SymbolTable.BuiltinRegistry;
        // One symbol per (origin, name) for the whole restore, as a cold build has one per type.
        var resolved = new Dictionary<(string Origin, string Name), TypeSymbol?>();
        var moduleTypes = new Dictionary<string, List<TypeSymbol>>(StringComparer.Ordinal);

        return udt =>
        {
            var key = (udt.CacheOrigin!, udt.Name);
            if (!resolved.TryGetValue(key, out var symbol))
            {
                symbol = Resolve(udt.CacheOrigin!, udt.Name);
                resolved[key] = symbol;
            }
            return symbol;
        };

        TypeSymbol? Resolve(string origin, string name)
        {
            if (origin.StartsWith(CachedTypeOrigin.FilePrefix, StringComparison.Ordinal))
            {
                return restoredTypes.GetValueOrDefault((origin[CachedTypeOrigin.FilePrefix.Length..], name));
            }

            if (origin.StartsWith(CachedTypeOrigin.ModulePrefix, StringComparison.Ordinal))
            {
                var module = origin[CachedTypeOrigin.ModulePrefix.Length..];
                if (_moduleRegistry == null)
                    return null;
                if (!moduleTypes.TryGetValue(module, out var exported))
                {
                    // The two export channels an import reads: a discovered stdlib module, else a
                    // .NET namespace (`from system.text import StringBuilder`).
                    exported = _moduleRegistry.GetModuleTypes(module);
                    if (exported.Count == 0 && _moduleRegistry.IsNetNamespace(module))
                        exported = _moduleRegistry.GetNamespaceTypes(module);
                    moduleTypes[module] = exported;
                }
                return exported.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal));
            }

            if (origin.StartsWith(CachedTypeOrigin.ClrPrefix, StringComparison.Ordinal))
            {
                var clrName = origin[CachedTypeOrigin.ClrPrefix.Length..];
                if (registry.GetType(name) is { } registered
                    && string.Equals(registered.ClrType?.FullName, clrName, StringComparison.Ordinal))
                {
                    return registered;
                }

                return Discovery.ClrTypeHelper.ResolveClrTypeByStoredName(clrName) is { } clrType
                    ? _moduleRegistry?.CreateTypeSymbolFromClrType(clrType)
                    : null;
            }

            return null;
        }
    }

    /// <summary>
    /// Derives <c>CodeGenInfo.SynthesizedInterfaces</c> for every restored type (#1746). The fact is
    /// not on the wire (RULED 2026-09-07: derive, do not grow the format); it is READ off the
    /// restored type's MATERIALIZED closure through the same reader the cold path uses — one owner
    /// — so cold == warm. Must run after <c>MaterializeInheritance</c> (the closure is on the symbol
    /// only then) and before <c>MaterializeCodeGenInfo</c> (which bridges the fact into the restored
    /// CodeGenInfo). Each file's module scope is entered so the references' written arguments
    /// (<c>IEquatable[Foo]</c>) resolve.
    /// </summary>
    private void DeriveSynthesizedInterfacesForRestoredTypes()
    {
        var semanticBinding = _projectModel!.SemanticBinding;
        var resolver = new TypeResolver(SymbolTable, SemanticInfo, _logger);
        var canEnter = SymbolTable.CurrentScope == SymbolTable.GlobalScope;

        foreach (var group in _restoredSymbols.Values.OfType<TypeSymbol>()
                     .GroupBy(t => t.DefiningFilePath ?? t.DeclaringFilePath ?? string.Empty))
        {
            var unit = group.Key.Length > 0 ? _projectModel.GetUnit(group.Key) : null;
            var entered = canEnter && unit != null;
            if (entered)
                SymbolTable.EnterModuleScope(unit!.ModulePath);
            try
            {
                foreach (var restoredType in group)
                {
                    var synthesized = SynthesizedInterfaceReader.Read(restoredType, resolver);
                    if (synthesized.Count > 0)
                        semanticBinding.SetSynthesizedInterfaces(restoredType, synthesized);
                }
            }
            finally
            {
                if (entered)
                    SymbolTable.ExitScope();
            }
        }
    }
}
