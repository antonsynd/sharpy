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

Touch these components **in order** (dependencies flow left to right):

1. **Lexer:** `Token.cs` (add `TokenType`), `Lexer.cs` (recognize it)
2. **Parser:** `Parser/Ast/*.cs` (add AST node), `Parser.cs` (parsing rules)
3. **Semantic:** `TypeChecker.cs` (type rules), add validator if needed
4. **CodeGen:** `RoslynEmitter*.cs` (C# emission via SyntaxFactory)
5. **Tests:** Unit tests per component + file-based integration tests

## Key Design Patterns

**AST nodes are immutable records:**
```csharp
public record FunctionDef : Statement {
    public string Name { get; init; }
    public List<Parameter> Parameters { get; init; }
    // Source location tracked automatically
}
```

**Semantic info stored in `SemanticInfo`, never on AST:**
```csharp
var semanticInfo = new SemanticInfo();
semanticInfo.SetType(expression, resolvedType);
semanticInfo.SetSymbol(name, symbol);
```

**Code generation uses Roslyn `SyntaxFactory` exclusively:**
```csharp
// ✅ Correct
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
return MethodDeclaration(returnType, Identifier("MyMethod"))
    .WithBody(Block(statements));

// ❌ Never use string templating
$"public {returnType} MyMethod() {{ }}"
```

## Semantic Analysis Pipeline

```
NameResolver.ResolveDeclarations()  → Pass 1: declarations
NameResolver.ResolveInheritance()   → Pass 2: inheritance
TypeResolver.ResolveTypes()         → Pass 3: type annotations
TypeChecker.CheckModule()           → Pass 4: type checking
ValidationPipeline.Validate()       → Pass 5: operator/protocol/access validation
```

## Type Narrowing

`TypeChecker._narrowedTypes` tracks narrowed types:
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

## Key Files to Know

| File | Purpose |
|------|---------|
| `TypeMapper.cs` | Sharpy types → C# types (`list[T]` → `List<T>`) |
| `NameMangler.cs` | `snake_case` → `PascalCase`, `__str__` → `ToString()` |
| `SemanticInfo.cs` | Type/symbol annotations (separate from AST) |
| `CodeGenInfo.cs` | Per-symbol codegen metadata |
