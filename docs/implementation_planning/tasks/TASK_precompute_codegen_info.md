# Task List: Pre-compute Code Generation Information (Recommendation #4)

**Version:** 1.0  
**Created:** January 2026  
**Target Timeline:** Before Phase 0.1.15  
**Estimated Total Effort:** Medium (3-5 days)

---

## Overview

This task list implements Recommendation #4 from the Architecture Review: Pre-compute Code Generation Information. The goal is to move name resolution and casing decisions from `RoslynEmitter` (code generation) to semantic analysis, making the emitter stateless and easier to test.

### Current Problem

`RoslynEmitter` tracks 15+ mutable dictionaries/sets for name management:
- `_declaredVariables`, `_variableVersions` (variable redefinition tracking)
- `_constVariables`, `_moduleConstVariables` (constant tracking)
- `_moduleVariables`, `_moduleFieldNames` (module-level variable tracking)
- `_classNames`, `_structNames`, `_stringEnumNames` (type name tracking)
- `_fromImportSymbols`, `_importAliasToOriginal` (import tracking)
- `_interfaceDefinitions`, `_variablesWithExecutionOrderIssues`, etc.

This makes RoslynEmitter hard to test, reason about, and parallelize.

### Solution

Create a `CodeGenInfo` record that attaches to symbols during semantic analysis, containing all pre-computed information needed for code generation:
- C# name (with proper casing)
- Version number (for redeclared variables)
- Whether it's module-level, constant, etc.

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **CodeGenInfo location** | Attached to `Symbol` | Symbols are the natural place for code generation metadata; keeps related data together |
| **Computation phase** | TypeChecker (after NameResolver) | TypeChecker has full symbol table context needed for version tracking |
| **Migration strategy** | Parallel implementation with feature flag | Allows gradual migration without breaking existing tests |
| **Two-way door?** | ✅ Yes | Feature flag allows rollback; CodeGenInfo is additive to existing Symbol classes |

### Future-Proofing

The `CodeGenInfo` class is designed to support future features:
- **Tagged Unions (v0.2.x)**: Add `DiscriminatorValue` field
- **Async/Await**: Add `AsyncStateId` for state machine generation
- **Properties**: Add `PropertyAccessorName` for getter/setter naming
- **LSP/Debugger**: `CodeGenInfo` becomes the bridge between semantic model and generated code

---

## Prerequisites

- [ ] All compiler tests passing (baseline)
- [ ] Working understanding of `Symbol.cs`, `RoslynEmitter.cs`, `NameMangler.cs`
- [ ] Read `architecture_review_and_recommendations.md` Recommendation #4

---

## Phase 1: Create CodeGenInfo Infrastructure (Day 1)

### Task 1.1: Create CodeGenInfo Record

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfo.cs` (NEW)

**Description:** Create the core `CodeGenInfo` record that will hold pre-computed code generation information for symbols.

**Implementation:**

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Information computed during semantic analysis for use during code generation.
/// Attached to symbols after type checking to avoid recomputing names during emission.
/// 
/// This is a TWO-WAY DOOR decision: CodeGenInfo is purely additive and can be
/// removed without affecting other functionality.
/// </summary>
public sealed record CodeGenInfo
{
    /// <summary>
    /// The C# name to use for this symbol (with proper casing applied).
    /// For variables: camelCase (local) or PascalCase (module-level)
    /// For constants: CONSTANT_CASE
    /// For types: PascalCase
    /// For methods: PascalCase
    /// </summary>
    public required string CSharpName { get; init; }
    
    /// <summary>
    /// The original Sharpy name (preserved for diagnostics and debugging).
    /// </summary>
    public required string OriginalName { get; init; }
    
    /// <summary>
    /// For redeclared variables, the version number (0 for first declaration, 1 for first redeclaration, etc.).
    /// This maps to variable names like: x, x_1, x_2, etc.
    /// </summary>
    public int Version { get; init; } = 0;
    
    /// <summary>
    /// If true, this is a module-level variable/constant (becomes a static field in C#).
    /// </summary>
    public bool IsModuleLevel { get; init; }
    
    /// <summary>
    /// If true, use CONSTANT_CASE and emit as `const` in C#.
    /// </summary>
    public bool IsConstant { get; init; }
    
    /// <summary>
    /// If true, this variable should not become a module-level field due to execution order issues.
    /// Example: Variables that depend on runtime values in their initializers.
    /// </summary>
    public bool HasExecutionOrderIssues { get; init; }
    
    /// <summary>
    /// For imported symbols, indicates how the symbol was imported.
    /// </summary>
    public ImportKind ImportKind { get; init; } = ImportKind.None;
    
    /// <summary>
    /// For aliased imports, the original name (e.g., "from config import MAX_VALUE as MAX" → "MAX_VALUE").
    /// </summary>
    public string? OriginalImportName { get; init; }
    
    // ============================================================
    // FUTURE EXTENSIBILITY (for v0.2.x+)
    // These fields are reserved for future features. They are
    // nullable/optional and won't affect current functionality.
    // ============================================================
    
    /// <summary>
    /// Reserved for tagged unions (v0.2.x): The discriminator value for union cases.
    /// </summary>
    public int? UnionDiscriminatorValue { get; init; }
    
    /// <summary>
    /// Reserved for async/await (v0.2.x): The state ID in async state machine.
    /// </summary>
    public int? AsyncStateId { get; init; }
    
    /// <summary>
    /// Reserved for properties: The accessor method name for property getters/setters.
    /// </summary>
    public string? PropertyAccessorName { get; init; }
    
    /// <summary>
    /// Get the versioned C# name (includes version suffix for redeclared variables).
    /// </summary>
    public string GetVersionedCSharpName()
    {
        if (Version == 0)
            return CSharpName;
        return $"{CSharpName}_{Version}";
    }
}

/// <summary>
/// How a symbol was imported into the current module.
/// </summary>
public enum ImportKind
{
    /// <summary>Not imported (defined locally).</summary>
    None,
    
    /// <summary>Imported via "import module" - accessed as module.member.</summary>
    ModuleImport,
    
    /// <summary>Imported via "from module import symbol" - accessed directly.</summary>
    FromImport,
    
    /// <summary>Imported via "from module import symbol as alias" - accessed via alias.</summary>
    FromImportWithAlias
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass (no changes to existing code yet)

**Commit:** `feat(semantic): Add CodeGenInfo record for pre-computed code generation metadata`

---

### Task 1.2: Add CodeGenInfo Property to Symbol Base Class

**File:** `src/Sharpy.Compiler/Semantic/Symbol.cs`

**Description:** Add an optional `CodeGenInfo` property to the `Symbol` base record. This is nullable because CodeGenInfo is computed after initial symbol creation.

**Implementation:**

Add to the `Symbol` base record:

```csharp
public abstract record Symbol
{
    public string Name { get; init; } = string.Empty;
    public SymbolKind Kind { get; init; }
    public AccessLevel AccessLevel { get; init; } = AccessLevel.Public;
    public int? DeclarationLine { get; init; }
    public int? DeclarationColumn { get; init; }
    public bool IsReExport { get; init; }
    public string? OriginalModule { get; init; }
    
    // NEW: Pre-computed code generation information
    /// <summary>
    /// Code generation information computed during semantic analysis.
    /// Null until CodeGenInfo computation pass runs.
    /// </summary>
    public CodeGenInfo? CodeGenInfo { get; set; }
}
```

**Note:** Using `set` instead of `init` allows us to set CodeGenInfo after initial symbol creation, which is necessary because symbols are created during NameResolver but CodeGenInfo is computed during/after TypeChecker.

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit:** `feat(semantic): Add CodeGenInfo property to Symbol base class`

---

### Task 1.3: Create CodeGenInfo Unit Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoTests.cs` (NEW)

**Description:** Create unit tests for the `CodeGenInfo` record to ensure correct behavior.

**Implementation:**

```csharp
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class CodeGenInfoTests
{
    [Fact]
    public void GetVersionedCSharpName_Version0_ReturnsBaseName()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "myVariable",
            OriginalName = "my_variable",
            Version = 0
        };
        
        Assert.Equal("myVariable", info.GetVersionedCSharpName());
    }
    
    [Fact]
    public void GetVersionedCSharpName_Version1_ReturnsNameWithSuffix()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "myVariable",
            OriginalName = "my_variable",
            Version = 1
        };
        
        Assert.Equal("myVariable_1", info.GetVersionedCSharpName());
    }
    
    [Fact]
    public void GetVersionedCSharpName_Version3_ReturnsNameWithSuffix()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "counter",
            OriginalName = "counter",
            Version = 3
        };
        
        Assert.Equal("counter_3", info.GetVersionedCSharpName());
    }
    
    [Fact]
    public void CodeGenInfo_ModuleLevelConstant_HasCorrectFlags()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "MAX_VALUE",
            OriginalName = "MAX_VALUE",
            IsModuleLevel = true,
            IsConstant = true
        };
        
        Assert.True(info.IsModuleLevel);
        Assert.True(info.IsConstant);
        Assert.False(info.HasExecutionOrderIssues);
        Assert.Equal(ImportKind.None, info.ImportKind);
    }
    
    [Fact]
    public void CodeGenInfo_FromImportWithAlias_TracksOriginalName()
    {
        var info = new CodeGenInfo
        {
            CSharpName = "MAX",
            OriginalName = "MAX",
            ImportKind = ImportKind.FromImportWithAlias,
            OriginalImportName = "MAX_VALUE"
        };
        
        Assert.Equal(ImportKind.FromImportWithAlias, info.ImportKind);
        Assert.Equal("MAX_VALUE", info.OriginalImportName);
    }
    
    [Fact]
    public void CodeGenInfo_IsRecord_SupportsWithExpression()
    {
        var original = new CodeGenInfo
        {
            CSharpName = "count",
            OriginalName = "count",
            Version = 0
        };
        
        var redeclared = original with { Version = 1 };
        
        Assert.Equal(0, original.Version);
        Assert.Equal(1, redeclared.Version);
        Assert.Equal("count", redeclared.CSharpName);
    }
}
```

**Verification:**
- [ ] All new tests pass
- [ ] All existing tests still pass

**Commit:** `test(semantic): Add unit tests for CodeGenInfo record`

---

## Phase 2: Create CodeGenInfo Computation Service (Day 1-2)

### Task 2.1: Create CodeGenInfoComputer Class

**File:** `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` (NEW)

**Description:** Create a class that computes `CodeGenInfo` for all symbols in a module. This class uses the existing `NameMangler` to determine C# names.

**Implementation:**

```csharp
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Computes CodeGenInfo for all symbols in a module.
/// This class runs after type checking to populate CodeGenInfo on symbols.
/// 
/// The computation mirrors what RoslynEmitter currently does at emission time,
/// but does it once during semantic analysis instead of dynamically during emission.
/// </summary>
public class CodeGenInfoComputer
{
    private readonly SymbolTable _symbolTable;
    private readonly Dictionary<string, int> _variableVersions = new();
    private readonly HashSet<string> _processedModuleLevelVars = new();
    
    public CodeGenInfoComputer(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }
    
    /// <summary>
    /// Compute CodeGenInfo for all symbols in the module.
    /// </summary>
    public void ComputeForModule(Module module)
    {
        // First pass: Process module-level declarations (top-level statements)
        ProcessModuleLevelDeclarations(module);
        
        // Second pass: Process type declarations (classes, structs, interfaces, enums)
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case ClassDef classDef:
                    ProcessClassDef(classDef);
                    break;
                case StructDef structDef:
                    ProcessStructDef(structDef);
                    break;
                case InterfaceDef interfaceDef:
                    ProcessInterfaceDef(interfaceDef);
                    break;
                case EnumDef enumDef:
                    ProcessEnumDef(enumDef);
                    break;
                case FunctionDef funcDef:
                    ProcessFunctionDef(funcDef, isModuleLevel: true);
                    break;
            }
        }
    }
    
    private void ProcessModuleLevelDeclarations(Module module)
    {
        foreach (var stmt in module.Body)
        {
            switch (stmt)
            {
                case VariableDeclaration varDecl:
                    ProcessModuleLevelVariable(varDecl);
                    break;
                case ConstDeclaration constDecl:
                    ProcessModuleLevelConstant(constDecl);
                    break;
                case ImportStatement import:
                    ProcessImport(import);
                    break;
                case FromImportStatement fromImport:
                    ProcessFromImport(fromImport);
                    break;
            }
        }
    }
    
    private void ProcessModuleLevelVariable(VariableDeclaration varDecl)
    {
        var symbol = _symbolTable.Lookup(varDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            varSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(varDecl.Name),
                OriginalName = varDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = false,
                HasExecutionOrderIssues = HasExecutionOrderIssues(varDecl.Value)
            };
            _processedModuleLevelVars.Add(varDecl.Name);
        }
    }
    
    private void ProcessModuleLevelConstant(ConstDeclaration constDecl)
    {
        var symbol = _symbolTable.Lookup(constDecl.Name);
        if (symbol is VariableSymbol varSymbol)
        {
            varSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToConstantCase(constDecl.Name),
                OriginalName = constDecl.Name,
                Version = 0,
                IsModuleLevel = true,
                IsConstant = true,
                HasExecutionOrderIssues = false // Constants are always compile-time
            };
        }
    }
    
    private void ProcessImport(ImportStatement import)
    {
        var effectiveName = import.Alias ?? import.ModulePath;
        var symbol = _symbolTable.Lookup(effectiveName);
        if (symbol is ModuleSymbol moduleSymbol)
        {
            moduleSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = effectiveName.Replace(".", "_"),
                OriginalName = effectiveName,
                ImportKind = import.Alias != null ? ImportKind.FromImportWithAlias : ImportKind.ModuleImport
            };
        }
    }
    
    private void ProcessFromImport(FromImportStatement fromImport)
    {
        foreach (var imported in fromImport.ImportedNames)
        {
            var effectiveName = imported.Alias ?? imported.Name;
            var symbol = _symbolTable.Lookup(effectiveName);
            if (symbol != null)
            {
                var originalName = imported.Alias != null ? imported.Name : null;
                var csharpName = DetermineCSharpNameForFromImport(imported.Name, symbol);
                
                symbol.CodeGenInfo = new CodeGenInfo
                {
                    CSharpName = csharpName,
                    OriginalName = effectiveName,
                    ImportKind = imported.Alias != null ? ImportKind.FromImportWithAlias : ImportKind.FromImport,
                    OriginalImportName = originalName
                };
            }
        }
    }
    
    private string DetermineCSharpNameForFromImport(string name, Symbol symbol)
    {
        // Use the same logic as RoslynEmitter for from-imports:
        // - ALL_CAPS names (constants) stay as CONSTANT_CASE
        // - Other names become PascalCase
        if (IsConstantCaseName(name))
        {
            return NameMangler.ToConstantCase(name);
        }
        return NameMangler.ToPascalCase(name);
    }
    
    private static bool IsConstantCaseName(string name)
    {
        // A name is considered CONSTANT_CASE if it's all uppercase with underscores
        return name.All(c => char.IsUpper(c) || c == '_' || char.IsDigit(c))
               && name.Any(char.IsUpper);
    }
    
    private void ProcessClassDef(ClassDef classDef)
    {
        var typeSymbol = _symbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(classDef.Name),
                OriginalName = classDef.Name
            };
            
            // Process class members
            ProcessTypeMembers(typeSymbol, classDef.Body);
        }
    }
    
    private void ProcessStructDef(StructDef structDef)
    {
        var typeSymbol = _symbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(structDef.Name),
                OriginalName = structDef.Name
            };
            
            ProcessTypeMembers(typeSymbol, structDef.Body);
        }
    }
    
    private void ProcessInterfaceDef(InterfaceDef interfaceDef)
    {
        var typeSymbol = _symbolTable.Lookup(interfaceDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            // Interfaces preserve their exact name (which should already have I prefix)
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToInterfaceName(interfaceDef.Name),
                OriginalName = interfaceDef.Name
            };
            
            ProcessTypeMembers(typeSymbol, interfaceDef.Members);
        }
    }
    
    private void ProcessEnumDef(EnumDef enumDef)
    {
        var typeSymbol = _symbolTable.Lookup(enumDef.Name) as TypeSymbol;
        if (typeSymbol != null)
        {
            typeSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(enumDef.Name),
                OriginalName = enumDef.Name
            };
            
            // Process enum members (they keep their exact names)
            foreach (var member in enumDef.Members)
            {
                // Enum members are represented as VariableSymbols or similar
                // Their names are preserved as-is in enums
            }
        }
    }
    
    private void ProcessTypeMembers(TypeSymbol typeSymbol, IEnumerable<Statement> body)
    {
        // Reset variable versions for this type scope
        var scopedVersions = new Dictionary<string, int>();
        
        foreach (var stmt in body)
        {
            switch (stmt)
            {
                case FieldDeclaration fieldDecl:
                    ProcessField(typeSymbol, fieldDecl);
                    break;
                case FunctionDef funcDef:
                    ProcessFunctionDef(funcDef, isModuleLevel: false);
                    break;
            }
        }
    }
    
    private void ProcessField(TypeSymbol typeSymbol, FieldDeclaration fieldDecl)
    {
        var fieldSymbol = typeSymbol.Fields.FirstOrDefault(f => f.Name == fieldDecl.Name);
        if (fieldSymbol != null)
        {
            fieldSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToCamelCase(fieldDecl.Name),
                OriginalName = fieldDecl.Name,
                IsModuleLevel = false,
                IsConstant = false
            };
        }
    }
    
    private void ProcessFunctionDef(FunctionDef funcDef, bool isModuleLevel)
    {
        var funcSymbol = _symbolTable.Lookup(funcDef.Name) as FunctionSymbol;
        if (funcSymbol != null)
        {
            funcSymbol.CodeGenInfo = new CodeGenInfo
            {
                CSharpName = NameMangler.ToPascalCase(funcDef.Name),
                OriginalName = funcDef.Name,
                IsModuleLevel = isModuleLevel
            };
            
            // Process parameters
            foreach (var param in funcDef.Parameters)
            {
                var paramSymbol = funcSymbol.Parameters.FirstOrDefault(p => p.Name == param.Name);
                // Note: Parameters don't have CodeGenInfo directly attached,
                // but we could extend this in the future
            }
            
            // Process function body for local variables
            if (funcDef.Body != null)
            {
                var localVersions = new Dictionary<string, int>();
                ProcessFunctionBody(funcDef.Body, localVersions);
            }
        }
    }
    
    private void ProcessFunctionBody(IEnumerable<Statement> body, Dictionary<string, int> localVersions)
    {
        foreach (var stmt in body)
        {
            ProcessStatementForLocalVariables(stmt, localVersions);
        }
    }
    
    private void ProcessStatementForLocalVariables(Statement stmt, Dictionary<string, int> localVersions)
    {
        switch (stmt)
        {
            case VariableDeclaration varDecl:
                ProcessLocalVariable(varDecl, localVersions);
                break;
            case ConstDeclaration constDecl:
                ProcessLocalConstant(constDecl, localVersions);
                break;
            case IfStatement ifStmt:
                ProcessFunctionBody(ifStmt.ThenBranch, localVersions);
                if (ifStmt.ElseBranch != null)
                    ProcessFunctionBody(ifStmt.ElseBranch, localVersions);
                foreach (var elif in ifStmt.ElifBranches)
                    ProcessFunctionBody(elif.Body, localVersions);
                break;
            case WhileStatement whileStmt:
                ProcessFunctionBody(whileStmt.Body, localVersions);
                break;
            case ForStatement forStmt:
                ProcessFunctionBody(forStmt.Body, localVersions);
                break;
            case TryStatement tryStmt:
                ProcessFunctionBody(tryStmt.TryBody, localVersions);
                foreach (var handler in tryStmt.Handlers)
                    ProcessFunctionBody(handler.Body, localVersions);
                if (tryStmt.FinallyBody != null)
                    ProcessFunctionBody(tryStmt.FinallyBody, localVersions);
                break;
        }
    }
    
    private void ProcessLocalVariable(VariableDeclaration varDecl, Dictionary<string, int> localVersions)
    {
        var baseName = NameMangler.ToCamelCase(varDecl.Name);
        
        int version = 0;
        if (localVersions.ContainsKey(baseName))
        {
            version = localVersions[baseName] + 1;
        }
        localVersions[baseName] = version;
        
        // Note: Local variables might not have direct symbol table entries
        // in all cases. This is a simplification - the full implementation
        // needs to track variables at the scope level.
    }
    
    private void ProcessLocalConstant(ConstDeclaration constDecl, Dictionary<string, int> localVersions)
    {
        // Local constants use CONSTANT_CASE
        var constName = NameMangler.ToConstantCase(constDecl.Name);
        
        // Constants typically aren't redeclared, but track just in case
        int version = 0;
        if (localVersions.ContainsKey(constName))
        {
            version = localVersions[constName] + 1;
        }
        localVersions[constName] = version;
    }
    
    private bool HasExecutionOrderIssues(Expression? initializer)
    {
        if (initializer == null)
            return false;
        
        // Check if the initializer contains any non-constant expressions
        // that would cause execution order issues as a static field
        return ContainsRuntimeExpression(initializer);
    }
    
    private bool ContainsRuntimeExpression(Expression expr)
    {
        return expr switch
        {
            IntegerLiteral => false,
            FloatLiteral => false,
            StringLiteral => false,
            BooleanLiteral => false,
            NoneLiteral => false,
            // Function calls, method calls, etc. are runtime
            FunctionCall => true,
            MethodCall => true,
            // Identifier references might be runtime (depends on what they reference)
            Identifier => true, // Conservative: assume runtime
            // Compound expressions - check recursively
            BinaryExpression binExpr => ContainsRuntimeExpression(binExpr.Left) || ContainsRuntimeExpression(binExpr.Right),
            UnaryExpression unaryExpr => ContainsRuntimeExpression(unaryExpr.Operand),
            // Default: assume runtime for safety
            _ => true
        };
    }
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit:** `feat(semantic): Add CodeGenInfoComputer for pre-computing code generation metadata`

---

### Task 2.2: Create CodeGenInfoComputer Unit Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` (NEW)

**Description:** Create unit tests for `CodeGenInfoComputer` to ensure it correctly computes CodeGenInfo for various symbol types.

**Implementation:**

```csharp
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Lexer;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class CodeGenInfoComputerTests
{
    private (Module module, SymbolTable symbolTable) ParseAndResolve(string source)
    {
        var lexer = new Lexer.Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser.Parser(tokens);
        var module = parser.ParseModule();
        
        var symbolTable = new SymbolTable();
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.Resolve(module);
        
        // Run type checker to fully populate symbols
        var typeChecker = new TypeChecker(symbolTable);
        typeChecker.Check(module);
        
        return (module, symbolTable);
    }
    
    [Fact]
    public void ComputeForModule_ModuleLevelVariable_SetsPascalCaseName()
    {
        var source = @"
my_variable: int = 42
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);
        
        computer.ComputeForModule(module);
        
        var symbol = symbolTable.Lookup("my_variable") as VariableSymbol;
        Assert.NotNull(symbol?.CodeGenInfo);
        Assert.Equal("MyVariable", symbol.CodeGenInfo.CSharpName);
        Assert.Equal("my_variable", symbol.CodeGenInfo.OriginalName);
        Assert.True(symbol.CodeGenInfo.IsModuleLevel);
        Assert.False(symbol.CodeGenInfo.IsConstant);
    }
    
    [Fact]
    public void ComputeForModule_ModuleLevelConstant_SetsConstantCaseName()
    {
        var source = @"
const MAX_VALUE: int = 100
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);
        
        computer.ComputeForModule(module);
        
        var symbol = symbolTable.Lookup("MAX_VALUE") as VariableSymbol;
        Assert.NotNull(symbol?.CodeGenInfo);
        Assert.Equal("MAX_VALUE", symbol.CodeGenInfo.CSharpName);
        Assert.True(symbol.CodeGenInfo.IsModuleLevel);
        Assert.True(symbol.CodeGenInfo.IsConstant);
    }
    
    [Fact]
    public void ComputeForModule_ClassDefinition_SetsPascalCaseName()
    {
        var source = @"
class my_class:
    pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);
        
        computer.ComputeForModule(module);
        
        var symbol = symbolTable.Lookup("my_class") as TypeSymbol;
        Assert.NotNull(symbol?.CodeGenInfo);
        Assert.Equal("MyClass", symbol.CodeGenInfo.CSharpName);
    }
    
    [Fact]
    public void ComputeForModule_FunctionDefinition_SetsPascalCaseName()
    {
        var source = @"
def my_function() -> None:
    pass
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);
        
        computer.ComputeForModule(module);
        
        var symbol = symbolTable.Lookup("my_function") as FunctionSymbol;
        Assert.NotNull(symbol?.CodeGenInfo);
        Assert.Equal("MyFunction", symbol.CodeGenInfo.CSharpName);
    }
    
    [Fact]
    public void ComputeForModule_Interface_PreservesExactName()
    {
        var source = @"
interface IMyInterface:
    def do_something(self) -> None: ...
";
        var (module, symbolTable) = ParseAndResolve(source);
        var computer = new CodeGenInfoComputer(symbolTable);
        
        computer.ComputeForModule(module);
        
        var symbol = symbolTable.Lookup("IMyInterface") as TypeSymbol;
        Assert.NotNull(symbol?.CodeGenInfo);
        Assert.Equal("IMyInterface", symbol.CodeGenInfo.CSharpName);
    }
}
```

**Verification:**
- [ ] All new tests pass
- [ ] All existing tests still pass

**Commit:** `test(semantic): Add unit tests for CodeGenInfoComputer`

---

## Phase 3: Integrate CodeGenInfoComputer into Compilation Pipeline (Day 2-3)

### Task 3.1: Add Feature Flag for CodeGenInfo Usage

**File:** `src/Sharpy.Compiler/ProjectConfig.cs`

**Description:** Add a feature flag to enable/disable CodeGenInfo computation and usage. This allows gradual migration and easy rollback.

**Implementation:**

Add to `ProjectConfig.cs`:

```csharp
/// <summary>
/// If true, compute CodeGenInfo during semantic analysis and use it during code generation.
/// This is a migration feature flag - set to true to use the new pre-computed approach.
/// Default is false for backwards compatibility during migration.
/// </summary>
public bool UsePrecomputedCodeGenInfo { get; set; } = false;
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass (flag defaults to false)

**Commit:** `feat(config): Add UsePrecomputedCodeGenInfo feature flag for gradual migration`

---

### Task 3.2: Integrate CodeGenInfoComputer into TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Description:** Call `CodeGenInfoComputer.ComputeForModule()` at the end of type checking when the feature flag is enabled.

**Implementation:**

Find the end of the `Check` method in `TypeChecker.cs` and add:

```csharp
public void Check(Module module, bool computeCodeGenInfo = false)
{
    // ... existing type checking code ...
    
    // At the very end, after all type checking is complete:
    if (computeCodeGenInfo)
    {
        var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable);
        codeGenInfoComputer.ComputeForModule(module);
    }
}
```

**Alternative:** If `TypeChecker.Check` doesn't have access to configuration, pass the flag explicitly or use a static configuration accessor.

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit:** `feat(semantic): Integrate CodeGenInfoComputer into TypeChecker`

---

### Task 3.3: Update Compiler.cs to Pass Feature Flag

**File:** `src/Sharpy.Compiler/Compiler.cs`

**Description:** Update the main compilation flow to pass the feature flag to TypeChecker.

**Implementation:**

Find where `TypeChecker.Check` is called and update to:

```csharp
// Look for the pattern:
typeChecker.Check(module);

// Change to:
typeChecker.Check(module, computeCodeGenInfo: _config.UsePrecomputedCodeGenInfo);
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit:** `feat(compiler): Pass UsePrecomputedCodeGenInfo flag to TypeChecker`

---

## Phase 4: Create Adapter in RoslynEmitter (Day 3)

### Task 4.1: Create GetCSharpName Helper Method in RoslynEmitter

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Description:** Create a helper method that checks for `CodeGenInfo` first, then falls back to existing logic. This allows gradual migration.

**Implementation:**

Add to `RoslynEmitter.cs`:

```csharp
/// <summary>
/// Get the C# name for a symbol, using CodeGenInfo if available.
/// Falls back to existing logic if CodeGenInfo is not computed.
/// </summary>
private string GetCSharpNameForSymbol(Symbol symbol, bool isNewDeclaration = false)
{
    // If CodeGenInfo is computed, use it
    if (symbol.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.GetVersionedCSharpName();
    }
    
    // Fall back to existing logic
    return symbol.Kind switch
    {
        SymbolKind.Variable => GetMangledVariableName(symbol.Name, isNewDeclaration),
        SymbolKind.Function => NameMangler.ToPascalCase(symbol.Name),
        SymbolKind.Type => NameMangler.ToPascalCase(symbol.Name),
        SymbolKind.Module => symbol.Name.Replace(".", "_"),
        _ => symbol.Name
    };
}

/// <summary>
/// Check if a symbol is a module-level constant, using CodeGenInfo if available.
/// </summary>
private bool IsModuleLevelConstant(Symbol symbol)
{
    if (symbol.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.IsModuleLevel && symbol.CodeGenInfo.IsConstant;
    }
    
    // Fall back to existing tracking
    return _moduleConstVariables.Contains(symbol.Name);
}

/// <summary>
/// Check if a symbol is a module-level variable, using CodeGenInfo if available.
/// </summary>
private bool IsModuleLevelVariable(Symbol symbol)
{
    if (symbol.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.IsModuleLevel && !symbol.CodeGenInfo.IsConstant;
    }
    
    // Fall back to existing tracking
    return _moduleVariables.Contains(symbol.Name);
}

/// <summary>
/// Check if a symbol has execution order issues, using CodeGenInfo if available.
/// </summary>
private bool HasExecutionOrderIssues(Symbol symbol)
{
    if (symbol.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.HasExecutionOrderIssues;
    }
    
    // Fall back to existing tracking
    return _variablesWithExecutionOrderIssues.Contains(symbol.Name);
}
```

**Verification:**
- [ ] File compiles without errors
- [ ] All existing tests still pass

**Commit:** `feat(codegen): Add CodeGenInfo-aware helper methods to RoslynEmitter`

---

### Task 4.2: Create Integration Tests for CodeGenInfo Path

**File:** `src/Sharpy.Compiler.Tests/CodeGen/CodeGenInfoIntegrationTests.cs` (NEW)

**Description:** Create integration tests that compile code with `UsePrecomputedCodeGenInfo = true` and verify the output matches the existing behavior.

**Implementation:**

```csharp
using Sharpy.Compiler;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Integration tests that verify CodeGenInfo produces identical output to existing logic.
/// These tests run compilation twice - once with and once without CodeGenInfo - and compare results.
/// </summary>
public class CodeGenInfoIntegrationTests
{
    [Fact]
    public void ModuleLevelVariable_ProducesSameOutput()
    {
        var source = @"
my_variable: int = 42
print(my_variable)
";
        AssertSameOutput(source);
    }
    
    [Fact]
    public void ModuleLevelConstant_ProducesSameOutput()
    {
        var source = @"
const MAX_VALUE: int = 100
print(MAX_VALUE)
";
        AssertSameOutput(source);
    }
    
    [Fact]
    public void ClassDefinition_ProducesSameOutput()
    {
        var source = @"
class my_class:
    x: int
    
    def __init__(self, x: int):
        self.x = x

obj = my_class(42)
print(obj.x)
";
        AssertSameOutput(source);
    }
    
    [Fact]
    public void FunctionDefinition_ProducesSameOutput()
    {
        var source = @"
def add_numbers(a: int, b: int) -> int:
    return a + b

result = add_numbers(1, 2)
print(result)
";
        AssertSameOutput(source);
    }
    
    [Fact]
    public void VariableRedefinition_ProducesSameOutput()
    {
        var source = @"
def test_redefinition() -> None:
    x: int = 1
    x: int = 2  # Redefinition
    x: int = 3  # Another redefinition
    print(x)

test_redefinition()
";
        AssertSameOutput(source);
    }
    
    [Fact]
    public void FromImport_ProducesSameOutput()
    {
        // This test requires a multi-file setup
        // Skip for now, add when ProjectCompilation tests are available
    }
    
    private void AssertSameOutput(string source)
    {
        // Compile without CodeGenInfo
        var configWithout = new ProjectConfig { UsePrecomputedCodeGenInfo = false };
        var outputWithout = CompileToString(source, configWithout);
        
        // Compile with CodeGenInfo
        var configWith = new ProjectConfig { UsePrecomputedCodeGenInfo = true };
        var outputWith = CompileToString(source, configWith);
        
        // The generated C# code should be identical
        Assert.Equal(outputWithout, outputWith);
    }
    
    private string CompileToString(string source, ProjectConfig config)
    {
        var compiler = new Compiler(config);
        var result = compiler.CompileToSource(source);
        return result.GeneratedCSharp;
    }
}
```

**Verification:**
- [ ] All new tests pass
- [ ] All existing tests still pass

**Commit:** `test(codegen): Add integration tests for CodeGenInfo path`

---

## Phase 5: Gradual Migration of RoslynEmitter (Day 4)

### Task 5.1: Migrate Variable Name Resolution to Use CodeGenInfo

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Description:** Update `GetMangledVariableName` and related methods to use `CodeGenInfo` when available, while preserving fallback to existing logic.

**Implementation:**

Replace the `GetMangledVariableName` method with a version that checks CodeGenInfo first:

```csharp
private string GetMangledVariableName(string name, bool isNewDeclaration, Symbol? symbol = null)
{
    // NEW: If we have a symbol with CodeGenInfo, use it
    if (symbol?.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.GetVersionedCSharpName();
    }
    
    // EXISTING: Fall back to current logic for backwards compatibility
    var baseName = NameMangler.ToCamelCase(name);

    // ... rest of existing logic unchanged ...
}
```

**Note:** This requires passing the `Symbol` to `GetMangledVariableName` where available. Update call sites incrementally.

**Verification:**
- [ ] All existing tests still pass
- [ ] CodeGenInfo integration tests pass

**Commit:** `refactor(codegen): Update GetMangledVariableName to use CodeGenInfo when available`

---

### Task 5.2: Update Variable Declaration Emission to Use CodeGenInfo

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`

**Description:** Update the code that emits variable declarations to use CodeGenInfo when available.

**Implementation:**

Find variable declaration emission code (likely in `GenerateStatement` or similar) and update:

```csharp
// Before generating a variable declaration, check for CodeGenInfo
private StatementSyntax GenerateVariableDeclaration(VariableDeclaration decl)
{
    var symbol = _context.LookupSymbol(decl.Name);
    
    string csharpName;
    if (symbol?.CodeGenInfo != null)
    {
        csharpName = symbol.CodeGenInfo.GetVersionedCSharpName();
    }
    else
    {
        // Fall back to existing logic
        csharpName = GetMangledVariableName(decl.Name, isNewDeclaration: true);
        // ... existing tracking logic ...
    }
    
    // ... rest of code generation ...
}
```

**Verification:**
- [ ] All existing tests still pass
- [ ] CodeGenInfo integration tests pass

**Commit:** `refactor(codegen): Update variable declaration emission to use CodeGenInfo`

---

### Task 5.3: Update Module-Level Variable Tracking to Use CodeGenInfo

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

**Description:** Update the module-level variable handling to use CodeGenInfo flags instead of internal tracking sets.

**Implementation:**

Find where `_moduleVariables`, `_moduleConstVariables`, and `_moduleFieldNames` are used and add CodeGenInfo checks:

```csharp
// When checking if something is a module-level variable:
private bool IsModuleLevelVariable(string name)
{
    var symbol = _context.LookupSymbol(name);
    if (symbol?.CodeGenInfo != null)
    {
        return symbol.CodeGenInfo.IsModuleLevel && !symbol.CodeGenInfo.IsConstant;
    }
    
    // Fall back to existing tracking
    return _moduleVariables.Contains(name);
}
```

**Verification:**
- [ ] All existing tests still pass
- [ ] CodeGenInfo integration tests pass

**Commit:** `refactor(codegen): Update module-level variable tracking to use CodeGenInfo`

---

## Phase 6: Verification and Cleanup (Day 4-5)

### Task 6.1: Run Full Test Suite with Feature Flag Enabled

**Description:** Run all compiler tests with `UsePrecomputedCodeGenInfo = true` to ensure complete compatibility.

**Steps:**
1. Temporarily modify test base class to enable the feature flag
2. Run full test suite: `dotnet test --filter "FullyQualifiedName~Sharpy.Compiler"`
3. Document any failures and fix them
4. Restore test base class to original state

**Commands:**
```bash
cd /Users/anton/Documents/github/sharpy
dotnet test src/Sharpy.Compiler.Tests --logger "trx;LogFileName=codegen_info_tests.trx"
```

**Verification:**
- [ ] All tests pass with feature flag enabled
- [ ] Test results saved for reference

**Commit:** `test: Verify all tests pass with UsePrecomputedCodeGenInfo enabled`

---

### Task 6.2: Enable Feature Flag by Default

**File:** `src/Sharpy.Compiler/ProjectConfig.cs`

**Description:** Once all tests pass, change the default value of `UsePrecomputedCodeGenInfo` to `true`.

**Implementation:**

```csharp
/// <summary>
/// If true, compute CodeGenInfo during semantic analysis and use it during code generation.
/// </summary>
public bool UsePrecomputedCodeGenInfo { get; set; } = true; // Changed from false
```

**Verification:**
- [ ] All tests still pass
- [ ] No behavioral changes in output

**Commit:** `feat(config): Enable UsePrecomputedCodeGenInfo by default`

---

### Task 6.3: Add Deprecation Comments to Legacy Tracking Fields

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Description:** Add deprecation comments to the legacy tracking fields that are now superseded by CodeGenInfo. Do NOT remove them yet - they serve as fallback.

**Implementation:**

```csharp
// ============================================================
// LEGACY TRACKING FIELDS
// These fields are maintained for backwards compatibility.
// New code should use Symbol.CodeGenInfo instead.
// TODO: Remove in v0.2.x after confirming CodeGenInfo stability.
// ============================================================

[Obsolete("Use Symbol.CodeGenInfo.Version instead")]
private readonly Dictionary<string, int> _variableVersions = new();

[Obsolete("Use Symbol.CodeGenInfo.IsConstant instead")]
private readonly HashSet<string> _constVariables = new();

// ... etc for all tracking fields ...
```

**Verification:**
- [ ] File compiles without errors (Obsolete attributes generate warnings, not errors)
- [ ] All tests still pass

**Commit:** `docs(codegen): Add deprecation comments to legacy RoslynEmitter tracking fields`

---

### Task 6.4: Update Documentation

**File:** `docs/implementation_planning/architecture_review_and_recommendations.md`

**Description:** Add implementation notes indicating that Recommendation #4 has been implemented.

**Implementation:**

Add to the Recommendation 4 section:

```markdown
---

**Implementation Status:** ✅ Implemented (January 2026)

**Implementation Notes:**
- `CodeGenInfo` record added to `src/Sharpy.Compiler/Semantic/CodeGenInfo.cs`
- `CodeGenInfoComputer` added to compute info during semantic analysis
- Feature flag `UsePrecomputedCodeGenInfo` controls migration (default: true)
- Legacy tracking fields in `RoslynEmitter` marked as obsolete but retained for fallback
- Full test coverage added in `CodeGenInfoTests.cs` and `CodeGenInfoComputerTests.cs`

**Future Work:**
- Remove legacy tracking fields in v0.2.x after extended stability verification
- Extend `CodeGenInfo` for tagged unions, async, and properties as those features are added
```

**Verification:**
- [ ] Documentation updated

**Commit:** `docs: Update architecture review with Recommendation #4 implementation status`

---

## Summary

### Files Created
1. `src/Sharpy.Compiler/Semantic/CodeGenInfo.cs` - Core CodeGenInfo record
2. `src/Sharpy.Compiler/Semantic/CodeGenInfoComputer.cs` - Computation logic
3. `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoTests.cs` - Unit tests
4. `src/Sharpy.Compiler.Tests/Semantic/CodeGenInfoComputerTests.cs` - Computer tests
5. `src/Sharpy.Compiler.Tests/CodeGen/CodeGenInfoIntegrationTests.cs` - Integration tests

### Files Modified
1. `src/Sharpy.Compiler/Semantic/Symbol.cs` - Add CodeGenInfo property
2. `src/Sharpy.Compiler/ProjectConfig.cs` - Add feature flag
3. `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Integrate computer
4. `src/Sharpy.Compiler/Compiler.cs` - Pass feature flag
5. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Add helper methods, deprecation comments
6. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` - Use CodeGenInfo
7. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` - Use CodeGenInfo

### Commits (in order)
1. `feat(semantic): Add CodeGenInfo record for pre-computed code generation metadata`
2. `feat(semantic): Add CodeGenInfo property to Symbol base class`
3. `test(semantic): Add unit tests for CodeGenInfo record`
4. `feat(semantic): Add CodeGenInfoComputer for pre-computing code generation metadata`
5. `test(semantic): Add unit tests for CodeGenInfoComputer`
6. `feat(config): Add UsePrecomputedCodeGenInfo feature flag for gradual migration`
7. `feat(semantic): Integrate CodeGenInfoComputer into TypeChecker`
8. `feat(compiler): Pass UsePrecomputedCodeGenInfo flag to TypeChecker`
9. `feat(codegen): Add CodeGenInfo-aware helper methods to RoslynEmitter`
10. `test(codegen): Add integration tests for CodeGenInfo path`
11. `refactor(codegen): Update GetMangledVariableName to use CodeGenInfo when available`
12. `refactor(codegen): Update variable declaration emission to use CodeGenInfo`
13. `refactor(codegen): Update module-level variable tracking to use CodeGenInfo`
14. `test: Verify all tests pass with UsePrecomputedCodeGenInfo enabled`
15. `feat(config): Enable UsePrecomputedCodeGenInfo by default`
16. `docs(codegen): Add deprecation comments to legacy RoslynEmitter tracking fields`
17. `docs: Update architecture review with Recommendation #4 implementation status`

### Rollback Plan

If issues are discovered after enabling the feature:
1. Set `UsePrecomputedCodeGenInfo = false` in `ProjectConfig.cs`
2. All code paths have fallback logic that uses the legacy tracking
3. No code has been deleted - only deprecated

### Future Work (v0.2.x)

1. Remove legacy tracking fields from `RoslynEmitter` after stability is confirmed
2. Extend `CodeGenInfo` with:
   - `UnionDiscriminatorValue` for tagged unions
   - `AsyncStateId` for async state machines
   - `PropertyAccessorName` for property getter/setter naming
3. Consider making `RoslynEmitter` fully stateless by moving all remaining state to `CodeGenInfo`
