using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// ImportResolver partial class: top-level import resolution entry points and module loading
/// (.spy modules via <see cref="ModuleLoader"/>, .NET assembly modules, .NET namespace modules,
/// and synthetic compiler-provided modules).
/// </summary>
internal partial class ImportResolver
{
    /// <summary>
    /// Resolve all imports in a module and register the imported symbols in the symbol table.
    /// This is the main entry point for import resolution during compilation.
    /// </summary>
    public void ResolveAllImports(Module module, SymbolTable symbolTable, string? currentDir,
        CancellationToken cancellationToken = default, string? currentModulePath = null)
    {
        _cancellationToken = cancellationToken;
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _logger.LogInfo("Starting import resolution");
        var importCount = 0;
        // Tracks which module each symbol name was imported from, used only by
        // from-imports. Plain `import` statements register a ModuleSymbol whose
        // name is the module itself, so name collisions are module-vs-module (handled
        // by SymbolTable.TryDefine). From-imports pull individual symbols into the
        // current scope, where the same name can arrive from different modules —
        // importedSymbolSources detects that conflict and emits a diagnostic.
        var importedSymbolSources = new Dictionary<string, string>();

        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (statement.UnwrapDecorated() is ImportStatement import)
            {
                importCount++;
                var modules = ResolveImport(import, currentDir);

                // Register module symbols and their exports
                for (int i = 0; i < import.Names.Length && i < modules.Count; i++)
                {
                    var importAlias = import.Names[i];
                    var moduleInfo = modules[i];

                    // Skip null entries (should not happen anymore, but defensive check)
                    if (moduleInfo == null)
                        continue;

                    // Plain imports of stub modules can't be deferred — user needs full module access
                    if (moduleInfo.IsStub)
                        _failedDeferredModules.Add(moduleInfo.Path);

                    // Handle aliased imports (import x as y)
                    if (importAlias.AsName != null)
                    {
                        var aliasedModule = new ModuleSymbol
                        {
                            Name = importAlias.AsName,
                            Kind = SymbolKind.Module,
                            FilePath = moduleInfo.Path,
                            Exports = BuildExportsFor(moduleInfo),
                            FunctionOverloads = BuildFunctionOverloadsFor(moduleInfo),
                            IsErrorRecovery = moduleInfo.IsErrorRecovery,
                            IsNetModule = moduleInfo.IsNetModule,
                            CanonicalModuleName = moduleInfo.CanonicalModuleName,
                            NetNamespaceName = moduleInfo.NetNamespaceName,
                            CSharpNamespace = moduleInfo.CSharpNamespace,
                            Documentation = moduleInfo.Module?.DocString
                                ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name),
                            NameDeclarationLine = importAlias.LineStart,
                            NameDeclarationColumn = importAlias.ColumnStart
                        };
                        symbolTable.TryDefine(aliasedModule);
                    }
                    else
                    {
                        // Handle non-aliased imports by building nested module structure
                        var parts = importAlias.Name.Split('.');

                        var leafModule = new ModuleSymbol
                        {
                            Name = parts[^1],
                            Kind = SymbolKind.Module,
                            FilePath = moduleInfo.Path,
                            Exports = BuildExportsFor(moduleInfo),
                            FunctionOverloads = BuildFunctionOverloadsFor(moduleInfo),
                            IsErrorRecovery = moduleInfo.IsErrorRecovery,
                            IsNetModule = moduleInfo.IsNetModule,
                            CanonicalModuleName = moduleInfo.CanonicalModuleName,
                            NetNamespaceName = moduleInfo.NetNamespaceName,
                            CSharpNamespace = moduleInfo.CSharpNamespace,
                            Documentation = moduleInfo.Module?.DocString
                                ?? _moduleRegistry?.GetModuleDocumentation(importAlias.Name),
                            NameDeclarationLine = importAlias.LineStart,
                            NameDeclarationColumn = importAlias.ColumnStart
                        };

                        // The structural parents below export exactly one thing — the nested
                        // ModuleSymbol built above — so they hold no extraction copy and need no
                        // ownership substitution; the leaf's exports ride along on that symbol.
                        ModuleSymbol currentModule = leafModule;
                        for (int j = parts.Length - 2; j >= 0; j--)
                        {
                            var parentModule = new ModuleSymbol
                            {
                                Name = parts[j],
                                Kind = SymbolKind.Module,
                                FilePath = "",
                                Exports = new ModuleExports { { currentModule.Name, currentModule } },
                                IsErrorRecovery = moduleInfo.IsErrorRecovery,
                                IsNetModule = moduleInfo.IsNetModule,
                                NameDeclarationLine = importAlias.LineStart,
                                NameDeclarationColumn = importAlias.ColumnStart
                            };
                            currentModule = parentModule;
                        }

                        symbolTable.TryDefine(currentModule);
                    }
                }
            }
            else if (statement.UnwrapDecorated() is FromImportStatement fromImport)
            {
                importCount++;
                _logger.LogDebug($"Processing from-import: from {fromImport.Module} import {string.Join(", ", fromImport.Names.Select(n => n.Name))}");
                var moduleInfo = ResolveFromImport(fromImport, currentDir);
                if (moduleInfo != null)
                {
                    _logger.LogDebug($"  Module resolved: {moduleInfo.Path}");
                    _logger.LogDebug($"  Exported symbols: [{string.Join(", ", moduleInfo.ExportedSymbols.Keys)}]");
                    IReadOnlyDictionary<string, Symbol> reExportedSymbols =
                        (IReadOnlyDictionary<string, Symbol>?)_semanticBinding.GetReExportedSymbols(fromImport)
                        ?? moduleInfo.ExportedSymbols;
                    var sourceModule = moduleInfo.CanonicalModuleName ?? fromImport.Module;

                    if (fromImport.ImportAll)
                    {
                        foreach (var (name, symbol) in reExportedSymbols)
                        {
                            // Only import public symbols (Python convention: no leading underscore)
                            if (name.StartsWith("_"))
                                continue;

                            // Recorded, not reported: a star-imported name that displaces a builtin
                            // is an error only where it is USED (#1324, mirroring C# CS0104).
                            if (BuiltinNameShadowing.ShadowsBuiltin(symbolTable.BuiltinRegistry, name))
                                symbolTable.AmbiguousGlobImports[name] = fromImport.Module;

                            _logger.LogDebug($"  Defining symbol (import *): {name}");
                            var defined = TryDefineFromImport(symbolTable, symbol, name, sourceModule,
                                importedSymbolSources, fromImport, importAlias: null);

                            if (moduleInfo.IsStub)
                                _deferredCycleSymbols.Add(name);

                            // Only register when there are actual overloads; single functions are already in the symbol table via TryDefine
                            if (defined && OverloadsFor(moduleInfo, name) is { Count: > 1 } wildOverloads)
                            {
                                symbolTable.DefineFunctionOverloads(name, wildOverloads);
                            }
                        }
                    }
                    else
                    {
                        foreach (var importAlias in fromImport.Names)
                        {
                            var lookupName = importAlias.Name;
                            var registerName = importAlias.AsName ?? importAlias.Name;

                            // Say it in the file where the rebinding takes effect (#1324). SPY0483
                            // already warns at the DECLARATION, but that is the library's file,
                            // which the consumer may never open — and the consumer is the one whose
                            // `len(xs)` now means something else.
                            if (BuiltinNameShadowing.ShadowsBuiltin(symbolTable.BuiltinRegistry, registerName))
                            {
                                AddWarning(
                                    $"'{registerName}' is a builtin name; this import rebinds it in "
                                    + $"this file, so a bare '{registerName}' here calls "
                                    + $"'{fromImport.Module}.{registerName}' and not the builtin. The "
                                    + $"builtin stays reachable as 'builtins.{registerName}' (add "
                                    + "'import builtins'), or import under an alias to keep both.",
                                    importAlias.LineStart, importAlias.ColumnStart,
                                    code: DiagnosticCodes.Validation.BuiltinRebornByExplicitImport,
                                    span: importAlias.Span);
                            }

                            if (reExportedSymbols.TryGetValue(lookupName, out var symbol))
                            {
                                _logger.LogDebug($"  Defining imported symbol: {lookupName} as {registerName} ({symbol.Kind})");

                                // A name imported out of the builtins module binds the REGISTRY's own
                                // symbol, not the CLR-discovered export that happens to implement it —
                                // identity is what every builtin dispatch decision reads (#1322). See
                                // BuiltinNameShadowing.RegistryBindingFor.
                                var registryBinding = BuiltinNameShadowing.RegistryBindingFor(
                                    symbolTable.BuiltinRegistry, moduleInfo, lookupName);

                                // Functions only, under an alias. Rebinding a builtin TYPE to the
                                // registry's TypeSymbol under a new spelling makes `x: bint` RESOLVE,
                                // and the emitter then maps the type by name to `Bint` — CS0246
                                // behind SPY0908, replacing today's clean SPY0202 "Type 'bint' not
                                // found. Did you mean 'int'?" with an internal error. Measured, not
                                // assumed. The type half would need the name-keyed type paths to
                                // follow the alias (#1383).
                                //
                                // Leaving the binding alone was never the whole answer, though: the
                                // unbound alias stayed pointed at the module's discovered export, so
                                // `bint("42")` emitted `Int(…)` — CS0103 behind SPY0908, the leak
                                // this restriction was supposed to have avoided. SPY0312 refuses the
                                // spelling outright (#1489, owner ruling 2026-08-13), which is why
                                // the restriction below is now belt-and-braces rather than the rule.
                                //
                                // SPY0312 RETIRED (#1527): type aliases are now transparent in
                                // every position, so `from builtins import int as bint` binds
                                // the registry symbol under the alias and bint("42") works.
                                // The FunctionSymbol restriction that was here is removed.

                                if (registryBinding != null)
                                    symbol = registryBinding.Value.Symbol;

                                // Resolve overloads BEFORE cloning so the clone's OriginSymbol
                                // can point into the same object graph the overload list uses —
                                // the shadow check in ResolveImportedFunctionOverload needs
                                // reference identity between the two (#1525).
                                var importedOverloads = registryBinding?.Overloads
                                    ?? OverloadsFor(moduleInfo, lookupName);

                                if (importAlias.AsName != null)
                                {
                                    // The alias binds a different SPELLING of the same builtin, so the
                                    // clone records what it dispatches as — without that, the clone
                                    // reads as a user function shadowing the builtin and the call is
                                    // ranked against the raw overload set (SPY0353, #1383).
                                    symbol = CloneSymbolWithName(symbol, registerName);
                                    if (registryBinding != null)
                                        symbol = symbol with { BuiltinAliasOf = registryBinding.Value.Symbol };

                                    // Stamp OriginSymbol from the overload list so identity holds
                                    // across module-loader vs. module-scope object graphs.
                                    if (symbol is FunctionSymbol clonedFunc && importedOverloads is { Count: > 0 })
                                        symbol = clonedFunc with { OriginSymbol = importedOverloads[0] };
                                }
                                var defined = TryDefineFromImport(symbolTable, symbol, registerName, sourceModule,
                                    importedSymbolSources, fromImport, importAlias);

                                if (moduleInfo.IsStub)
                                    _deferredCycleSymbols.Add(registerName);

                                // Only register when there are actual overloads; single functions are already in the symbol table via TryDefine
                                if (defined && importedOverloads is { Count: > 1 })
                                {
                                    symbolTable.DefineFunctionOverloads(registerName, importedOverloads);
                                }
                            }
                            else if (registerName != lookupName && reExportedSymbols.TryGetValue(registerName, out symbol))
                            {
                                // Fallback: error recovery modules may register symbols under alias name
                                _logger.LogDebug($"  Defining imported symbol (alias fallback): {registerName} ({symbol.Kind})");
                                var defined = TryDefineFromImport(symbolTable, symbol, registerName, sourceModule,
                                    importedSymbolSources, fromImport, importAlias);

                                if (moduleInfo.IsStub)
                                    _deferredCycleSymbols.Add(registerName);

                                // Only register when there are actual overloads; single functions are already in the symbol table via TryDefine
                                if (defined && OverloadsFor(moduleInfo, registerName) is { Count: > 1 } fallbackOverloads)
                                {
                                    symbolTable.DefineFunctionOverloads(registerName, fallbackOverloads);
                                }
                            }
                            else
                            {
                                _logger.LogWarning($"Symbol '{lookupName}' not found in module exports",
                                    fromImport.LineStart, fromImport.ColumnStart);

                                if (moduleInfo.IsStub)
                                    _failedDeferredModules.Add(moduleInfo.Path);
                            }
                        }
                    }
                }
            }
        }

        _logger.LogInfo($"Completed import resolution ({importCount} imports processed)");
    }

    /// <summary>
    /// Resolve an import statement
    /// </summary>
    public List<ModuleInfo?> ResolveImport(ImportStatement importStmt, string? searchPath = null,
        string? currentModulePath = null, CancellationToken cancellationToken = default)
    {
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _cancellationToken = cancellationToken;
        _logger.LogDebug($"Resolving import: {string.Join(", ", importStmt.Names.Select(n => n.Name))}");

        var result = new List<ModuleInfo?>();

        foreach (var importAlias in importStmt.Names)
        {
            // Intercept 'import typing' / 'import dataclasses' — redirect to native Sharpy syntax
            if (importAlias.Name == "typing")
            {
                AddError(
                    Shared.TypingEquivalences.GenericModuleMessage,
                    importAlias.LineStart, importAlias.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypingModuleRedirect,
                    span: importAlias.Span ?? importStmt.Span);

                var errorRecoveryModule = CreateErrorRecoveryModule(
                    importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);
                result.Add(new ModuleInfo
                {
                    Path = $"<error-recovery:{importAlias.Name}>",
                    Module = null!,
                    ExportedSymbols = errorRecoveryModule.Exports,
                    IsErrorRecovery = true
                });
                continue;
            }

            if (importAlias.Name == "dataclasses")
            {
                AddError(
                    Shared.DataclassesEquivalences.GenericModuleMessage,
                    importAlias.LineStart, importAlias.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassesModuleRedirect,
                    span: importAlias.Span ?? importStmt.Span);

                var errorRecoveryModule = CreateErrorRecoveryModule(
                    importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);
                result.Add(new ModuleInfo
                {
                    Path = $"<error-recovery:{importAlias.Name}>",
                    Module = null!,
                    ExportedSymbols = errorRecoveryModule.Exports,
                    IsErrorRecovery = true
                });
                continue;
            }

            // First, try to resolve as .NET assembly module through ModuleRegistry
            var moduleInfo = TryResolveNetModule(importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);

            // Try synthetic modules (e.g., asyncio)
            moduleInfo ??= TryResolveSyntheticModule(importAlias.Name);

            // Track .NET module names for codegen to emit correct using directives
            if (moduleInfo is { IsNetModule: true })
                _semanticBinding.MarkAsNetModule(importAlias.Name, moduleInfo.CSharpNamespace, moduleInfo.CSharpClassName);

            // If not found in .NET assemblies, try .spy file
            if (moduleInfo == null)
            {
                var modulePath = ResolveModulePath(importAlias.Name, searchPath);
                if (modulePath == null)
                {
                    // Mark the module name as a root cause to suppress cascading errors
                    // at the diagnostic level (complements symbol-level IsErrorRecovery)
                    _diagnostics.AddRootCauseError(importAlias.Name,
                        $"Cannot find module '{importAlias.Name}'" + (_currentModulePath != null ? $" (in {Path.GetFileName(_currentModulePath)})" : ""),
                        importAlias.LineStart, importAlias.ColumnStart, _currentModulePath,
                        DiagnosticCodes.Semantic.ModuleNotFound, CompilerPhase.ImportResolution);

                    // Create error recovery module to prevent cascading "undefined identifier" errors
                    // The module symbol will be registered in ResolveAllImports to suppress downstream errors
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);
                    result.Add(new ModuleInfo
                    {
                        Path = $"<error-recovery:{importAlias.Name}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsErrorRecovery = true
                    });
                    continue;
                }

                // Track the dependency (current module depends on imported module)
                // Note: .NET modules are not tracked in the file dependency graph
                if (_dependencyRecorder != null && _currentModulePath != null)
                {
                    _dependencyRecorder.AddDependency(_currentModulePath, modulePath);
                }

                moduleInfo = LoadModule(modulePath, importAlias.LineStart, importAlias.ColumnStart);

                // Plain imports of stub modules can't be deferred
                if (moduleInfo is { IsStub: true })
                    _failedDeferredModules.Add(moduleInfo.Path);
            }

            // Always add to maintain positional alignment with importStmt.Names
            result.Add(moduleInfo);
        }

        return result;
    }

    /// <summary>
    /// Resolve a from-import statement
    /// </summary>
    public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null,
        string? currentModulePath = null, CancellationToken cancellationToken = default)
    {
        if (currentModulePath != null)
            UpdateCurrentModule(currentModulePath);
        _cancellationToken = cancellationToken;
        var importedNames = fromImport.ImportAll ? "*" : string.Join(", ", fromImport.Names.Select(n => n.AsName != null ? $"{n.Name} as {n.AsName}" : n.Name));
        _logger.LogDebug($"[ImportResolver] Resolving from-import: from {fromImport.Module} import {importedNames}");
        if (_currentModulePath != null)
        {
            _logger.LogDebug($"[ImportResolver]   Current module: {Path.GetFileName(_currentModulePath)}");
        }

        // Helpful error for unsupported Python constructs: intercept before module resolution
        // so the error fires even when the module has no remaining exported functions.
        if (fromImport.Module == "collections" && !fromImport.ImportAll)
        {
            foreach (var alias in fromImport.Names)
            {
                if (alias.Name == "namedtuple")
                {
                    AddError(
                        "collections.namedtuple is not supported in Sharpy. " +
                        "Use 'type Point = tuple[x: float, y: float]' for named tuples, " +
                        "or '@dataclass class Point: x: float; y: float' for data classes.",
                        alias.LineStart, alias.ColumnStart,
                        code: DiagnosticCodes.Validation.NamedtupleNotSupported,
                        span: alias.Span ?? fromImport.Span);

                    // Create error recovery module to suppress cascading errors
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);
                    foreach (var importAlias in fromImport.Names)
                    {
                        var targetName = importAlias.AsName ?? importAlias.Name;
                        errorRecoveryModule.Exports.Add(targetName, CreateErrorRecoverySymbol(
                            targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart));
                        _diagnostics.MarkAsRootCause(targetName);
                    }
                    return new ModuleInfo
                    {
                        Path = $"<error-recovery:{fromImport.Module}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsNetModule = false
                    };
                }
            }
        }

        // Intercept 'from __future__ import <feature>' — per-file feature enablement.
        // Import resolution is Pass 1.5 (post-parse), so only semantic/codegen-scoped
        // features can be enabled here; parser-scoped names are rejected. Enabled
        // features are recorded per-file (keyed by the current module path) and unioned
        // into the semantic phase's FeatureFlags for that file only.
        if (fromImport.Module == "__future__")
        {
            var fileKey = _currentModulePath ?? string.Empty;
            var errorRecoveryModule = CreateErrorRecoveryModule(
                fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

            if (fromImport.ImportAll)
            {
                AddError(
                    "'from __future__ import *' is not supported; import specific features by name.",
                    fromImport.LineStart, fromImport.ColumnStart,
                    code: DiagnosticCodes.Semantic.UnknownFutureFeature,
                    span: fromImport.Span);
            }
            else
            {
                foreach (var alias in fromImport.Names)
                {
                    var targetName = alias.AsName ?? alias.Name;
                    errorRecoveryModule.Exports.Add(targetName, CreateErrorRecoverySymbol(
                        targetName, fromImport.Module, alias.LineStart, alias.ColumnStart));
                    _diagnostics.MarkAsRootCause(targetName);

                    if (!Shared.FeatureFlags.KnownFeatures.TryGetValue(alias.Name, out var info))
                    {
                        AddError(
                            $"Unknown feature '{alias.Name}' in 'from __future__ import'. {Shared.FeatureFlags.KnownFeatureListMessage()}",
                            alias.LineStart, alias.ColumnStart,
                            code: DiagnosticCodes.Semantic.UnknownFutureFeature,
                            span: alias.Span ?? fromImport.Span);
                        continue;
                    }

                    if (info.Scope == Shared.FeatureScope.Parser)
                    {
                        AddError(
                            $"feature '{alias.Name}' affects syntax and must be enabled via --enable-feature or <Features>",
                            alias.LineStart, alias.ColumnStart,
                            code: DiagnosticCodes.Semantic.UnknownFutureFeature,
                            span: alias.Span ?? fromImport.Span);
                        continue;
                    }

                    // Semantic/codegen-scoped: enable for this file only.
                    if (!_fileFutureFeatures.TryGetValue(fileKey, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        _fileFutureFeatures[fileKey] = set;
                    }
                    set.Add(alias.Name);
                }
            }

            return new ModuleInfo
            {
                Path = $"<error-recovery:{fromImport.Module}>",
                Module = null!,
                ExportedSymbols = errorRecoveryModule.Exports,
                IsNetModule = false
            };
        }

        // Intercept 'from typing import X' — redirect to native Sharpy syntax
        if (fromImport.Module == "typing")
        {
            if (fromImport.ImportAll)
            {
                AddError(
                    Shared.TypingEquivalences.GenericModuleMessage,
                    fromImport.LineStart, fromImport.ColumnStart,
                    code: DiagnosticCodes.Semantic.TypingModuleRedirect,
                    span: fromImport.Span);
            }
            else
            {
                foreach (var alias in fromImport.Names)
                {
                    AddError(
                        Shared.TypingEquivalences.GetMessage(alias.Name),
                        alias.LineStart, alias.ColumnStart,
                        code: DiagnosticCodes.Semantic.TypingModuleRedirect,
                        span: alias.Span ?? fromImport.Span);
                }
            }

            var errorRecoveryModule = CreateErrorRecoveryModule(
                fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);
            if (!fromImport.ImportAll)
            {
                foreach (var importAlias in fromImport.Names)
                {
                    var targetName = importAlias.AsName ?? importAlias.Name;
                    errorRecoveryModule.Exports.Add(targetName, CreateErrorRecoverySymbol(
                        targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart));
                    _diagnostics.MarkAsRootCause(targetName);
                }
            }
            return new ModuleInfo
            {
                Path = $"<error-recovery:{fromImport.Module}>",
                Module = null!,
                ExportedSymbols = errorRecoveryModule.Exports,
                IsNetModule = false
            };
        }

        // Intercept 'from dataclasses import X' — redirect to native Sharpy syntax
        if (fromImport.Module == "dataclasses")
        {
            if (fromImport.ImportAll)
            {
                AddError(
                    Shared.DataclassesEquivalences.GenericModuleMessage,
                    fromImport.LineStart, fromImport.ColumnStart,
                    code: DiagnosticCodes.Semantic.DataclassesModuleRedirect,
                    span: fromImport.Span);
            }
            else
            {
                foreach (var alias in fromImport.Names)
                {
                    AddError(
                        Shared.DataclassesEquivalences.GetMessage(alias.Name),
                        alias.LineStart, alias.ColumnStart,
                        code: DiagnosticCodes.Semantic.DataclassesModuleRedirect,
                        span: alias.Span ?? fromImport.Span);
                }
            }

            var errorRecoveryModule = CreateErrorRecoveryModule(
                fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);
            if (!fromImport.ImportAll)
            {
                foreach (var importAlias in fromImport.Names)
                {
                    var targetName = importAlias.AsName ?? importAlias.Name;
                    errorRecoveryModule.Exports.Add(targetName, CreateErrorRecoverySymbol(
                        targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart));
                    _diagnostics.MarkAsRootCause(targetName);
                }
            }
            return new ModuleInfo
            {
                Path = $"<error-recovery:{fromImport.Module}>",
                Module = null!,
                ExportedSymbols = errorRecoveryModule.Exports,
                IsNetModule = false
            };
        }

        // First, try to resolve as .NET assembly module
        var moduleInfo = TryResolveNetModule(fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

        // Try synthetic modules (e.g., asyncio)
        moduleInfo ??= TryResolveSyntheticModule(fromImport.Module);

        // Track .NET module names for codegen to emit correct using directives
        if (moduleInfo is { IsNetModule: true })
            _semanticBinding.MarkAsNetModule(fromImport.Module, moduleInfo.CSharpNamespace, moduleInfo.CSharpClassName);

        // If not found in .NET assemblies or synthetic modules, try .spy file
        if (moduleInfo == null)
        {
            var resolution = ResolveModuleWithResult(fromImport.Module, searchPath);
            if (resolution == null)
            {
                _logger.LogDebug($"[ImportResolver]   Module '{fromImport.Module}' not found");

                // Mark the module name as a root cause to suppress cascading errors
                _diagnostics.AddRootCauseError(fromImport.Module,
                    $"Cannot find module '{fromImport.Module}'" + (_currentModulePath != null ? $" (in {Path.GetFileName(_currentModulePath)})" : ""),
                    fromImport.LineStart, fromImport.ColumnStart, _currentModulePath,
                    DiagnosticCodes.Semantic.ModuleNotFound, CompilerPhase.ImportResolution);

                // Create error recovery module with placeholder symbols for each imported name
                // This prevents cascading "undefined identifier" errors in TypeChecker
                if (!fromImport.ImportAll && fromImport.Names.Length > 0)
                {
                    var errorRecoveryModule = CreateErrorRecoveryModule(
                        fromImport.Module, fromImport.LineStart, fromImport.ColumnStart);

                    foreach (var importAlias in fromImport.Names)
                    {
                        // The symbol name is the target (alias if present, otherwise original)
                        // Store by TARGET name (alias if present) since that's how ResolveAllImports looks up symbols
                        var targetName = importAlias.AsName ?? importAlias.Name;
                        var errorSymbol = CreateErrorRecoverySymbol(
                            targetName, fromImport.Module, importAlias.LineStart, importAlias.ColumnStart);
                        errorRecoveryModule.Exports.Add(targetName, errorSymbol);
                        _logger.LogDebug($"[ImportResolver]   Created error recovery symbol: {targetName}");

                        // Also mark each imported symbol name as a root cause
                        // This allows suppression of "undefined identifier" errors even if
                        // symbol-level error recovery doesn't catch them
                        _diagnostics.MarkAsRootCause(targetName);
                    }

                    // Return the error recovery module so symbols get registered
                    return new ModuleInfo
                    {
                        Path = $"<error-recovery:{fromImport.Module}>",
                        Module = null!,
                        ExportedSymbols = errorRecoveryModule.Exports,
                        IsErrorRecovery = true
                    };
                }

                return null;
            }

            _logger.LogDebug($"[ImportResolver]   Resolved to: {resolution.FullPath}");
            _logger.LogDebug($"[ImportResolver]   Canonical name: {resolution.CanonicalModuleName ?? resolution.ModuleName}");

            // Store the resolved module path for code generation
            // For relative imports like ".helpers", this gives the canonical name like "mypackage.helpers"
            var resolvedPath = resolution.CanonicalModuleName ?? resolution.ModuleName;
            _semanticBinding.SetResolvedModulePath(fromImport, resolvedPath);

            // Track the dependency (current module depends on imported module)
            // Note: .NET modules are not tracked in the file dependency graph
            if (_dependencyRecorder != null && _currentModulePath != null)
            {
                _dependencyRecorder.AddDependency(_currentModulePath, resolution.FullPath);
            }

            moduleInfo = LoadModule(resolution.FullPath, fromImport.LineStart, fromImport.ColumnStart);
        }
        else
        {
            _logger.LogDebug($"[ImportResolver]   Resolved as .NET module");
        }

        // Validate imported names and populate re-export information for code generation
        if (moduleInfo != null)
        {
            _logger.LogDebug($"[ImportResolver]   Module loaded, exported symbols: {string.Join(", ", moduleInfo.ExportedSymbols.Keys)}");

            // Initialize the re-exported symbols dictionary for code generation
            var reExportedSymbols = new Dictionary<string, Symbol>();

            if (fromImport.ImportAll)
            {
                // import * - only imports public symbols (no leading underscore)
                // This is handled during symbol table population, not here
                // We just validate the module exists

                // Populate re-export symbols for code generation
                foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                {
                    if (!name.StartsWith("_"))
                    {
                        var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
                        reExportedSymbols[name] = reExportSymbol;

                        if (moduleInfo.IsStub)
                            _deferredCycleSymbols.Add(name);

                        _logger.LogDebug($"[ImportResolver]     Re-exporting (wildcard): {name} ({symbol.Kind})");
                    }
                }
            }
            else
            {
                // Direct imports - validate each name exists and is importable
                foreach (var importAlias in fromImport.Names)
                {
                    var symbolName = importAlias.Name;
                    var targetName = importAlias.AsName ?? importAlias.Name;

                    // For .NET modules, try PascalCase conversion if the exact name isn't found
                    // (e.g., from system import console -> System.Console)
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName) && moduleInfo.IsNetModule)
                    {
                        var pascalName = NameMangler.ToPascalCase(symbolName);
                        if (moduleInfo.ExportedSymbols.ContainsKey(pascalName))
                            symbolName = pascalName;
                    }

                    // Case-insensitive fallback for Python-style names that don't match
                    // PascalCase splitting rules (e.g., "defaultdict" → "DefaultDict")
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName))
                    {
                        var caseMatch = moduleInfo.ExportedSymbols.Keys
                            .FirstOrDefault(k => string.Equals(k, symbolName, StringComparison.OrdinalIgnoreCase));
                        if (caseMatch != null)
                            symbolName = caseMatch;
                    }

                    // Check if symbol exists in the module's exported symbols
                    if (!moduleInfo.ExportedSymbols.ContainsKey(symbolName))
                    {
                        _logger.LogDebug($"[ImportResolver]     Symbol '{symbolName}' NOT FOUND in module exports");

                        if (moduleInfo.IsStub)
                        {
                            _failedDeferredModules.Add(moduleInfo.Path);
                            var stubMsg = $"Circular import detected: cannot import '{symbolName}' from '{fromImport.Module}' " +
                                $"because it is involved in a circular dependency. " +
                                $"Only type declarations (class, struct, interface, enum) can be imported from circular modules.";
                            AddError(stubMsg,
                                importAlias.LineStart, importAlias.ColumnStart,
                                code: DiagnosticCodes.Semantic.CircularImportStubError,
                                span: importAlias.Span ?? fromImport.Span);
                        }
                        else
                        {
                            var importMessage = $"Module '{fromImport.Module}' has no exported symbol '{symbolName}'";
                            var importSuggestion = EditDistance.FindClosestMatch(symbolName, moduleInfo.ExportedSymbols.Keys);
                            if (importSuggestion != null)
                                importMessage += $". Did you mean '{importSuggestion}'?";
                            AddError(importMessage,
                                importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.ImportError,
                                span: importAlias.Span ?? fromImport.Span);
                        }
                        continue;
                    }

                    // Check visibility rules for direct imports
                    if (!IsDirectlyImportable(symbolName))
                    {
                        AddError($"Cannot import private symbol '{symbolName}' from module '{fromImport.Module}'",
                            importAlias.LineStart, importAlias.ColumnStart, code: DiagnosticCodes.Semantic.AccessViolation,
                            span: importAlias.Span ?? fromImport.Span);
                    }

                    // Populate re-export symbols for code generation
                    if (moduleInfo.ExportedSymbols.TryGetValue(symbolName, out var symbol))
                    {
                        var reExportSymbol = CreateReExportSymbol(symbol, fromImport, targetName);
                        reExportedSymbols[targetName] = reExportSymbol;

                        if (moduleInfo.IsStub)
                            _deferredCycleSymbols.Add(targetName);

                        // Log detailed information about re-exported symbols for debugging transitive imports
                        if (symbol is TypeSymbol typeSymbol)
                        {
                            _logger.LogDebug($"[ImportResolver]     Importing type: {symbolName} -> {targetName}, DefiningModule: {typeSymbol.DefiningModule ?? "null"}, IsReExport: {typeSymbol.IsReExport}");
                        }
                        else
                        {
                            _logger.LogDebug($"[ImportResolver]     Importing: {symbolName} -> {targetName} ({symbol.Kind})");
                        }
                    }
                }
            }

            // Store re-exported symbols
            if (reExportedSymbols.Count > 0)
            {
                _logger.LogDebug($"[ImportResolver]   Storing {reExportedSymbols.Count} re-exported symbols");
                _semanticBinding.SetReExportedSymbols(fromImport, reExportedSymbols);
            }
        }

        return moduleInfo;
    }

    /// <summary>
    /// Load and parse a module (delegates to ModuleLoader).
    /// </summary>
    private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
    {
        var previousModulePath = _currentModulePath;
        UpdateCurrentModule(modulePath);

        try
        {
            var moduleInfo = _moduleLoader.LoadModule(modulePath, lineStart, columnStart,
                resolveModuleImports: (module, loadedModuleInfo, searchPath) =>
                {
                    // Extract re-exported symbols from from-imports BEFORE resolving imports.
                    // This ensures ExportedSymbols is populated for transitive resolution.
                    foreach (var statement in module.Body)
                    {
                        if (statement.UnwrapDecorated() is FromImportStatement fromImport)
                        {
                            ExtractReExportedSymbols(fromImport, loadedModuleInfo);
                        }
                    }

                    // Resolve imports to detect transitive circular dependencies
                    ResolveModuleImports(module, searchPath);
                },
                cancellationToken: _cancellationToken);

            // Merge any diagnostics from the module loader
            _diagnostics.Merge(_moduleLoader.Diagnostics);

            return moduleInfo;
        }
        finally
        {
            if (previousModulePath != null)
            {
                UpdateCurrentModule(previousModulePath);
            }
            else
            {
                _currentModulePath = null;
                _moduleLoader.CurrentModulePath = null;
            }
        }
    }

    /// <summary>
    /// Resolve all imports within a module to detect transitive circular dependencies
    /// </summary>
    private void ResolveModuleImports(Module module, string? searchPath)
    {
        foreach (var statement in module.Body)
        {
            switch (statement.UnwrapDecorated())
            {
                case ImportStatement import:
                    ResolveImport(import, searchPath);
                    break;
                case FromImportStatement fromImport:
                    ResolveFromImport(fromImport, searchPath);
                    break;
            }
        }
    }

    /// <summary>
    /// Try to resolve a module from loaded .NET assemblies through ModuleRegistry,
    /// or from standard .NET namespaces (e.g., "system" -> "System").
    /// </summary>
    private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
    {
        if (_moduleRegistry == null)
            return null;

        // Check cache first
        var cacheKey = $".net:{moduleName}";
        var cached = _moduleLoader.GetCachedModule(cacheKey);
        if (cached != null)
            return cached;

        // Check if this is a .NET namespace (e.g., "system" -> "System")
        if (_moduleRegistry.IsNetNamespace(moduleName))
        {
            return ResolveNetNamespaceModule(moduleName, cacheKey);
        }

        // Check if this module is loaded in the registry (for module classes)
        if (!_moduleRegistry.IsModuleLoaded(moduleName))
            return null;

        _logger.LogDebug($"Resolving .NET module: {moduleName}");

        // Get functions, types, and fields from the module
        var functions = _moduleRegistry.GetModuleFunctions(moduleName);
        var types = _moduleRegistry.GetModuleTypes(moduleName);
        var fields = _moduleRegistry.GetModuleFields(moduleName);
        if (functions.Count == 0 && types.Count == 0 && fields.Count == 0)
        {
            _logger.LogWarning($".NET module '{moduleName}' has no exported functions, types, or fields", lineStart ?? 0, columnStart ?? 0);
            return null;
        }

        // Create ModuleInfo for the .NET module
        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!,
            ExportedSymbols = new ModuleExports(),
            IsNetModule = true,
            CanonicalModuleName = moduleName,
            CSharpNamespace = _moduleRegistry.GetModuleCSharpNamespace(moduleName),
            CSharpClassName = _moduleRegistry.GetModuleCSharpClassName(moduleName)
        };

        foreach (var function in functions)
        {
            moduleInfo.ExportedSymbols.Add(function.Name, function);

            if (!moduleInfo.FunctionOverloads.TryGetValue(function.Name, out var overloadList))
            {
                overloadList = new List<FunctionSymbol>();
                moduleInfo.FunctionOverloads[function.Name] = overloadList;
            }
            overloadList.Add(function);
        }

        // Exporting a TypeSymbol files it in the types-only lookup too, so the same-named field
        // added below cannot shadow the type in annotation position (#1092).
        foreach (var type in types)
            moduleInfo.ExportedSymbols.Add(type.Name, type);

        foreach (var (fieldName, fieldType, isConst, clrName) in fields)
        {
            moduleInfo.ExportedSymbols.Add(fieldName, new VariableSymbol
            {
                Name = fieldName,
                Kind = SymbolKind.Variable,
                Type = fieldType,
                IsConstant = isConst,
                IsStatic = true,
                AccessLevel = AccessLevel.Public,
                NameDeclarationLine = null,
                NameDeclarationColumn = null,
                ClrFieldName = clrName
            });
        }

        _moduleLoader.CacheModule(cacheKey, moduleInfo);

        _logger.LogInfo($"Loaded .NET module '{moduleName}' with {functions.Count} functions, {types.Count} types, and {fields.Count} fields");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a .NET namespace as a module (e.g., "system" -> types from System namespace).
    /// </summary>
    private ModuleInfo? ResolveNetNamespaceModule(string moduleName, string cacheKey)
    {
        _logger.LogDebug($"Resolving .NET namespace module: {moduleName}");

        var netNamespace = _moduleRegistry!.GetNetNamespace(moduleName);

        var moduleInfo = new ModuleInfo
        {
            Path = $".net:{moduleName}",
            Module = null!,
            ExportedSymbols = new ModuleExports(),
            IsNetModule = true,
            NetNamespaceName = netNamespace
        };

        var types = _moduleRegistry!.GetNamespaceTypes(moduleName);
        foreach (var typeSymbol in types)
            moduleInfo.ExportedSymbols.Add(typeSymbol.Name, typeSymbol);

        if (_moduleRegistry.IsModuleLoaded(moduleName))
        {
            var functions = _moduleRegistry.GetModuleFunctions(moduleName);
            foreach (var function in functions)
            {
                moduleInfo.ExportedSymbols.Add(function.Name, function);

                if (!moduleInfo.FunctionOverloads.TryGetValue(function.Name, out var overloadList))
                {
                    overloadList = new List<FunctionSymbol>();
                    moduleInfo.FunctionOverloads[function.Name] = overloadList;
                }
                overloadList.Add(function);
            }
        }

        _moduleLoader.CacheModule(cacheKey, moduleInfo);

        _logger.LogInfo($"Loaded .NET namespace '{moduleName}' with {moduleInfo.ExportedSymbols.Count} exports");

        return moduleInfo;
    }

    /// <summary>
    /// Resolve a module name to a file path
    /// </summary>
    private string? ResolveModulePath(string moduleName, string? searchPath = null)
    {
        return ResolveModuleWithResult(moduleName, searchPath)?.FullPath;
    }

    /// <summary>
    /// Resolve a module name and return the full resolution result
    /// </summary>
    private ModuleResolutionResult? ResolveModuleWithResult(string moduleName, string? searchPath = null)
    {
        if (searchPath != null)
        {
            _moduleResolver.AddSearchPath(searchPath);
        }

        return _moduleResolver.Resolve(moduleName);
    }

    /// <summary>
    /// Try to resolve a synthetic (compiler-provided) module by name.
    /// Synthetic modules don't correspond to .spy files or .NET assemblies — they are
    /// built-in modules whose functions map to special codegen patterns.
    /// Currently supports: asyncio (gather → Task.WhenAll, sleep → Task.Delay).
    /// </summary>
    private ModuleInfo? TryResolveSyntheticModule(string moduleName)
    {
        if (moduleName != Shared.SyntheticModuleNames.Asyncio)
            return null;

        var cacheKey = $"synthetic:{moduleName}";
        var cached = _moduleLoader.GetCachedModule(cacheKey);
        if (cached != null)
            return cached;

        _logger.LogDebug($"Resolving synthetic module: {moduleName}");

        var exports = new ModuleExports();

        // asyncio.gather(*tasks) -> Task.WhenAll(tasks)
        // Variadic, accepts Task arguments, returns Task (void result since WhenAll returns Task)
        exports.Add("gather", new FunctionSymbol
        {
            Name = "gather",
            Kind = SymbolKind.Function,
            ReturnType = new TaskType { ResultType = null },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "tasks",
                    Type = new TaskType { ResultType = null },
                    IsVariadic = true
                }
            },
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
            NameDeclarationLine = null,
            NameDeclarationColumn = null
        });

        // asyncio.sleep(seconds) -> Task.Delay(TimeSpan.FromSeconds(seconds))
        // Accepts float (double), returns Task (void)
        exports.Add("sleep", new FunctionSymbol
        {
            Name = "sleep",
            Kind = SymbolKind.Function,
            ReturnType = new TaskType { ResultType = null },
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol
                {
                    Name = "seconds",
                    Type = SemanticType.Float
                }
            },
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
            NameDeclarationLine = null,
            NameDeclarationColumn = null
        });

        var moduleInfo = new ModuleInfo
        {
            Path = $"synthetic:{moduleName}",
            Module = null!,
            ExportedSymbols = exports,
            IsNetModule = true // Treat as non-file module for import resolution
        };

        _moduleLoader.CacheModule(cacheKey, moduleInfo);
        _logger.LogInfo($"Loaded synthetic module '{moduleName}' with {exports.Count} functions");

        return moduleInfo;
    }
}
