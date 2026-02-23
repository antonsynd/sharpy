---
applyTo: "src/Sharpy.Compiler/**"
---
# Sharpy.Compiler

Core compiler: Lexer → Parser → Semantic → ValidationPipeline → CodeGen. Location: `src/Sharpy.Compiler/`

## Directory Structure

```
Sharpy.Compiler/
├── Lexer/           # Tokenization (Lexer*.cs — 4 partials, Token.cs)
├── Parser/          # Recursive descent → AST (Parser*.cs — 6 files, Ast/*.cs)
├── Semantic/        # NameResolver → ImportResolver → TypeResolver → TypeChecker (8 partial files)
│   └── Validation/  # Pluggable validators (OperatorValidator, etc.)
├── CodeGen/         # RoslynEmitter*.cs (11 partial files), TypeMapper.cs, NameMangler.cs
├── Discovery/       # CLR type discovery, module imports, caching
├── Analysis/        # Control flow analysis (ControlFlowGraph, BasicBlock)
├── Diagnostics/     # DiagnosticBag, DiagnosticCodes, DiagnosticRenderer
├── Model/           # CompilationUnit, ProjectModel
├── Project/         # ProjectCompiler, SpyProject, DependencyGraph
├── Services/        # CompilerServices, CompilerServicesBuilder
├── Text/            # SourceText, TextSpan, ILocatable
├── Logging/         # Compiler logging infrastructure
├── Utilities/       # Shared utility classes
├── Compiler.cs      # Single-file compilation
└── AssemblyCompiler.cs  # Multi-file projects
```

## Adding a Language Feature

Touch components **in order** (dependencies flow left→right):

1. **Lexer:** `Token.cs` (add `TokenType`), `Lexer.cs` (recognize it)
2. **Parser:** `Parser/Ast/*.cs` (add AST record), `Parser.cs` (parsing rules)
3. **Semantic:** `TypeChecker*.cs` (type rules), add validator if needed
4. **CodeGen:** `RoslynEmitter*.cs` (C# emission via SyntaxFactory)
5. **Tests:** Unit tests per component + `.spy`/`.expected` integration tests

**Before implementing:** Check `docs/language_specification/` for spec compliance.

## Key Design Patterns

**AST nodes are immutable records:**
```csharp
public record FunctionDef : Statement {
    public string Name { get; init; }
    public List<Parameter> Parameters { get; init; }
    // Source location tracked via Node base class
}
```

**Semantic info stored in `SemanticInfo`, never on AST:**
```csharp
// SemanticInfo is the single source of truth for resolved types/symbols
semanticInfo.SetType(expression, resolvedType);
semanticInfo.SetSymbol(name, symbol);
// AST nodes remain immutable throughout compilation
```

**Code generation uses Roslyn `SyntaxFactory` exclusively:**
```csharp
// ✅ Correct — use SyntaxFactory methods
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
return MethodDeclaration(returnType, Identifier("MyMethod"))
    .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
    .WithBody(Block(statements));

// ❌ NEVER use string templating
$"public {returnType} MyMethod() {{ }}"
```

## Semantic Analysis Pipeline

Five-pass architecture (order matters):

```
NameResolver.ResolveDeclarations()  → Pass 1: build symbol table
NameResolver.ResolveInheritance()   → Pass 1b: resolve base classes
ImportResolver                      → Pass 1.5: module imports
TypeResolver.ResolveTypes()         → Pass 2: resolve type annotations
TypeChecker.CheckModule()           → Pass 3: type checking + inference
ValidationPipeline.Validate()       → Pass 4: operators/protocols/access
```

**Materialization points:** After each phase, computed data is frozen from `SemanticBinding` onto `Symbol` properties.

## Validation Pipeline Architecture

After `TypeChecker`, pluggable validators run via `ValidationPipeline`. Validators implement `ISemanticValidator` with an `Order` property (lower runs first):

| Order | Validator | Purpose |
|-------|-----------|---------|
| 50 | `ModuleLevelValidator` | Entry point validation |
| 55 | `NamingConventionValidator` | Naming convention checks |
| 60 | `DecoratorValidator` | Decorator validation |
| 150 | `SignatureValidator` | Dunder method signatures |
| 160 | `EqualityContractValidator` | Equality contract checks |
| 170 | `InterfaceConflictValidator` | Interface conflict detection |
| 250 | `DefaultParameterValidator` | Default parameter validation |
| 400 | `ControlFlowValidator` | CFG-based unreachable code, missing returns |
| 410 | `PropertyValidator` | Property validation |
| 420 | `UnusedVariableValidator` | Unused variable warnings |
| 430 | `UnusedImportValidator` | Unused import warnings |
| 450 | `AccessValidator` | Private/protected member access |
| 460 | `DunderInvocationValidator` | Direct dunder call warnings |
| 500 | `ProtocolValidator`, `OperatorValidator` | Protocol/operator validation |

**Responsibility split:** TypeChecker handles type mismatches and in-progress inference. ValidationPipeline handles self-contained AST analyses. See `Semantic/Validation/README.md`.

## Type Narrowing

`TypeChecker._narrowedTypes` tracks flow-sensitive types:
- `if x is not None:` → narrows `T?` to `T` in branch
- `isinstance(x, SomeClass)` → narrows to `SomeClass`

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

**CRITICAL:** Fix bugs, don't change test expectations. Use `[Fact(Skip = "reason")]` if blocked.

## Debugging

```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect C# output
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Inspect AST
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Inspect tokens
```

## Key Files

| File | Purpose |
|------|---------|
| `TypeMapper.cs` | Sharpy→C# types: `list[T]` → `global::Sharpy.Core.List<T>` |
| `NameMangler.cs` | `snake_case` → `PascalCase`, `__str__` → `ToString()` |
| `SemanticInfo.cs` | Type/symbol annotations (separate from AST) |
| `SemanticBinding.cs` | Computed data, materialized at phase boundaries |
| `CodeGenInfo.cs` | Per-symbol codegen metadata (invocation style, etc.) |
| `RoslynEmitter*.cs` | 11 partial classes by AST category |
| `PrimitiveCatalog.cs` | Source of truth for primitive types and CLR mappings |
| `OperatorRegistry.cs` | Operator type rules |

## C# 9.0 Constraints (Sharpy.Core Only)

`Sharpy.Core` targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0`. `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0` with `LangVersion latest`.

| ✅ C# 9.0 Available | ❌ Not Available (C# 10+) |
|---------------------|-------------------------|
| Records | File-scoped namespaces |
| Init-only setters | Global usings |
| Target-typed new | Record structs |
| Pattern matching | Required members |
