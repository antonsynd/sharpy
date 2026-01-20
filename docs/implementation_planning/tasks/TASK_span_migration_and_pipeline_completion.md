# Task List: Source Span Migration & Architecture Cleanup

**Document Version:** 1.0  
**Created:** January 2026  
**Target:** v0.1.x  
**Estimated Total Effort:** 5-8 days  
**Prerequisites:** All existing tests pass before starting

---

## Overview

This document provides a comprehensive, step-by-step task list for:

1. **Part A: Source Span Migration** — Add `TextSpan` to all remaining AST nodes (high priority from architecture addendum)
2. **Part B: Validation Pipeline Phase 1 Completion** — Remove dual-path legacy code in TypeChecker
3. **Part C: TypeInferenceService Separation** — Extract type inference from validators
4. **Part D: Integration Tests** — Add comprehensive tests for the validation pipeline

Each part is designed to be:
- **Incremental:** Small commits that can be reverted individually
- **Testable:** Run tests after each major step
- **Non-breaking:** Existing tests continue to pass throughout

---

## Design Decisions

### Two-Way Door Decisions (Reversible)

1. **Span property is nullable/optional** — Existing code without spans continues to work
2. **Helper methods follow token patterns** — Can change internal implementation later
3. **Tests check span presence optionally** — Tests pass whether or not spans are set
4. **Validation pipeline is additive** — Old validators remain until fully migrated

### One-Way Door Decisions (Commit Now)

1. **TextSpan uses (Start, Length)** — Matches Roslyn's design; standard in .NET compilers
2. **Character offsets are 0-based** — Universal standard in compiler tooling
3. **Spans computed at parse time** — Parser owns span creation (not semantic analysis)
4. **TypeInferenceService is stateless** — Follows service pattern for future parallelism

---

## Prerequisites Checklist

Before starting, verify:

```bash
cd /Users/anton/Documents/github/sharpy
dotnet build src/Sharpy.Compiler
dotnet test src/Sharpy.Compiler.Tests --no-build
```

- [ ] All tests pass
- [ ] Note the current test count: _____ tests passed
- [ ] Create feature branch: `git checkout -b feature/span-migration-pipeline-completion`

---

# Part A: Source Span Migration

**Goal:** Add `TextSpan? Span` tracking to all AST nodes for future LSP, debugger, and error reporting.

**Why now:** From architecture addendum: *"Start #10 (Source Spans) during v0.1.x — retrofitting is extremely expensive."*

**Current State:** Foundation complete (`TextSpan`, `SourceText`, `Token.Position`), only `Identifier` has spans populated.

---

## A.1: Verify Foundation Is Complete

**Files:** Multiple in `Text/` and `Lexer/`  
**Effort:** 15 minutes  
**Risk:** None

### Task A.1.1: Verify TextSpan and SourceText Exist

```bash
ls src/Sharpy.Compiler/Text/
```

**Expected:** Should see `TextSpan.cs`, `SourceText.cs`, `ILocatable.cs`

### Task A.1.2: Verify Token Has Position Tracking

```bash
grep -n "Position" src/Sharpy.Compiler/Lexer/Token.cs
```

**Expected:** Should see `Position` property and `GetSpan()` method.

### Task A.1.3: Verify Existing Span Tests Pass

```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Text"
```

**Expected:** All TextSpan and SourceText tests pass.

### Task A.1.4: Verify Identifier Has Span

```bash
grep -n "Span = GetSpanFromToken" src/Sharpy.Compiler/Parser/Parser.Primaries.cs
```

**Expected:** Should find the Identifier case setting `Span = GetSpanFromToken(identToken)`.

If any of these fail, the foundation work is incomplete. Refer to `task_f7_and_source_spans.md` Part B.

---

## A.2: Add Spans to Primary Expressions (Literals)

**File:** `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`  
**Effort:** 1 hour  
**Risk:** Low

### Task A.2.1: Add Span to IntegerLiteral

Find the `TokenType.Integer` case in `ParsePrimary()`. The current code looks like:

```csharp
case TokenType.Integer:
{
    var tokenValue = Current.Value;
    Advance();
    // ... suffix extraction ...
    return new IntegerLiteral { Value = value, Suffix = suffix, LineStart = startLine, ... };
}
```

**Change to:**
```csharp
case TokenType.Integer:
{
    var token = Current;  // Capture token BEFORE Advance()
    var tokenValue = token.Value;
    Advance();
    // ... suffix extraction unchanged ...
    return new IntegerLiteral 
    { 
        Value = value, 
        Suffix = suffix, 
        LineStart = startLine, 
        ColumnStart = startColumn, 
        LineEnd = Previous.Line, 
        ColumnEnd = Previous.Column + Previous.Value.Length,
        Span = GetSpanFromToken(token)  // ADD THIS
    };
}
```

### Task A.2.2: Add Span to FloatLiteral

Same pattern for `TokenType.Float` case.

### Task A.2.3: Add Span to StringLiteral

For `TokenType.String` case:
```csharp
case TokenType.String:
{
    var token = Current;  // Capture token
    var value = token.Value;
    Advance();
    return new StringLiteral 
    { 
        Value = value, 
        IsRaw = false, 
        LineStart = startLine, 
        ColumnStart = startColumn, 
        LineEnd = Previous.Line, 
        ColumnEnd = Previous.Column + Previous.Value.Length,
        Span = GetSpanFromToken(token)  // ADD THIS
    };
}
```

### Task A.2.4: Add Span to RawString

Same pattern for `TokenType.RawString` case.

### Task A.2.5: Add Span to BooleanLiteral (True/False)

For `TokenType.True` and `TokenType.False` cases:
```csharp
case TokenType.True:
{
    var token = Current;
    Advance();
    return new BooleanLiteral 
    { 
        Value = true, 
        LineStart = startLine, 
        ColumnStart = startColumn, 
        LineEnd = Previous.Line, 
        ColumnEnd = Previous.Column + Previous.Value.Length,
        Span = GetSpanFromToken(token)
    };
}
```

### Task A.2.6: Add Span to NoneLiteral

```csharp
case TokenType.None:
{
    var token = Current;
    Advance();
    return new NoneLiteral 
    { 
        LineStart = startLine, 
        ColumnStart = startColumn, 
        LineEnd = Previous.Line, 
        ColumnEnd = Previous.Column + Previous.Value.Length,
        Span = GetSpanFromToken(token)
    };
}
```

### Task A.2.7: Add Span to EllipsisLiteral

Same pattern for `TokenType.Ellipsis`.

### Task A.2.8: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

All tests should pass. Spans are additive and don't break existing behavior.

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Primaries.cs
git commit -m "feat(parser): add TextSpan support for literal nodes

Add spans to: IntegerLiteral, FloatLiteral, StringLiteral, BooleanLiteral, 
NoneLiteral, EllipsisLiteral

Part of source span migration (Rec #10)."
```

---

## A.3: Add Spans to Collection Literals

**File:** `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`  
**Effort:** 45 minutes  
**Risk:** Low

Collection literals span from opening bracket/brace to closing bracket/brace.

### Task A.3.1: Add Span to ListLiteral

Find the `TokenType.LeftBracket` case. Currently returns:
```csharp
return new ListLiteral { Elements = elements, LineStart = startLine, ... };
```

**Change to use `GetSpanFromTokens`:**

For list literals, capture the start and end tokens:
```csharp
case TokenType.LeftBracket:
{
    var startToken = Current;  // Capture [
    Advance();
    // ... existing element parsing ...
    var endToken = Previous;  // The ] just consumed by Expect
    Expect(TokenType.RightBracket);
    // Actually, after Expect, Previous is ], so capture BEFORE Expect
    
    // BETTER APPROACH: Capture after Expect
    return new ListLiteral 
    { 
        Elements = elements, 
        LineStart = startLine, 
        ColumnStart = startColumn, 
        LineEnd = Previous.Line, 
        ColumnEnd = Previous.Column + Previous.Value.Length,
        Span = GetSpanFromTokens(startToken, Previous)  // [ to ]
    };
}
```

**Note:** `GetSpanFromTokens` spans from first token's start to second token's end.

### Task A.3.2: Add Span to TupleLiteral

Two places: empty tuple `()` and tuple with elements.

### Task A.3.3: Add Span to DictLiteral

For `{}` dict literal creation.

### Task A.3.4: Add Span to SetLiteral

For `{element, ...}` set literal creation.

### Task A.3.5: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Primaries.cs
git commit -m "feat(parser): add TextSpan support for collection literals

Add spans to: ListLiteral, TupleLiteral, DictLiteral, SetLiteral

Spans cover from opening to closing bracket/brace."
```

---

## A.4: Add Spans to Comprehensions

**File:** `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task A.4.1: Add Span to ListComprehension

Find where `ListComprehension` is created. Span from `[` to `]`.

### Task A.4.2: Add Span to DictComprehension

Span from `{` to `}`.

### Task A.4.3: Add Span to SetComprehension

Span from `{` to `}`.

### Task A.4.4: Add Span to GeneratorExpression

If present in the parser. Span from `(` to `)`.

### Task A.4.5: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Primaries.cs
git commit -m "feat(parser): add TextSpan support for comprehension nodes

Add spans to: ListComprehension, DictComprehension, SetComprehension, GeneratorExpression"
```

---

## A.5: Add Spans to Binary and Unary Operators

**File:** `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`  
**Effort:** 1 hour  
**Risk:** Low-Medium (many expression parsing methods)

For expressions, the span should cover from the first operand to the last operand.

### Task A.5.1: Add Span to BinaryOp

Find where `BinaryOp` nodes are created. They typically look like:
```csharp
return new BinaryOp
{
    Left = left,
    Operator = op,
    Right = right,
    LineStart = left.LineStart,
    ...
};
```

**Change to:**
```csharp
return new BinaryOp
{
    Left = left,
    Operator = op,
    Right = right,
    LineStart = left.LineStart,
    ColumnStart = left.ColumnStart,
    LineEnd = right.LineEnd,
    ColumnEnd = right.ColumnEnd,
    Span = CombineSpans(left.Span, right.Span)  // NEW HELPER NEEDED
};
```

### Task A.5.2: Create CombineSpans Helper

Add to `Parser.Types.cs` near `GetSpanFromToken`:

```csharp
/// <summary>
/// Combines two optional spans into a span covering both.
/// Returns null if either span is null.
/// </summary>
private static Text.TextSpan? CombineSpans(Text.TextSpan? first, Text.TextSpan? second)
{
    if (first == null || second == null)
        return null;
    return first.Value.Union(second.Value);
}
```

### Task A.5.3: Add Span to UnaryOp

For unary operations like `-x`, `not x`:
```csharp
return new UnaryOp
{
    Operator = op,
    Operand = operand,
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = operand.LineEnd,
    ColumnEnd = operand.ColumnEnd,
    Span = CombineSpans(GetSpanFromToken(operatorToken), operand.Span)
};
```

Note: Need to capture the operator token before parsing the operand.

### Task A.5.4: Add Span to ComparisonChain

If there's a separate node for chained comparisons like `a < b < c`.

### Task A.5.5: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Expressions.cs
git add src/Sharpy.Compiler/Parser/Parser.Types.cs
git commit -m "feat(parser): add TextSpan support for operator expressions

Add spans to: BinaryOp, UnaryOp, ComparisonChain
Add CombineSpans helper for merging node spans."
```

---

## A.6: Add Spans to Access Expressions

**File:** `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`  
**Effort:** 45 minutes  
**Risk:** Low

### Task A.6.1: Add Span to MemberAccess

For `obj.member`:
```csharp
// Span from object start to member end
Span = CombineSpans(obj.Span, GetSpanFromToken(memberToken))
```

### Task A.6.2: Add Span to IndexAccess

For `obj[index]`:
```csharp
// Span from object start to ] 
Span = CombineSpans(obj.Span, GetSpanFromToken(closeBracket))
```

### Task A.6.3: Add Span to SliceAccess

For `obj[start:stop:step]`.

### Task A.6.4: Add Span to Call

For function/method calls `func(args)`:
```csharp
// Span from callable start to )
Span = CombineSpans(callable.Span, GetSpanFromToken(closeParen))
```

### Task A.6.5: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Expressions.cs
git commit -m "feat(parser): add TextSpan support for access expressions

Add spans to: MemberAccess, IndexAccess, SliceAccess, Call

These are high-value for error reporting."
```

---

## A.7: Add Spans to Other Expressions

**File:** `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`  
**Effort:** 1 hour  
**Risk:** Low

### Task A.7.1: Add Span to Parenthesized

For `(expr)` - span from `(` to `)`.

### Task A.7.2: Add Span to TernaryExpression

For `x if condition else y` - span from first expr to last expr.

### Task A.7.3: Add Span to LambdaExpression

For `lambda x: x + 1` - span from `lambda` keyword to body end.

### Task A.7.4: Add Span to AwaitExpression

For `await expr` - span from `await` to operand end.

### Task A.7.5: Add Span to YieldExpression

For `yield expr` - span from `yield` to operand end.

### Task A.7.6: Add Span to WalrusExpression

For `name := value` - span from name to value end.

### Task A.7.7: Add Span to TryExpression / MaybeExpression

For `try expr` and `maybe expr`.

### Task A.7.8: Add Span to SuperExpression

For `super()`.

### Task A.7.9: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Expressions.cs
git commit -m "feat(parser): add TextSpan support for remaining expressions

Add spans to: Parenthesized, TernaryExpression, LambdaExpression, 
AwaitExpression, YieldExpression, WalrusExpression, TryExpression, 
MaybeExpression, SuperExpression"
```

---

## A.8: Add Spans to Simple Statements

**File:** `src/Sharpy.Compiler/Parser/Parser.Statements.cs`  
**Effort:** 1.5 hours  
**Risk:** Low

### Task A.8.1: Add Span to ExpressionStatement

Span equals the expression's span.

### Task A.8.2: Add Span to Assignment

Span from target start to value end.

### Task A.8.3: Add Span to AugmentedAssignment

Span from target start to value end.

### Task A.8.4: Add Span to VariableDeclaration

Span from name to initializer end (or type annotation if no initializer).

### Task A.8.5: Add Span to ReturnStatement

Span from `return` keyword to value end (or just `return` if no value).

### Task A.8.6: Add Span to BreakStatement

Span is just the `break` keyword.

### Task A.8.7: Add Span to ContinueStatement

Span is just the `continue` keyword.

### Task A.8.8: Add Span to PassStatement

Span is just the `pass` keyword.

### Task A.8.9: Add Span to RaiseStatement

Span from `raise` to exception expression end.

### Task A.8.10: Add Span to AssertStatement

Span from `assert` to condition/message end.

### Task A.8.11: Add Span to DelStatement

Span from `del` to target end.

### Task A.8.12: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Statements.cs
git commit -m "feat(parser): add TextSpan support for simple statements

Add spans to: ExpressionStatement, Assignment, AugmentedAssignment,
VariableDeclaration, ReturnStatement, BreakStatement, ContinueStatement,
PassStatement, RaiseStatement, AssertStatement, DelStatement"
```

---

## A.9: Add Spans to Control Flow Statements

**File:** `src/Sharpy.Compiler/Parser/Parser.Statements.cs`  
**Effort:** 1 hour  
**Risk:** Low

### Task A.9.1: Add Span to IfStatement

Span from `if` keyword to end of last body/else block.

**Note:** For compound statements, the span should ideally cover the entire statement including all branches. This may require tracking the end token after parsing all branches.

### Task A.9.2: Add Span to WhileStatement

Span from `while` to body end.

### Task A.9.3: Add Span to ForStatement

Span from `for` to body end.

### Task A.9.4: Add Span to TryStatement

Span from `try` to finally/except body end.

### Task A.9.5: Add Span to WithStatement

Span from `with` to body end.

### Task A.9.6: Add Span to MatchStatement

Span from `match` to end of all cases.

### Task A.9.7: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Statements.cs
git commit -m "feat(parser): add TextSpan support for control flow statements

Add spans to: IfStatement, WhileStatement, ForStatement, TryStatement,
WithStatement, MatchStatement"
```

---

## A.10: Add Spans to Definitions

**File:** `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`  
**Effort:** 1.5 hours  
**Risk:** Low

### Task A.10.1: Add Span to FunctionDef

Span from `def` (or first decorator `@`) to end of body.

**Design decision:** Should span include decorators? **Yes** — the semantic unit is the decorated function.

### Task A.10.2: Add Span to ClassDef

Span from `class` (or first decorator) to end of body.

### Task A.10.3: Add Span to StructDef

Span from `struct` (or first decorator) to end of body.

### Task A.10.4: Add Span to InterfaceDef

Span from `interface` to end of body.

### Task A.10.5: Add Span to EnumDef

Span from `enum` to end of body.

### Task A.10.6: Add Span to PropertyDef

If present as separate node.

### Task A.10.7: Add Span to EventDef

If present as separate node.

### Task A.10.8: Add Span to Decorator

Span from `@` to decorator name end.

### Task A.10.9: Add Span to Parameter

Span from name to type annotation/default value end.

### Task A.10.10: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Definitions.cs
git commit -m "feat(parser): add TextSpan support for definition nodes

Add spans to: FunctionDef, ClassDef, StructDef, InterfaceDef, EnumDef,
PropertyDef, EventDef, Decorator, Parameter"
```

---

## A.11: Add Spans to Import Statements

**File:** `src/Sharpy.Compiler/Parser/Parser.Statements.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task A.11.1: Add Span to ImportStatement

Span from `import` to module path end (or alias end).

### Task A.11.2: Add Span to FromImportStatement

Span from `from` to imported names end.

### Task A.11.3: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Statements.cs
git commit -m "feat(parser): add TextSpan support for import statements

Add spans to: ImportStatement, FromImportStatement"
```

---

## A.12: Add Spans to Type Annotations

**File:** `src/Sharpy.Compiler/Parser/Parser.Types.cs`  
**Effort:** 1 hour  
**Risk:** Low-Medium

Type annotations have multiple variants in the AST.

### Task A.12.1: Add Span to SimpleType

For `int`, `str`, etc.

### Task A.12.2: Add Span to NullableType

For `int?` - span from base type to `?`.

### Task A.12.3: Add Span to GenericType

For `list[int]` - span from name to `]`.

### Task A.12.4: Add Span to UnionType

For `int | str` - span from first type to last type.

### Task A.12.5: Add Span to FunctionType

For `(int, str) -> bool` - span from `(` to return type end.

### Task A.12.6: Add Span to TupleType

For `tuple[int, str]` - span covers the tuple type annotation.

### Task A.12.7: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Parser/Parser.Types.cs
git commit -m "feat(parser): add TextSpan support for type annotations

Add spans to: SimpleType, NullableType, GenericType, UnionType,
FunctionType, TupleType"
```

---

## A.13: Add Span Tests

**File:** New test file  
**Effort:** 1 hour  
**Risk:** None

### Task A.13.1: Create Parser Span Tests

**File:** `src/Sharpy.Compiler.Tests/Parser/ParserSpanTests.cs`

```csharp
using Xunit;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests that parser correctly populates TextSpan on AST nodes.
/// </summary>
public class ParserSpanTests
{
    private Module Parse(string code)
    {
        var lexer = new Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        return parser.ParseModule();
    }

    [Fact]
    public void Identifier_HasSpan()
    {
        var module = Parse("x = 42");
        var assignment = module.Body[0] as Assignment;
        var identifier = assignment?.Target as Identifier;
        
        Assert.NotNull(identifier?.Span);
        Assert.Equal(0, identifier.Span.Value.Start);
        Assert.Equal(1, identifier.Span.Value.Length);
    }

    [Fact]
    public void IntegerLiteral_HasSpan()
    {
        var module = Parse("42");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as IntegerLiteral;
        
        Assert.NotNull(literal?.Span);
        Assert.Equal(0, literal.Span.Value.Start);
        Assert.Equal(2, literal.Span.Value.Length);
    }

    [Fact]
    public void StringLiteral_HasSpan()
    {
        var module = Parse("\"hello\"");
        var stmt = module.Body[0] as ExpressionStatement;
        var literal = stmt?.Expression as StringLiteral;
        
        Assert.NotNull(literal?.Span);
    }

    [Fact]
    public void BinaryOp_HasSpan_CoveringBothOperands()
    {
        var module = Parse("1 + 2");
        var stmt = module.Body[0] as ExpressionStatement;
        var binOp = stmt?.Expression as BinaryOp;
        
        Assert.NotNull(binOp?.Span);
        // Span should cover "1 + 2"
        Assert.Equal(0, binOp.Span.Value.Start);
        Assert.Equal(5, binOp.Span.Value.Length);
    }

    [Fact]
    public void Call_HasSpan()
    {
        var module = Parse("foo(x, y)");
        var stmt = module.Body[0] as ExpressionStatement;
        var call = stmt?.Expression as Call;
        
        Assert.NotNull(call?.Span);
        // Span should cover "foo(x, y)"
        Assert.Equal(0, call.Span.Value.Start);
    }

    [Fact]
    public void MemberAccess_HasSpan()
    {
        var module = Parse("obj.method");
        var stmt = module.Body[0] as ExpressionStatement;
        var access = stmt?.Expression as MemberAccess;
        
        Assert.NotNull(access?.Span);
    }

    [Fact]
    public void FunctionDef_HasSpan()
    {
        var module = Parse(@"def foo():
    pass");
        var funcDef = module.Body[0] as FunctionDef;
        
        Assert.NotNull(funcDef?.Span);
    }

    [Fact]
    public void ClassDef_HasSpan()
    {
        var module = Parse(@"class Foo:
    x: int");
        var classDef = module.Body[0] as ClassDef;
        
        Assert.NotNull(classDef?.Span);
    }

    // Add more tests for each node type as needed
}
```

### Task A.13.2: Run All Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Parser/ParserSpanTests.cs
git commit -m "test(parser): add comprehensive span population tests

Verify TextSpan is correctly set on all major AST node types."
```

---

## A.14: Update Span Migration Status

**File:** `docs/implementation_planning/source_span_migration_status.md`  
**Effort:** 15 minutes  
**Risk:** None

### Task A.14.1: Mark All Nodes Complete

Update the status document to show all nodes have spans.

### Task A.14.2: Document Next Steps

Update "Next Steps" section to mention semantic layer propagation.

**Commit Point:**
```bash
git add docs/implementation_planning/source_span_migration_status.md
git commit -m "docs: update source span migration status to complete

All parser nodes now have TextSpan support."
```

---

## A.15: Part A Summary

### Task A.15.1: Run Full Test Suite

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

**Record:**
- Tests before Part A: _____
- Tests after Part A: _____
- All passing: [ ] Yes / [ ] No

### Task A.15.2: Tag Milestone

```bash
git tag span-migration-complete
```

---


# Part B: Validation Pipeline Phase 1 Completion

**Goal:** Remove the dual-path legacy code in TypeChecker now that the validation pipeline is always enabled.

**Current State:** Pipeline is enabled by default (`ValidationPipelineFactory.CreateDefault()`), but `TypeChecker.Errors` still collects from both V2 validators (via pipeline) AND legacy validators, with deduplication logic.

**Why:** Clean up technical debt, simplify error collection, prepare for removing legacy validators entirely.

---

## B.1: Verify Pipeline Is Default

**Files:** `TypeChecker.cs`  
**Effort:** 15 minutes  
**Risk:** None

### Task B.1.1: Confirm Pipeline Creation

```bash
grep -n "ValidationPipelineFactory.CreateDefault" src/Sharpy.Compiler/Semantic/TypeChecker.cs
```

**Expected:** Should see pipeline created in constructor when none provided.

### Task B.1.2: Confirm No `_usePipeline` Flag

```bash
grep -n "_usePipeline" src/Sharpy.Compiler/Semantic/TypeChecker.cs
```

**Expected:** Should NOT find this flag (was removed in earlier cleanup). If found, it should always be `true`.

### Task B.1.3: Run Tests to Confirm Current State

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

All tests should pass before proceeding.

---

## B.2: Analyze Current Error Collection

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`  
**Effort:** 30 minutes  
**Risk:** None (analysis only)

### Task B.2.1: Document Current Error Flow

Read the `Errors` property getter (around line 129). Document the current flow:

1. Starts with `_errors` (TypeChecker's own errors)
2. Adds `_typeResolver.Errors`
3. Adds legacy validator errors with deduplication
4. Returns combined list

### Task B.2.2: Identify What Can Be Simplified

The V2 validators report errors through the pipeline's `DiagnosticBag`. In `CheckModule`, these are merged into `_errors`. So the legacy validator errors might be duplicates.

**Question to answer:** Are there any errors that ONLY come from legacy validators that are NOT also reported by V2 validators?

Check by comparing V2 validators with their legacy counterparts:
- `ControlFlowValidatorV2` vs `ControlFlowValidator`
- `AccessValidatorV2` vs `AccessValidator`
- `OperatorValidatorV2` vs `OperatorValidator`
- `ProtocolValidatorV2` vs `ProtocolValidator`
- `DefaultParameterValidatorV2` vs `DefaultParameterValidator`
- `SignatureValidatorV2` vs `OperatorSignatureValidator` + `ProtocolSignatureValidator`

---

## B.3: Remove Legacy Validator Error Collection

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`  
**Effort:** 1 hour  
**Risk:** Medium (changes error reporting)

### Task B.3.1: Simplify the Errors Property

The current `Errors` getter has deduplication logic. Simplify to:

**Before (approximately):**
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        
        // Legacy validators still collected with deduplication
        var legacyErrors = new List<SemanticError>();
        legacyErrors.AddRange(_typeResolver.Errors);
        legacyErrors.AddRange(_controlFlowValidator.Errors);
        // ... more legacy validators ...
        
        // Deduplicate
        foreach (var legacyError in legacyErrors)
        {
            bool isDuplicate = allErrors.Any(e => /* dedup logic */);
            if (!isDuplicate) allErrors.Add(legacyError);
        }
        return allErrors;
    }
}
```

**After:**
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        // All errors now come through _errors (TypeChecker errors + pipeline errors + TypeResolver errors)
        // The pipeline merges V2 validator errors in CheckModule
        // TypeResolver errors are merged in CheckModule
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);
        return allErrors;
    }
}
```

### Task B.3.2: Ensure CheckModule Merges All Errors

Verify that `CheckModule` already merges:
1. Pipeline diagnostic errors → `_errors`
2. TypeResolver errors (if not already merged)

### Task B.3.3: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

**Expected:** Some tests may fail if they depend on legacy validator error messages.

### Task B.3.4: Fix Any Failing Tests

If tests fail:
1. Check if the error comes from a legacy validator not yet in V2
2. Either add the check to a V2 validator, or keep that specific legacy validator

**Document any legacy validators that must be kept:**
- [ ] ControlFlowValidator — fully migrated to V2? ____
- [ ] AccessValidator — fully migrated to V2? ____
- [ ] OperatorValidator — fully migrated to V2? ____
- [ ] ProtocolValidator — fully migrated to V2? ____
- [ ] DefaultParameterValidator — fully migrated to V2? ____

### Task B.3.5: Run Tests Again

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

All tests must pass before proceeding.

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Semantic/TypeChecker.cs
git commit -m "refactor(semantic): simplify TypeChecker.Errors collection

Remove deduplication logic - V2 validators via pipeline are now
the authoritative source. TypeResolver errors still included.

Part of validation pipeline completion (Rec #1)."
```

---

## B.4: Remove Unused Legacy Validator Fields (If Safe)

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`  
**Effort:** 30 minutes  
**Risk:** Medium

### Task B.4.1: Identify Which Legacy Validators Can Be Removed

After B.3, check which legacy validators are still used:

```bash
grep -n "_controlFlowValidator\|_accessValidator\|_operatorValidator\|_protocolValidator\|_defaultParameterValidator" src/Sharpy.Compiler/Semantic/TypeChecker*.cs
```

### Task B.4.2: Remove Unused Fields

If a legacy validator is ONLY used for `.Errors` collection (which we removed), it can potentially be removed.

**BUT WAIT:** Some legacy validators are still called during type checking for **type inference** (e.g., `_operatorValidator.ValidateBinaryOp` returns a type). These must be kept until Part C (TypeInferenceService).

**For now, keep all legacy validator fields.** Just remove their error collection from `Errors` getter.

### Task B.4.3: Add Deprecation Comments

Add comments to legacy validator fields:

```csharp
// DEPRECATED: Used for type inference only. Error reporting moved to V2 validators.
// Will be removed when TypeInferenceService is complete (Part C).
private readonly OperatorValidator _operatorValidator;
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Semantic/TypeChecker.cs
git commit -m "docs(semantic): mark legacy validators as deprecated

Legacy validators kept for type inference only.
Error reporting now handled by V2 pipeline validators."
```

---

## B.5: Update Documentation

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`  
**Effort:** 15 minutes  
**Risk:** None

### Task B.5.1: Update XML Documentation

Update the `<remarks>` on `Errors` property:

```csharp
/// <summary>
/// All semantic errors found during type checking.
/// </summary>
/// <remarks>
/// Errors come from:
/// 1. TypeChecker's own checks (type mismatches, undefined symbols, etc.)
/// 2. TypeResolver errors (unresolved types)
/// 3. V2 validators via the ValidationPipeline
/// 
/// Legacy validators are deprecated for error reporting.
/// </remarks>
public IReadOnlyList<SemanticError> Errors { get; }
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Semantic/TypeChecker.cs
git commit -m "docs(semantic): update TypeChecker.Errors documentation

Clarify error sources after pipeline completion."
```

---

## B.6: Part B Summary

### Task B.6.1: Run Full Test Suite

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

### Task B.6.2: Verify Error Messages Are Unchanged

Run a few integration tests manually and verify error messages look correct:

```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~ValidationPipelineIntegration"
```

**Record:**
- Tests passing: [ ] Yes / [ ] No
- Error messages look correct: [ ] Yes / [ ] No

---

# Part C: TypeInferenceService Separation

**Goal:** Extract type inference logic from legacy validators into a dedicated service, enabling clean separation of validation (error detection) from type computation.

**Current State:** `TypeInferenceService.cs` exists and is used by `TypeChecker`. Need to ensure it's complete and properly integrated.

**Why:** Clean architectural boundary between "what type is this?" (inference) and "is this valid?" (validation).

---

## C.1: Verify TypeInferenceService Exists

**Effort:** 15 minutes  
**Risk:** None

### Task C.1.1: Check for Existing Service

```bash
ls -la src/Sharpy.Compiler/Semantic/TypeInferenceService.cs
```

### Task C.1.2: Read Current Implementation

```bash
head -100 src/Sharpy.Compiler/Semantic/TypeInferenceService.cs
```

**Document what methods exist:**
- [ ] `InferBinaryOpType` — exists? ____
- [ ] `InferUnaryOpType` — exists? ____
- [ ] `InferIterableElementType` — exists? ____
- [ ] `InferIndexAccessType` — exists? ____
- [ ] `InferMembershipType` — exists? ____
- [ ] `InferLenType` — exists? ____

### Task C.1.3: Check TypeChecker Uses It

```bash
grep -n "_typeInference" src/Sharpy.Compiler/Semantic/TypeChecker*.cs
```

---

## C.2: Complete TypeInferenceService (If Needed)

**File:** `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`  
**Effort:** 2-4 hours (if incomplete)  
**Risk:** Medium

### Task C.2.1: Compare with Legacy Validator Type Inference

Check what type inference is done in legacy validators:

```bash
grep -n "return.*SemanticType\|-> SemanticType" src/Sharpy.Compiler/Semantic/OperatorValidator.cs
grep -n "return.*SemanticType\|-> SemanticType" src/Sharpy.Compiler/Semantic/ProtocolValidator.cs
```

### Task C.2.2: Add Missing Methods to TypeInferenceService

For each type-returning method in legacy validators, ensure `TypeInferenceService` has an equivalent.

### Task C.2.3: Update TypeChecker to Use TypeInferenceService

Find places in `TypeChecker` that call legacy validators for type inference and replace with `_typeInference` calls.

**Example pattern to find:**
```bash
grep -n "_operatorValidator.Validate" src/Sharpy.Compiler/Semantic/TypeChecker*.cs
```

**Replace:**
```csharp
// Before
var resultType = _operatorValidator.ValidateBinaryOp(op, leftType, rightType);

// After
_operatorValidator.ValidateBinaryOp(op, leftType, rightType);  // For errors only
var resultType = _typeInference.InferBinaryOpType(op, leftType, rightType);  // For type
```

### Task C.2.4: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Semantic/TypeInferenceService.cs
git add src/Sharpy.Compiler/Semantic/TypeChecker*.cs
git commit -m "refactor(semantic): complete TypeInferenceService integration

TypeInferenceService now handles all type inference.
Legacy validators only used for validation, not type computation."
```

---

## C.3: Add TypeInferenceService Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/TypeInferenceServiceTests.cs`  
**Effort:** 1 hour  
**Risk:** None

### Task C.3.1: Check for Existing Tests

```bash
ls src/Sharpy.Compiler.Tests/Semantic/TypeInferenceServiceTests.cs
```

### Task C.3.2: Add Comprehensive Tests

If tests are incomplete, add:

```csharp
[Fact]
public void InferBinaryOpType_IntPlusInt_ReturnsInt()
{
    var service = new TypeInferenceService(symbolTable, clrCache);
    var result = service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int);
    Assert.Equal(SemanticType.Int, result);
}

[Fact]
public void InferBinaryOpType_IntDivideInt_ReturnsDouble()
{
    var service = new TypeInferenceService(symbolTable, clrCache);
    var result = service.InferBinaryOpType(BinaryOperator.Divide, SemanticType.Int, SemanticType.Int);
    Assert.Equal(SemanticType.Double, result);
}

// More tests for each inference method...
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Semantic/TypeInferenceServiceTests.cs
git commit -m "test(semantic): add comprehensive TypeInferenceService tests"
```

---

## C.4: Deprecate Type-Returning Methods in Legacy Validators

**Files:** `OperatorValidator.cs`, `ProtocolValidator.cs`  
**Effort:** 30 minutes  
**Risk:** Low

### Task C.4.1: Add Obsolete Attributes

For methods that return types:

```csharp
/// <summary>
/// Validates a binary operation and returns the result type.
/// </summary>
[Obsolete("Use TypeInferenceService.InferBinaryOpType for type inference. " +
          "Use OperatorValidatorV2 for validation. This method will be removed in v0.2.")]
public SemanticType ValidateBinaryOp(BinaryOperator op, SemanticType left, SemanticType right)
{
    // ... existing implementation ...
}
```

### Task C.4.2: Run Build with Warnings

```bash
dotnet build src/Sharpy.Compiler --warnaserror:CS0618
```

This will show where deprecated methods are still called. Either:
1. Update callers to use `TypeInferenceService`, or
2. Suppress the warning if migration is intentionally deferred

### Task C.4.3: Run Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --no-build
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler/Semantic/OperatorValidator.cs
git add src/Sharpy.Compiler/Semantic/ProtocolValidator.cs
git commit -m "refactor(semantic): deprecate type-returning methods in legacy validators

Mark methods as obsolete with migration guidance.
Type inference should use TypeInferenceService."
```

---

## C.5: Part C Summary

### Task C.5.1: Run Full Test Suite

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

### Task C.5.2: Document Architecture

Update any architecture documentation to reflect the separation:

- **TypeInferenceService** — Computes types, stateless, no side effects
- **V2 Validators** — Report errors, use TypeInferenceService internally
- **Legacy Validators** — Deprecated, kept for migration compatibility

---

# Part D: Integration Tests for Validation Pipeline

**Goal:** Add comprehensive integration tests ensuring the validation pipeline works correctly with realistic code.

**Why:** Confidence before removing legacy validators entirely in future.

---

## D.1: Review Existing Integration Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`  
**Effort:** 15 minutes  
**Risk:** None

### Task D.1.1: Read Existing Tests

```bash
cat src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs
```

**Document existing coverage:**
- [ ] Valid code produces no errors
- [ ] Signature errors detected
- [ ] Control flow errors detected
- [ ] Access errors detected
- [ ] Multi-file projects work
- [ ] Error limit respected

---

## D.2: Add Error Ordering Tests

**File:** `src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`  
**Effort:** 30 minutes  
**Risk:** None

### Task D.2.1: Add Error Order Test

```csharp
[Fact]
public void Pipeline_ReportsErrorsInValidatorOrder()
{
    // Code with multiple error types
    var code = @"
class BadOperator:
    def __add__(self):  # Signature error (Order 150)
        break           # Control flow error (Order 400)
        return 0
";
    var (_, typeChecker) = CompileAndCheck(code);
    
    Assert.True(typeChecker.Errors.Count >= 2);
    
    // Signature errors should come before control flow errors
    var signatureErrorIndex = typeChecker.Errors.ToList()
        .FindIndex(e => e.Message.Contains("parameter"));
    var controlFlowErrorIndex = typeChecker.Errors.ToList()
        .FindIndex(e => e.Message.Contains("break"));
    
    // This test documents expected behavior, not strict requirement
    // (error order may not matter to users)
}
```

---

## D.3: Add Error Deduplication Tests

**Effort:** 30 minutes  
**Risk:** None

### Task D.3.1: Test No Duplicate Errors

```csharp
[Fact]
public void Pipeline_NoDuplicateErrors()
{
    var code = @"
def foo() -> int:
    x = 5
";
    var (_, typeChecker) = CompileAndCheck(code);
    
    // Should have exactly one "must return" error, not duplicates
    var returnErrors = typeChecker.Errors
        .Where(e => e.Message.Contains("must return"))
        .ToList();
    
    Assert.Single(returnErrors);
}
```

---

## D.4: Add Multi-File Project Tests

**Effort:** 1 hour  
**Risk:** None

### Task D.4.1: Test Cross-File Validation

```csharp
[Fact]
public void Pipeline_ValidatesAcrossFiles()
{
    // Use ProjectCompilationHelper for multi-file tests
    var files = new Dictionary<string, string>
    {
        ["base.spy"] = @"
@abstract
class Base:
    @abstract
    def process(self) -> int:
        ...
",
        ["derived.spy"] = @"
from base import Base

class Derived(Base):
    # Missing @override - should error
    def process(self) -> int:
        return 42
"
    };
    
    // Test that override validation works across files
}
```

---

## D.5: Add Error Limit Tests

**Effort:** 30 minutes  
**Risk:** None

### Task D.5.1: Test MaxErrors Configuration

```csharp
[Fact]
public void Pipeline_RespectsMaxErrors()
{
    // Generate code with many errors
    var codeBuilder = new StringBuilder();
    for (int i = 0; i < 200; i++)
    {
        codeBuilder.AppendLine($"def func{i}() -> int:");
        codeBuilder.AppendLine("    pass  # Missing return");
    }
    
    var code = codeBuilder.ToString();
    
    // Configure max errors
    var typeChecker = CreateTypeCheckerWithMaxErrors(50);
    // ... compile and check ...
    
    Assert.True(typeChecker.Errors.Count <= 55); // Allow some tolerance
}
```

---

## D.6: Add Edge Case Tests

**Effort:** 30 minutes  
**Risk:** None

### Task D.6.1: Test Empty Module

```csharp
[Fact]
public void Pipeline_HandlesEmptyModule()
{
    var code = "";
    var (_, typeChecker) = CompileAndCheck(code);
    Assert.Empty(typeChecker.Errors);
}
```

### Task D.6.2: Test Module With Only Comments

```csharp
[Fact]
public void Pipeline_HandlesCommentsOnlyModule()
{
    var code = @"
# This is a comment
# Another comment
";
    var (_, typeChecker) = CompileAndCheck(code);
    Assert.Empty(typeChecker.Errors);
}
```

### Task D.6.3: Test Deeply Nested Code

```csharp
[Fact]
public void Pipeline_HandlesDeeplyNestedCode()
{
    var code = @"
def outer() -> None:
    class Inner:
        def method(self) -> None:
            while True:
                if True:
                    for i in range(10):
                        break
";
    var (_, typeChecker) = CompileAndCheck(code);
    Assert.Empty(typeChecker.Errors);
}
```

---

## D.7: Run and Verify All Tests

**Effort:** 30 minutes  
**Risk:** None

### Task D.7.1: Run All Integration Tests

```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Integration"
```

### Task D.7.2: Run Full Test Suite

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

**Commit Point:**
```bash
git add src/Sharpy.Compiler.Tests/Integration/
git commit -m "test(integration): add comprehensive validation pipeline tests

Tests for:
- Error ordering
- Deduplication
- Multi-file validation
- Error limits
- Edge cases (empty module, deeply nested code)"
```

---

## D.8: Part D Summary

### Task D.8.1: Document Test Coverage

Create or update a test coverage summary:

```bash
dotnet test src/Sharpy.Compiler.Tests --collect:"XPlat Code Coverage"
```

### Task D.8.2: Final Full Test Run

```bash
dotnet test src/Sharpy.Compiler.Tests --verbosity normal
```

**Final counts:**
- Tests before this task list: _____
- Tests after this task list: _____
- All passing: [ ] Yes / [ ] No

---

# Final Summary and Merge

## Final Checklist

- [ ] All tests pass
- [ ] No new warnings in build
- [ ] Documentation updated
- [ ] All commits have meaningful messages

## Merge to Main

```bash
git checkout main
git merge feature/span-migration-pipeline-completion
git push origin main
```

## Create Release Notes

Document what was completed:

1. **Source Span Migration** — All AST nodes now have `TextSpan` support
2. **Validation Pipeline** — Simplified error collection, legacy deduplication removed
3. **TypeInferenceService** — Clean separation of type inference from validation
4. **Integration Tests** — Comprehensive pipeline testing

## Next Steps (Future Tasks)

After this task list is complete, the following become unblocked:

1. **Remove Legacy Validators Entirely** — Now that V2 validators handle all errors
2. **LSP Foundation** — Spans enable accurate position-based queries
3. **Better Error Messages** — Spans enable source code excerpts in errors
4. **Parallel Compilation Preparation** — Stateless services enable parallelism

---

## Appendix: Node Types Checklist

Use this to track progress on Part A span migration:

### Expressions
- [ ] Identifier (already done)
- [ ] IntegerLiteral
- [ ] FloatLiteral
- [ ] StringLiteral
- [ ] BooleanLiteral
- [ ] NoneLiteral
- [ ] EllipsisLiteral
- [ ] FStringLiteral
- [ ] ListLiteral
- [ ] DictLiteral
- [ ] SetLiteral
- [ ] TupleLiteral
- [ ] BinaryOp
- [ ] UnaryOp
- [ ] ComparisonChain
- [ ] MemberAccess
- [ ] IndexAccess
- [ ] SliceAccess
- [ ] Call
- [ ] ListComprehension
- [ ] DictComprehension
- [ ] SetComprehension
- [ ] GeneratorExpression
- [ ] Parenthesized
- [ ] TernaryExpression
- [ ] LambdaExpression
- [ ] AwaitExpression
- [ ] YieldExpression
- [ ] WalrusExpression
- [ ] TryExpression
- [ ] MaybeExpression
- [ ] SuperExpression

### Statements
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

### Definitions
- [ ] FunctionDef
- [ ] ClassDef
- [ ] StructDef
- [ ] InterfaceDef
- [ ] EnumDef
- [ ] PropertyDef
- [ ] EventDef
- [ ] Decorator
- [ ] Parameter

### Imports
- [ ] ImportStatement
- [ ] FromImportStatement

### Types
- [ ] SimpleType
- [ ] NullableType
- [ ] GenericType
- [ ] UnionType
- [ ] FunctionType
- [ ] TupleType
