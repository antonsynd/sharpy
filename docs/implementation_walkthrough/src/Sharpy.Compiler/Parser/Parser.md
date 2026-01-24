# Walkthrough: Parser.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.cs`

---

## 1. Overview

The `Parser` class is the heart of Sharpy's syntax analysis phase. It transforms a flat stream of tokens (produced by the Lexer) into a hierarchical Abstract Syntax Tree (AST) that represents the semantic structure of Sharpy code.

**Key responsibilities:**
- Convert tokens into AST nodes (statements and expressions)
- Enforce Sharpy's grammar rules
- Handle Python-like syntax (indentation, colons, decorators)
- Support operator precedence and associativity
- Track source locations (`TextSpan`) for error reporting and IDE features

**Pipeline position:**
```
Source Code → Lexer → [TOKENS] → Parser → [AST] → Semantic Analysis → Code Generation
```

The parser uses a **recursive descent** approach with **precedence climbing** for expressions, making it both efficient and easy to understand/maintain.
