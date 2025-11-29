# Sharpy.Compiler

Core compiler: Lexer → Parser → Semantic → CodeGen. Location: `src/Sharpy.Compiler/`

## Directory Structure

```
Sharpy.Compiler/
├── Lexer/          # Tokenization (Lexer.cs, Token.cs)
├── Parser/         # Recursive descent → AST (Parser.cs, Ast/*.cs)
├── Semantic/       # NameResolver → TypeResolver → TypeChecker
├── CodeGen/        # RoslynEmitter.cs, TypeMapper.cs, NameMangler.cs
├── Discovery/      # Module imports, caching
├── Compiler.cs     # Single-file compilation
└── AssemblyCompiler.cs  # Multi-file projects
```

## Adding a Language Feature

For each new feature, touch these files in order:

1. **Lexer:** `Token.cs` (add TokenType), `Lexer.cs` (recognize it)
2. **Parser:** `Parser.cs` + `Ast/*.cs` (add AST node)
3. **Semantic:** `TypeChecker.cs` (type rules)
4. **CodeGen:** `RoslynEmitter.cs` (C# generation)
5. **Tests:** Add tests for each component

## Key Design Patterns

**AST nodes are immutable records:**
```csharp
public record FunctionDef : Statement { ... }
```

**Semantic info stored separately:**
```csharp
// NOT on AST nodes - use SemanticInfo class
var semanticInfo = new SemanticInfo();
semanticInfo.SetType(expression, resolvedType);
```

**Code generation uses Roslyn SyntaxFactory:**
```csharp
// Never use string templating
return SyntaxFactory.MethodDeclaration(...)
```

## Type Narrowing

`TypeChecker` tracks narrowed types in `_narrowedTypes` for:
- `if x is not None:` → narrows `T?` to `T` in branch
- `isinstance(x, SomeClass)` → narrows to `SomeClass`

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~Semantic"
dotnet test --filter "FullyQualifiedName~CodeGen"
```

**CRITICAL:** Fix bugs, don't change test expectations. Use `[Fact(Skip = "reason")]` if blocked.

## Debugging

```csharp
// Dump AST for debugging
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```
