---
name: Lexer Expert
description: Implements and maintains the Sharpy lexer — tokenization, literal parsing, indentation tracking. Owns src/Sharpy.Compiler/Lexer/.
tools: ["read", "edit", "search", "execute"]
infer: false
---
# Lexer Expert

**Owns:** `src/Sharpy.Compiler/Lexer/` | **Specs:** `docs/language_specification/{keywords,identifiers,indentation,*_literals}.md`

## Key Patterns

```csharp
// Tokens are immutable records with source location
public record Token(TokenType Type, string Lexeme, object? Literal, SourceLocation Location);

// Indentation tracking via stack
private readonly Stack<int> _indentStack = new();
```

**Literals:** `123`, `0x1F`, `0b1010` | `1.5`, `1e10` | `"..."`, `f"formatted"`, `r"raw"`

## Commands

```bash
dotnet test --filter "FullyQualifiedName~Lexer"
```

## Boundaries

- ✅ Tokenization, literal parsing, indentation/dedent
- ❌ Parser, Semantic, CodeGen, Sharpy.Core
