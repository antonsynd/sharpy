# Source Span Migration Status

This document tracks the progress of implementing TextSpan support across the compiler.

## Completed (Part B Foundation)

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

## In Progress

- [ ] Parser span population (partial)
  - Helper methods added: `GetSpanFromToken`, `GetSpanFromTokens`
  - Currently populating spans for subset of node types

## Node Types with Spans

Expressions:
- [x] Identifier

Literals:
- [ ] IntegerLiteral
- [ ] FloatLiteral
- [ ] StringLiteral
- [ ] BooleanLiteral
- [ ] NoneLiteral
- [ ] EllipsisLiteral
- [ ] FStringLiteral

Collections:
- [ ] ListLiteral
- [ ] DictLiteral
- [ ] SetLiteral
- [ ] TupleLiteral

Operators:
- [ ] BinaryOp
- [ ] UnaryOp
- [ ] ComparisonChain

Access:
- [ ] MemberAccess
- [ ] IndexAccess
- [ ] SliceAccess
- [ ] Call

Comprehensions:
- [ ] ListComprehension
- [ ] DictComprehension
- [ ] SetComprehension
- [ ] GeneratorExpression

Other Expressions:
- [ ] Parenthesized
- [ ] TernaryExpression
- [ ] LambdaExpression
- [ ] AwaitExpression
- [ ] YieldExpression

## Node Types Without Spans (need migration)

Statements:
- [ ] ExpressionStatement
- [ ] Assignment
- [ ] AugmentedAssignment
- [ ] VariableDeclaration
- [ ] ReturnStatement
- [ ] IfStatement
- [ ] WhileStatement
- [ ] ForStatement
- [ ] BreakStatement
- [ ] ContinueStatement
- [ ] PassStatement
- [ ] RaiseStatement
- [ ] AssertStatement
- [ ] TryStatement
- [ ] WithStatement
- [ ] MatchStatement
- [ ] DelStatement

Definitions:
- [ ] FunctionDef
- [ ] ClassDef
- [ ] StructDef
- [ ] InterfaceDef
- [ ] EnumDef
- [ ] PropertyDef
- [ ] EventDef

Imports:
- [ ] ImportStatement
- [ ] FromImportStatement

Types:
- [ ] TypeAnnotation (all variants)

## Test Coverage

- Unit tests for TextSpan: 18 tests in `TextSpanTests.cs`
- Unit tests for SourceText: 8 tests in `SourceTextTests.cs`
- Lexer position tests: 8 tests in `LexerTests.cs`
- Parser span tests: 2 tests in `ParserTests.cs`

## Next Steps

1. Continue adding span population to additional node types
2. Focus on high-value nodes first (error reporting):
   - Identifier (done)
   - Call expressions
   - Member access
   - Assignment targets
3. Add semantic layer span propagation
4. Update error messages to use spans
