# Phase 8.1–8.5: Completing Pattern Matching — Implementation Plan

## Overview

Match statements currently support 5 patterns (Literal, Wildcard, Binding, Tuple, MemberAccess) + guards. All advanced pattern AST nodes exist in `Pattern.cs` and `Expression.Future.cs` as placeholders but are not wired into the pipeline.

## Recommended Order: 8.2, 8.4, 8.3, 8.5, 8.1

**Rationale:** 8.2 (or-patterns) is simplest — composes existing patterns. 8.4 (relational) adds a new prefix form but is self-contained. 8.3 (type patterns) requires type resolution in pattern context. 8.5 (property/positional) requires property lookup and `Deconstruct` awareness. 8.1 (match expression) benefits from all patterns being available for testing.

---

## 8.2 — Or-Patterns (`case "a" | "b":`)

### Current State
- `OrPattern` AST in `Pattern.cs` (line ~369) with `Alternatives: ImmutableArray<Pattern>`
- Parser errors on `|`: test `match_or_pattern_unsupported_0001.spy` confirms
- Skipped test `match_or_wildcard_exhaustive_0001.skip` waiting for this

### Commits

**Commit 8.2a: Parser support**

File: `src/Sharpy.Compiler/Parser/Parser.Statements.cs`

1. Rename `ParsePattern()` → `ParseSinglePattern()` (private)
2. New `ParsePattern()`:
   - Call `ParseSinglePattern()` for first pattern
   - While `Current.Type == TokenType.Pipe`: advance, parse next single pattern
   - If 2+ alternatives: return `OrPattern { Alternatives = ... }`
   - If 1: return the single pattern unchanged

**Commit 8.2b: Semantic support**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

1. Add `case OrPattern orPattern:` in `CheckPattern()`
2. Check each alternative: `CheckPattern(alt, scrutineeType)`
3. Validate: binding patterns inside or-patterns → error (C# restriction). Wildcard `_` is allowed.

**Commit 8.2c: CodeGen support**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Patterns.cs`

1. Add `case OrPattern orPattern:` in `GenerateMatchPattern()`
2. Generate C# `or` pattern: `BinaryPattern(SyntaxKind.OrPattern, left, right)`
3. For 3+ alternatives, nest right-associatively: `a or (b or c)`
4. **MemberAccess in or-patterns:** Generate `var __matchN` binding + combined `when` guard with `||`

**Commit 8.2d: Tests**

- Remove `match_or_wildcard_exhaustive_0001.skip`
- Update/convert `match_or_pattern_unsupported_0001` from error to success test
- New: `match_or_literal_0001.spy`, `match_or_member_access_0001.spy`, `match_or_binding_error_0001.spy` + `.error`

---

## 8.4 — Relational Patterns (`case > 0:`)

### Current State
- `RelationalPattern` AST in `Pattern.cs` (line ~211) with `Operator: string` and `Value: Expression`
- Parser errors: test `match_relational_pattern_unsupported_0001.spy` confirms

### Commits

**Commit 8.4a: Parser support**

File: `src/Sharpy.Compiler/Parser/Parser.Statements.cs`

1. In `ParseSinglePattern()`, add cases:
   ```
   case TokenType.Greater:
   case TokenType.Less:
   case TokenType.GreaterEqual:
   case TokenType.LessEqual:
       return ParseRelationalPattern();
   ```
2. `ParseRelationalPattern()`: capture operator, advance, parse value via `ParseUnary()`, return `RelationalPattern`

**Commit 8.4b: Semantic support**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

1. Add `case RelationalPattern relational:` in `CheckPattern()`
2. Check value expression type
3. Validate scrutinee is comparable (numeric types)

**Commit 8.4c: CodeGen support**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Patterns.cs`

1. Map operators to C# relational pattern syntax kinds:
   - `">"` → `GreaterThanRelationalPattern`
   - `">="` → `GreaterThanOrEqualRelationalPattern`
   - `"<"` → `LessThanRelationalPattern`
   - `"<="` → `LessThanOrEqualRelationalPattern`
2. `SyntaxFactory.RelationalPattern(Token(syntaxKind), expr)`

**Commit 8.4d: Tests**

- Convert `match_relational_pattern_unsupported_0001` from error to success
- New: `match_relational_basic_0001.spy`, `match_relational_combined_0001.spy`, `match_relational_type_error_0001.spy` + `.error`

---

## 8.3 — Type Patterns with Binding (`case int() as n:`)

### Current State
- `TypePattern` AST in `Pattern.cs` (line ~87) with `Type: TypeAnnotation` and `BindingName: Identifier?`
- Parser errors: test `match_type_pattern_unsupported_0001.spy` confirms

### Commits

**Commit 8.3a: Parser support**

File: `src/Sharpy.Compiler/Parser/Parser.Statements.cs`

1. In `ParseIdentifierOrMemberAccessPattern()` (line ~1097): after identifier, check for `(`:
   ```
   if (Current.Type == TokenType.LeftParen)
       return ParseTypePattern(token);
   ```
2. `ParseTypePattern(Token typeToken)`:
   - Create `TypeAnnotation` from type name
   - `Expect(LeftParen)`, `Expect(RightParen)` (empty parens = pure type check)
   - Check for `as` keyword → parse binding name
   - Return `TypePattern { Type, BindingName }`
3. **Handle builtin type keywords:** `int`, `str`, etc. may be keyword tokens. Add cases in `ParseSinglePattern()`:
   ```
   case TokenType.Int:
   case TokenType.Str:
   case TokenType.Float:
   case TokenType.Bool:
       if (Peek().Type == TokenType.LeftParen)
           return ParseTypePattern(Current);
   ```

**Commit 8.3b: Semantic support**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

1. Add `case TypePattern typePattern:` in `CheckPattern()`
2. Resolve type via `_typeResolver.ResolveTypeAnnotation(typePattern.Type)`
3. Validate compatibility with scrutinee type
4. If `BindingName` present: create `VariableSymbol`, define in scope, set type

**Commit 8.3c: CodeGen support**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Patterns.cs`

1. Map to C# declaration pattern: `DeclarationPattern(type, designation)`
   - With binding: `SingleVariableDesignation(Identifier(mangledName))`
   - Without: `DiscardDesignation()`
2. Example: `case int() as n:` → C# `case int n:`

**Commit 8.3d: Tests**

- Convert `match_type_pattern_unsupported_0001` from error to success
- New: `match_type_basic_0001.spy`, `match_type_no_binding_0001.spy`, `match_type_multiple_0001.spy`, `match_type_guard_0001.spy`

---

## 8.5 — Property/Positional Patterns

### Current State
- `PropertyPattern`, `PropertyPatternField`, `PositionalPattern` AST nodes in `Pattern.cs` (lines ~279–364)
- No parser construction; depends on type patterns (8.3) for `TypeName(` prefix

### Commits

**Commit 8.5a: Parser support**

File: `src/Sharpy.Compiler/Parser/Parser.Statements.cs`

1. Extend `ParseTypePattern()` from 8.3. After `Expect(LeftParen)`:
   - `RightParen` → pure type pattern (8.3)
   - `Identifier` + `Assign` → property pattern: parse `name=pattern` pairs
   - Otherwise → positional pattern: parse comma-separated patterns
2. **Property:** `PropertyPatternField { Name, Pattern }` per field
3. **Positional:** elements are `ParsePattern()` results
4. **Disambiguation:** `identifier =` → property; else → positional. Mixed = error.

**Commit 8.5b: Semantic — property patterns**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

1. Add `case PropertyPattern propPattern:` in `CheckPattern()`
2. Resolve type, look up property/field on `TypeSymbol`
3. For each field: validate name exists, recursively `CheckPattern(field.Pattern, fieldType)`

**Commit 8.5c: Semantic — positional patterns**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs`

1. Add `case PositionalPattern posPattern:` in `CheckPattern()`
2. Resolve type, check for `Deconstruct` method or field count
3. Validate element count matches, recursively check each element
4. **Initial simplification:** Match positionally against fields in declaration order

**Commit 8.5d: CodeGen — property patterns**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Patterns.cs`

1. Map to C# recursive pattern with property clause:
   ```csharp
   // Sharpy: case Point(x=0, y=1):
   // C#:    case Point { X: 0, Y: 1 }:
   ```
2. `RecursivePattern().WithType(type).WithPropertyPatternClause(...)`
3. Property names mangled via `NameMangler.Transform(name, NameContext.Property)`

**Commit 8.5e: CodeGen — positional patterns**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Patterns.cs`

1. Map to C# recursive pattern with positional clause:
   ```csharp
   // Sharpy: case Point(0, y):
   // C#:    case Point(0, var y):
   ```
2. `RecursivePattern().WithType(type).WithPositionalPatternClause(...)`

**Commit 8.5f: Tests**

- New: `match_property_basic_0001.spy`, `match_positional_basic_0001.spy`, `match_positional_binding_0001.spy`
- Error tests: `match_property_unknown_field_0001.spy` + `.error`, `match_positional_count_mismatch_0001.spy` + `.error`

---

## 8.1 — Match Expression (Expression Form)

### Current State
- `MatchExpression` AST in `Expression.Future.cs` (line ~60) with `Scrutinee`, `Arms: ImmutableArray<MatchArm>`
- `MatchArm` has `Pattern`, `Guard?`, `Result: Expression`
- Parser dispatches `match` only as statement

### Design
- **Statement level:** `ParseStatement()` catches `match` → `ParseMatchStatement()` (unchanged)
- **Expression level:** Add `TokenType.Match` to `ParsePrimary()` → `ParseMatchExpression()`
- **Disambiguation:** Expression form has `case pattern: expression` (single line); statement form has `case pattern:` + NEWLINE INDENT block

### Commits

**Commit 8.1a: Parser support**

File: `src/Sharpy.Compiler/Parser/Parser.Primaries.cs`

1. Add `TokenType.Match` case → `ParseMatchExpression()`

File: `src/Sharpy.Compiler/Parser/Parser.Expressions.cs` (or `.Statements.cs`)

2. `ParseMatchExpression()`:
   - Parse scrutinee, colon, newline, indent
   - Parse arms: `case pattern [if guard]: expression` per line
   - `MatchArm { Pattern, Guard, Result }` per arm
   - Expect dedent
   - Return `MatchExpression { Scrutinee, Arms }`

**Commit 8.1b: Semantic support**

File: `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`

1. Add `MatchExpression matchExpr => CheckMatchExpression(matchExpr),` in dispatch
2. Check scrutinee, each arm's pattern + guard + result expression
3. Compute result type: least common ancestor of all arm result types

**Commit 8.1c: CodeGen support**

File: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` (dispatch) + `RoslynEmitter.Patterns.cs` (impl)

1. Emit C# switch expression: `SwitchExpression(scrutineeExpr, arms)`
2. Each arm: `SwitchExpressionArm(pattern, whenClause, resultExpr)`

**Commit 8.1d: Tests**

- New: `match_expr_basic_0001.spy`, `match_expr_return_0001.spy`, `match_expr_nested_0001.spy`, `match_expr_guard_0001.spy`

---

## Commit Sequence (18 commits total)

| # | Commit | Feature |
|---|--------|---------|
| 1 | 8.2a | Parser: or-patterns |
| 2 | 8.2b | Semantic: or-patterns |
| 3 | 8.2c | CodeGen: or-patterns |
| 4 | 8.2d | Tests: or-patterns |
| 5 | 8.4a | Parser: relational patterns |
| 6 | 8.4b | Semantic: relational patterns |
| 7 | 8.4c | CodeGen: relational patterns |
| 8 | 8.4d | Tests: relational patterns |
| 9 | 8.3a | Parser: type patterns |
| 10 | 8.3b | Semantic: type patterns |
| 11 | 8.3c | CodeGen: type patterns |
| 12 | 8.3d | Tests: type patterns |
| 13 | 8.5a-c | Parser + Semantic: property/positional patterns |
| 14 | 8.5d-e | CodeGen: property/positional patterns |
| 15 | 8.5f | Tests: property/positional patterns |
| 16 | 8.1a | Parser: match expressions |
| 17 | 8.1b-c | Semantic + CodeGen: match expressions |
| 18 | 8.1d | Tests: match expressions |

Each commit must build and pass all existing tests.

---

## Files Summary

| File | Changes |
|------|---------|
| `Parser/Parser.Statements.cs` | Rename `ParsePattern` → `ParseSinglePattern`, new `ParsePattern` (or-wrapping), `ParseRelationalPattern`, extend for type/property/positional |
| `Parser/Parser.Primaries.cs` | Add `TokenType.Match` for match expressions |
| `Semantic/TypeChecker.Statements.cs` | Add OrPattern, RelationalPattern, TypePattern, PropertyPattern, PositionalPattern cases |
| `Semantic/TypeChecker.Expressions.cs` | Add `MatchExpression` dispatch |
| `CodeGen/RoslynEmitter.Patterns.cs` | Add all new pattern codegen + `GenerateMatchExpression()` |
| `CodeGen/RoslynEmitter.Expressions.cs` | Add `MatchExpression` dispatch |
| `Diagnostics/DiagnosticCodes.cs` | Add pattern-specific error codes if needed |
| Test fixtures in `pattern_matching/` | New `.spy`/`.expected`/`.error` files |

## Risks

1. **Or-patterns + MemberAccess guards** — combine with `||` in `when` clause
2. **Builtin type keywords in patterns** — `int`, `str` may be keyword tokens, handle both
3. **Match expression disambiguation** — statement dispatch catches first; expression only in `ParsePrimary()`
4. **Property vs. positional** — require `name=pattern` for property; plain values = positional
5. **C# 9.0 compatibility** — relational, or, recursive patterns are all C# 9.0, safe for generated code
