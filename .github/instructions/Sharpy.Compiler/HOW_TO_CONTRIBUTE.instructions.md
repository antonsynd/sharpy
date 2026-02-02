# Sharpy.Compiler

Core compiler: Lexer → Parser → Semantic → ValidationPipeline → CodeGen. Location: `src/Sharpy.Compiler/`

## Directory Structure

```
Sharpy.Compiler/
├── Lexer/           # Tokenization (Lexer.cs, Token.cs)
├── Parser/          # Recursive descent → AST (Parser.cs, Ast/*.cs)
├── Semantic/        # NameResolver → TypeResolver → TypeChecker
│   └── Validation/  # Pluggable validators (OperatorValidator, etc.)
├── CodeGen/         # RoslynEmitter*.cs (partial classes), TypeMapper.cs, NameMangler.cs
├── Discovery/       # Module imports, caching
├── Analysis/        # Control flow analysis
├── Model/           # Shared data structures
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
NameResolver.ResolveInheritance()   → Pass 2: resolve base classes
TypeResolver.ResolveTypes()         → Pass 3: resolve type annotations
TypeChecker.CheckModule()           → Pass 4: type checking + inference
ValidationPipeline.Validate()       → Pass 5: operators/protocols/access
```

## Validation Pipeline Architecture

After `TypeChecker`, pluggable validators run via `ValidationPipeline`:
- `ModuleLevelValidator` — entry point rules, module-level type annotations
- `OperatorValidator` — binary/unary operator type checking
- `ProtocolValidator` — `__len__`, `__iter__` signature validation
- `AccessValidator` — private member access validation
- `ControlFlowValidator` — unreachable code, missing returns

See `Semantic/Validation/README.md` for the TypeChecker vs ValidationPipeline split.

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
| `CodeGenInfo.cs` | Per-symbol codegen metadata (invocation style, etc.) |
| `RoslynEmitter*.cs` | Partial classes by AST category (Expressions, Statements, etc.) |

## C# 9.0 Constraints

| ✅ Available | ❌ Not Available (C# 10+) |
|-------------|-------------------------|
| Records | File-scoped namespaces |
| Init-only setters | Global usings |
| Target-typed new | Record structs |
| Pattern matching | Required members |
