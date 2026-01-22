# Task List: ProjectCompiler Model Integration

**Goal:** Migrate ProjectCompiler to use CompilationUnit and ProjectModel classes instead of separate dictionaries, providing a cleaner data model for incremental compilation.

**Priority:** Low - Improves architecture but not blocking language features.

**Prerequisites:** 
- CompilationUnit implemented (✅ Done)
- ProjectModel implemented (✅ Done)
- DependencyGraph implemented (✅ Done)

**Estimated Total Effort:** 3-5 days

**Related Documents:**
- `architecture_review_and_recommendations.md` - Recommendation 1
- `Model/README.md` - Model namespace documentation

---

## Problem Summary

ProjectCompiler currently uses separate dictionaries:

```csharp
// Current (fragmented)
private Dictionary<string, Module> _parsedModules = new();
private Dictionary<string, CompilationMetrics> _fileMetrics = new();
private List<string> _errors = new();
```

This makes it hard to:
- Track all artifacts for a single file
- Implement incremental compilation
- Support parallel compilation
- Query compilation state

---

## Current State

### What's Done
- `CompilationUnit` class with all artifact storage
- `CompilationUnitFactory` for creating units
- `ProjectModel` class with unit collection
- `DependencyGraph` for build ordering

### What's Remaining
- ProjectCompiler still uses legacy dictionaries
- CompilationUnit not wired into the pipeline
- DependencyGraph not integrated with ProjectCompiler

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Dual-path migration**: Keep legacy dictionaries during transition, populate both
2. **Incremental adoption**: Update one compilation phase at a time

### One-Way Door Decisions (Commit Now)
1. **CompilationUnit as unit of work**: Each file is a CompilationUnit through the pipeline
2. **ProjectModel owns all units**: Single source of truth for project state

---

## Phase 1: Wire CompilationUnit into Parsing (2-4 hours)

### Task 1.1: Create CompilationUnits During Parsing
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Create CompilationUnit instances instead of just storing AST.

```csharp
private Dictionary<string, CompilationUnit> _compilationUnits = new();

private void ParseSourceFiles(ProjectConfig config)
{
    _logger.LogInfo("Phase 1: Parsing source files");
    
    foreach (var sourceFile in GetSourceFiles(config))
    {
        var sourceText = File.ReadAllText(sourceFile);
        var modulePath = GetModulePath(sourceFile, config);
        
        // Create CompilationUnit
        var unit = new CompilationUnit(sourceFile, modulePath, sourceText);
        
        // Parse and attach AST
        var lexer = new Lexer.Lexer(sourceText);
        var tokens = lexer.Tokenize().ToList();
        unit.Tokens = tokens;
        
        if (lexer.Errors.Any())
        {
            foreach (var error in lexer.Errors)
            {
                unit.Diagnostics.AddError(error.Message, error.Line, 0);
            }
            unit.Phase = CompilationPhase.Failed;
        }
        else
        {
            var parser = new Parser.Parser(tokens);
            var ast = parser.ParseModule();
            unit.Ast = ast;
            unit.Phase = CompilationPhase.Parsed;
            
            // Extract imports for later
            unit.Imports = ast.Body.OfType<ImportStatement>().ToList();
            unit.FromImports = ast.Body.OfType<FromImportStatement>().ToList();
        }
        
        _compilationUnits[sourceFile] = unit;
        
        // Keep legacy dictionary for backward compatibility during migration
        if (unit.Ast != null)
        {
            _parsedModules[sourceFile] = unit.Ast;
        }
    }
}
```

**Verification:**
- [ ] CompilationUnits created for all files
- [ ] Existing tests still pass (via legacy dictionaries)

**Commit:** `refactor(project): Create CompilationUnits during parsing`

---

### Task 1.2: Track Compilation Phase
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Update CompilationUnit.Phase as compilation progresses.

After each phase, update the unit's phase:
```csharp
private void CollectTypeDeclarations(...)
{
    foreach (var (file, unit) in _compilationUnits)
    {
        if (unit.Phase == CompilationPhase.Failed) continue;
        
        // ... existing type collection logic ...
        
        unit.Phase = CompilationPhase.NamesResolved;
    }
}
```

**Verification:**
- [ ] Phase tracking works
- [ ] Failed units are skipped

**Commit:** `refactor(project): Track compilation phase in CompilationUnit`

---

## Phase 2: Wire DependencyGraph into Import Resolution (2-4 hours)

### Task 2.1: Build DependencyGraph During Import Resolution
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Use DependencyGraphBuilder to track file dependencies.

```csharp
private DependencyGraph? _dependencyGraph;

private void ResolveImports(ProjectConfig config)
{
    _logger.LogInfo("Phase 4: Resolving imports");
    
    var graphBuilder = new DependencyGraphBuilder();
    
    foreach (var (sourceFile, unit) in _compilationUnits)
    {
        if (unit.Phase == CompilationPhase.Failed) continue;
        
        var importResolver = new ImportResolver(...);
        importResolver.ResolveImports(unit.Ast!, sourceFile);
        
        // Track dependencies
        var dependencies = new HashSet<string>();
        
        // From regular imports
        foreach (var import in unit.Imports)
        {
            var resolvedPath = ResolveImportPath(import, config);
            if (resolvedPath != null)
            {
                dependencies.Add(resolvedPath);
            }
        }
        
        // From from-imports
        foreach (var fromImport in unit.FromImports)
        {
            var resolvedPath = ResolveFromImportPath(fromImport, config);
            if (resolvedPath != null)
            {
                dependencies.Add(resolvedPath);
            }
        }
        
        // Add to graph
        foreach (var dep in dependencies)
        {
            graphBuilder.AddDependency(sourceFile, dep);
        }
        
        // Store on CompilationUnit
        unit.DirectDependencies = dependencies.ToImmutableHashSet();
    }
    
    _dependencyGraph = graphBuilder.Build();
    
    // Check for cycles
    var cycles = _dependencyGraph.DetectCycles();
    if (cycles.Count > 0)
    {
        foreach (var cycle in cycles)
        {
            _errors.Add($"Circular import detected: {string.Join(" -> ", cycle)}");
        }
    }
}
```

**Verification:**
- [ ] DependencyGraph built correctly
- [ ] Circular imports detected
- [ ] CompilationUnit.DirectDependencies populated

**Commit:** `feat(project): Build DependencyGraph during import resolution`

---

### Task 2.2: Use Build Order for Semantic Analysis
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Process files in dependency order for semantic analysis.

```csharp
private void RunSemanticAnalysis(ProjectConfig config)
{
    _logger.LogInfo("Phase 5: Semantic analysis");
    
    // Get build order from dependency graph
    var buildOrder = _dependencyGraph?.GetBuildOrder() 
        ?? _compilationUnits.Keys.ToList();
    
    foreach (var sourceFile in buildOrder)
    {
        if (!_compilationUnits.TryGetValue(sourceFile, out var unit))
            continue;
        if (unit.Phase == CompilationPhase.Failed)
            continue;
        
        _logger.LogDebug($"Type checking {sourceFile}");
        
        var typeChecker = new TypeChecker(_services!);
        typeChecker.CheckModule(unit.Ast!, computeCodeGenInfo: true);
        
        // Store diagnostics
        foreach (var error in typeChecker.Errors)
        {
            unit.Diagnostics.AddError(error.Message, error.Line, error.Column);
        }
        
        unit.Phase = unit.Diagnostics.HasErrors 
            ? CompilationPhase.Failed 
            : CompilationPhase.TypeChecked;
    }
}
```

**Verification:**
- [ ] Files processed in dependency order
- [ ] Dependencies processed before dependents

**Commit:** `refactor(project): Use dependency order for semantic analysis`

---

## Phase 3: Wire CompilationUnit into Code Generation (2-4 hours)

### Task 3.1: Generate C# Per CompilationUnit
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Store generated C# on CompilationUnit.

```csharp
private void GenerateCode(ProjectConfig config)
{
    _logger.LogInfo("Phase 6: Code generation");
    
    foreach (var (sourceFile, unit) in _compilationUnits)
    {
        if (unit.Phase != CompilationPhase.TypeChecked)
            continue;
        
        var context = new CodeGenContext(_symbolTable, ...);
        var emitter = new RoslynEmitter(context);
        
        var csharpCode = emitter.Emit(unit.Ast!);
        unit.GeneratedCSharp = csharpCode;
        unit.Phase = CompilationPhase.CodeGenerated;
        
        _logger.LogDebug($"Generated C# for {sourceFile}: {csharpCode.Length} chars");
    }
}
```

**Verification:**
- [ ] Generated C# stored on CompilationUnit
- [ ] Phase updated correctly

**Commit:** `refactor(project): Store generated C# on CompilationUnit`

---

### Task 3.2: Create ProjectModel for Results
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Build a ProjectModel as the compilation result.

```csharp
public ProjectModel? Compile(ProjectConfig config)
{
    // ... existing compilation phases ...
    
    // Build ProjectModel
    var projectModel = new ProjectModel
    {
        Config = config,
        Units = _compilationUnits.Values.ToList(),
        DependencyGraph = _dependencyGraph,
        GlobalSymbols = _symbolTable,
        BuildOrder = _dependencyGraph?.GetBuildOrder().ToList() 
            ?? _compilationUnits.Keys.ToList()
    };
    
    return projectModel;
}
```

**Verification:**
- [ ] ProjectModel contains all units
- [ ] Dependency graph attached

**Commit:** `feat(project): Return ProjectModel from compilation`

---

## Phase 4: Remove Legacy Dictionaries (1-2 hours)

### Task 4.1: Remove Legacy Module Dictionary
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Remove `_parsedModules` and use `_compilationUnits` throughout.

Find and replace:
```csharp
// OLD
_parsedModules[sourceFile]

// NEW
_compilationUnits[sourceFile].Ast
```

**Verification:**
- [ ] No usages of `_parsedModules`
- [ ] All tests pass

**Commit:** `refactor(project): Remove legacy _parsedModules dictionary`

---

### Task 4.2: Remove Legacy Metrics Dictionary
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Use CompilationUnit.Diagnostics instead of separate metrics.

```csharp
// Metrics can be computed from CompilationUnit
public CompilationMetrics GetMetrics(CompilationUnit unit)
{
    return new CompilationMetrics
    {
        FileName = unit.FilePath,
        ParseTime = ...,
        TypeCheckTime = ...,
        ErrorCount = unit.Diagnostics.ErrorCount,
        WarningCount = unit.Diagnostics.WarningCount
    };
}
```

**Verification:**
- [ ] Metrics still available
- [ ] No legacy dictionary

**Commit:** `refactor(project): Remove legacy _fileMetrics dictionary`

---

### Task 4.3: Consolidate Error Collection
**File:** `src/Sharpy.Compiler/Project/ProjectCompiler.cs`
**Description:** Use CompilationUnit.Diagnostics instead of `_errors` list.

```csharp
// Get all errors across all units
public IReadOnlyList<string> GetAllErrors()
{
    return _compilationUnits.Values
        .SelectMany(u => u.Diagnostics.GetErrors())
        .Select(e => $"{u.FilePath}({e.Line},{e.Column}): {e.Message}")
        .ToList();
}
```

**Verification:**
- [ ] Errors aggregated correctly
- [ ] Error messages include file paths

**Commit:** `refactor(project): Consolidate errors into CompilationUnit.Diagnostics`

---

## Phase 5: Add ProjectModel Query Methods (Optional, 1-2 hours)

### Task 5.1: Add Unit Lookup Methods
**File:** `src/Sharpy.Compiler/Model/ProjectModel.cs`
**Description:** Add helper methods for querying project state.

```csharp
public class ProjectModel
{
    // ... existing properties ...
    
    /// <summary>
    /// Get a compilation unit by file path.
    /// </summary>
    public CompilationUnit? GetUnit(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return Units.FirstOrDefault(u => NormalizePath(u.FilePath) == normalized);
    }
    
    /// <summary>
    /// Get all units that failed compilation.
    /// </summary>
    public IReadOnlyList<CompilationUnit> GetFailedUnits()
    {
        return Units.Where(u => u.Phase == CompilationPhase.Failed).ToList();
    }
    
    /// <summary>
    /// Get all units that completed successfully.
    /// </summary>
    public IReadOnlyList<CompilationUnit> GetSuccessfulUnits()
    {
        return Units.Where(u => u.Phase == CompilationPhase.CodeGenerated).ToList();
    }
    
    /// <summary>
    /// Check if the project has any errors.
    /// </summary>
    public bool HasErrors => Units.Any(u => u.HasErrors);
    
    /// <summary>
    /// Get aggregated diagnostics across all units.
    /// </summary>
    public IReadOnlyList<CompilerDiagnostic> GetAllDiagnostics()
    {
        return Units.SelectMany(u => u.Diagnostics.GetAll()).ToList();
    }
}
```

**Verification:**
- [ ] Query methods work correctly
- [ ] Unit tests added

**Commit:** `feat(model): Add ProjectModel query methods`

---

## Phase 6: Verification (30 minutes)

### Task 6.1: Run Full Test Suite
```bash
dotnet test Sharpy.Compiler.Tests --verbosity minimal
```

**Verification:**
- [ ] All tests pass

---

### Task 6.2: Run Integration Tests
```bash
dotnet test Sharpy.Compiler.Tests --filter "FullyQualifiedName~Integration" --verbosity normal
```

**Verification:**
- [ ] Multi-file compilation works
- [ ] Dependency ordering correct

---

### Task 6.3: Verify Incremental Compilation Foundation
**Description:** Verify that the foundation for incremental compilation is in place.

Check that:
- [ ] CompilationUnit.ContentHash is computed
- [ ] DependencyGraph.GetAffectedFiles works
- [ ] CompilationUnit.IsStale() method works

---

## Summary

After completing these tasks:

1. ✅ ProjectCompiler uses CompilationUnit for all file tracking
2. ✅ DependencyGraph integrated for build ordering
3. ✅ ProjectModel returned as compilation result
4. ✅ Legacy dictionaries removed
5. ✅ Foundation ready for incremental compilation

Benefits:
- Single source of truth for file artifacts
- Clear dependency relationships
- Ready for parallel compilation (process independent units)
- Ready for incremental compilation (check staleness, rebuild affected)
