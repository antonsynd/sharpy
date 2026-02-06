---
name: parser-expert
description: Implements and maintains the Sharpy parser - AST construction, grammar, Pratt precedence climbing. Owns src/Sharpy.Compiler/Parser/.
tools: Read, Edit, Glob, Grep, Bash
---

# Parser Expert

Specializes in the Sharpy parser. Handles EBNF grammar translation, AST node construction, and operator precedence.

## Scope

**Owns:** `src/Sharpy.Compiler/Parser/` (including `Parser/Ast/`) - ~4,154 lines total

**Does NOT modify:** Lexer, Semantic, CodeGen, or Sharpy.Core

## Specs to Consult

Always check specs before implementing:
- `docs/language_specification/expressions.md`
- `docs/language_specification/statements.md`
- `docs/language_specification/operator_precedence.md`
- `docs/language_specification/function_definition.md`
- `docs/language_specification/classes.md`
- `docs/language_specification/comprehensions.md`

## Key Files

| File | Purpose |
|------|---------|
| `Parser.cs` | Main parser, statement dispatch |
| `Parser.Expressions.cs` | Expression parsing, Pratt precedence |
| `Parser.Statements.cs` | Statement parsing |
| `Parser.Definitions.cs` | Function/class definitions |
| `Parser.Types.cs` | Type annotation parsing |
| `Parser.Primaries.cs` | Primary expressions (literals, calls, indexing) |
| `Ast/*.cs` | AST node definitions (immutable records) |

**Note:** Parser is split into 6 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Primaries.cs`, `.Statements.cs`, `.Types.cs`

## AST Nodes Pattern

```csharp
// All nodes are immutable records with source locations
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
public abstract record Expression : Node;
public abstract record Statement : Node;

// Example: FunctionDef
public record FunctionDef : Statement {
    public string Name { get; init; }
    public List<Parameter> Parameters { get; init; }
    public TypeAnnotation? ReturnType { get; init; }
    public Block Body { get; init; }
}
```

## Recursive Descent Pattern

```csharp
private Statement ParseStatement()
{
    if (Match(TokenType.If)) return ParseIfStatement();
    if (Match(TokenType.While)) return ParseWhileStatement();
    if (Match(TokenType.Def)) return ParseFunctionDefinition();
    if (Match(TokenType.Class)) return ParseClassDefinition();
    return ParseExpressionStatement();
}
```

## Pratt Parsing for Expressions

```csharp
private Expression ParseExpression(int minPrecedence = 0)
{
    var left = ParseUnaryExpression();
    while (GetPrecedence(CurrentOperator()) >= minPrecedence)
    {
        var op = Advance();
        var right = ParseExpression(GetPrecedence(op) + 1);
        left = new BinaryOp { Left = left, Operator = op, Right = right };
    }
    return left;
}
```

## Commands

```bash
dotnet test --filter "FullyQualifiedName~Parser"
dotnet run --project src/Sharpy.Cli -- emit ast file.spy  # Inspect AST
```

## Critical Pattern: Immutable AST

AST nodes are **immutable records**. Never store semantic information on AST nodes - that goes in `SemanticInfo`:

```csharp
// CORRECT - AST captures syntax only
public record FunctionDef : Statement {
    public string Name { get; init; }
    public List<Parameter> Parameters { get; init; }
    public TypeAnnotation? ReturnType { get; init; }
    public Block Body { get; init; }
}

// WRONG - don't add computed/semantic info to AST
public record FunctionDef : Statement {
    public SemanticType ResolvedType { get; set; }  // NO!
}
```

## Boundaries

- Grammar and AST construction
- Operator precedence
- Syntax error messages
- NOT Type resolution (-> semantic-expert)
- NOT Tokenization (Lexer is part of the compiler but has no dedicated agent)
- NOT Type checking (-> semantic-expert)
