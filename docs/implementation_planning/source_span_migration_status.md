# Source Span Migration Status

This document tracks the progress of implementing TextSpan support across the compiler.

## Completed: Part B Foundation

- [x] `TextSpan` type (`src/Sharpy.Compiler/Text/TextSpan.cs`)
  - Immutable struct for [start, end) character ranges
  - Factory methods: `FromBounds`, `FromLength`
  - Contains/overlap checking: `Contains`, `Overlaps`, `Intersection`
  - Merging: `Union`

- [x] `SourceText` type (`src/Sharpy.Compiler/Text/SourceText.cs`)
  - Immutable source text container
  - O(log n) line number lookups
  - `GetLineAndColumn` for offset-to-line conversion
  - `GetText(TextSpan)` for extracting source ranges

- [x] `ILocatable` interface (`src/Sharpy.Compiler/Text/ILocatable.cs`)
  - Common interface for elements with source locations
  - Single property: `TextSpan? Span { get; }`
  - Implemented by: Token, Node

- [x] Token position tracking (`src/Sharpy.Compiler/Lexer/`)
  - `Token.Position` property (zero-based character offset)
  - `Token.GetSpan()` method returns `TextSpan?`
  - All token creation sites updated to track positions

- [x] Node base class Span property (`src/Sharpy.Compiler/Parser/Ast/Node.cs`)
  - `public TextSpan? Span { get; init; }`
  - Optional, defaults to null for backward compatibility
  - Existing `LineStart`/`ColumnStart`/`LineEnd`/`ColumnEnd` preserved

## Completed: Parser Helper Methods

- [x] `GetSpanFromToken(Token)` - Create span from single token
- [x] `GetSpanFromTokens(Token, Token)` - Create span covering token range
- [x] `CombineSpans(TextSpan?, TextSpan?)` - Merge two spans

## Completed: Node Types with Spans

### Expressions

- [x] Identifier
- [x] IntegerLiteral
- [x] FloatLiteral
- [x] StringLiteral
- [x] BooleanLiteral
- [x] NoneLiteral
- [x] EllipsisLiteral
- [x] FStringLiteral (via string token)

### Collections

- [x] ListLiteral
- [x] DictLiteral
- [x] SetLiteral
- [x] TupleLiteral

### Operators

- [x] BinaryOp (covers both operands)
- [x] UnaryOp (operator to operand)
- [x] ComparisonChain

### Access Expressions

- [x] MemberAccess
- [x] IndexAccess
- [x] SliceAccess
- [x] FunctionCall

### Comprehensions

- [x] ListComprehension
- [x] DictComprehension
- [x] SetComprehension

### Other Expressions

- [x] Parenthesized (within the inner expression span context)
- [x] TernaryExpression (condition to else value)
- [x] LambdaExpression

### Statements

- [x] ExpressionStatement (via expression span)
- [x] Assignment
- [x] AugmentedAssignment
- [x] VariableDeclaration
- [x] ReturnStatement
- [x] IfStatement (including elif branches)
- [x] WhileStatement
- [x] ForStatement
- [x] BreakStatement
- [x] ContinueStatement
- [x] PassStatement
- [x] RaiseStatement
- [x] AssertStatement
- [x] TryStatement (including handlers)
- [x] WithStatement
- [x] MatchStatement

### Definitions

- [x] FunctionDef (including decorators)
- [x] ClassDef
- [x] StructDef
- [x] InterfaceDef
- [x] EnumDef
- [x] PropertyDef
- [x] Parameter

### Imports

- [x] ImportStatement (with aliases)
- [x] FromImportStatement (with aliases)
- [x] ImportAlias

### Type Annotations

- [x] SimpleTypeAnnotation
- [x] GenericTypeAnnotation
- [x] NullableTypeAnnotation
- [x] UnionTypeAnnotation
- [x] FunctionTypeAnnotation
- [x] TupleTypeAnnotation

## Test Coverage

- Unit tests for TextSpan: 18 tests in `TextSpanTests.cs`
- Unit tests for SourceText: 8 tests in `SourceTextTests.cs`
- Lexer position tests: 8 tests in `LexerTests.cs`
- Parser span tests: 22 tests in `ParserSpanTests.cs`
  - Literals: 6 tests
  - Collections: 2 tests
  - Operators: 2 tests
  - Access expressions: 3 tests
  - Statements: 4 tests
  - Definitions: 3 tests
  - Type annotations: 2 tests

## Future Work

1. **Semantic Layer Integration**
   - Update error messages to use spans for precise locations
   - Use spans in DiagnosticBag for LSP-compatible diagnostics

2. **LSP Support (v0.2.x+)**
   - Hover information using spans
   - Go-to-definition with span-based navigation
   - Error squiggles with precise ranges

3. **Debugger Support**
   - PDB generation requires accurate source mapping
   - Spans enable breakpoint positioning
