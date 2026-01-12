# Implementation Plan: Task 0.1.1.2 - Audit/Verify Operator Precedence

## Task Summary
Audit the parser's operator precedence implementation against the language specification (`docs/language_specification/operator_precedence.md`) to verify correctness and identify gaps.

---

## Executive Summary

**Finding:** The parser has **significant gaps** compared to the specification. Of 20 precedence levels in the spec, only 15 are implemented, with 5 operators missing entirely.

---

## 1. Step-by-Step Implementation Approach

### Step 1: Specification vs Implementation Comparison

**Source:** `docs/language_specification/operator_precedence.md`

| Prec | Spec Operators | Implementation Status | Parser Method | Associativity |
|------|----------------|----------------------|---------------|---------------|
| 1 | `()`, `[]`, `.`, `?.` | ✅ Implemented | `ParsePostfix()`, `ParsePrimary()` | Left |
| 2 | `**` | ✅ Implemented | `ParsePower()` | Right |
| 3 | `+x`, `-x`, `~x` | ✅ Implemented | `ParseUnary()` | Right (prefix) |
| 4 | `*`, `/`, `//`, `%` | ✅ Implemented | `ParseMultiplicative()` | Left |
| 5 | `+`, `-` | ✅ Implemented | `ParseAdditive()` | Left |
| 6 | `<<`, `>>` | ✅ Implemented | `ParseShift()` | Left |
| 7 | `&` | ✅ Implemented | `ParseBitwiseAnd()` | Left |
| 8 | `^` | ✅ Implemented | `ParseBitwiseXor()` | Left |
| 9 | `\|` | ✅ Implemented | `ParseBitwiseOr()` | Left |
| 10 | `\|>` | ❌ **NOT IMPLEMENTED** | - | Left |
| 11 | `to` | ❌ **NOT IMPLEMENTED** | - | Left |
| 12 | Comparisons | ✅ Implemented | `ParseComparison()` | Chained |
| 13 | `not` | ✅ Implemented | `ParseLogicalNot()` | Right (prefix) |
| 14 | `and` | ✅ Implemented | `ParseLogicalAnd()` | Left |
| 15 | `or` | ✅ Implemented | `ParseLogicalOr()` | Left |
| 16 | `??` | ✅ Implemented | `ParseNullCoalesce()` | Left |
| 17 | `try`, `maybe` | ❌ **NOT IMPLEMENTED** | - | Right (prefix) |
| 18 | `x if c else y` | ✅ Implemented | `ParseConditionalExpression()` | Right |
| 19 | `lambda` | ⚠️ **WRONG LOCATION** | `ParsePrimary()` | Right |
| 20 | `:=` | ❌ **NOT IMPLEMENTED** | - | Right |

### Step 2: Current Parser Call Chain Analysis

**Current implementation (lines 1317-1774 in Parser.cs):**

```
ParseExpression()
  → ParseConditionalExpression()   [if-else]
    → ParseNullCoalesce()          [??]
      → ParseLogicalOr()           [or]
        → ParseLogicalAnd()        [and]
          → ParseLogicalNot()      [not]
            → ParseComparison()    [==, <, in, is, etc.]
              → ParseBitwiseOr()   [|]
                → ParseBitwiseXor() [^]
                  → ParseBitwiseAnd() [&]
                    → ParseShift() [<<, >>]
                      → ParseAdditive() [+, -]
                        → ParseMultiplicative() [*, /, //, %]
                          → ParseUnary() [+x, -x, ~x]
                            → ParsePower() [**]
                              → ParsePostfix() [., [], (), ?.]
                                → ParsePrimary() [literals, lambda]
```

### Step 3: Critical Issues Identified

#### Issue 1: Missing Pipe Operator `|>` (Priority: High)
- **Token exists:** `TokenType.PipeForward` (Token.cs:133)
- **Not parsed:** No `ParsePipe()` method
- **Spec location:** Between bitwise OR (prec 9) and `to` (prec 11)
- **Impact:** Data pipeline expressions won't parse

#### Issue 2: Missing Type Coercion `to` (Priority: High)
- **Token exists:** `TokenType.To` (Token.cs:75)
- **Not parsed:** No `ParseTypeCast()` method
- **Spec location:** Between pipe (prec 10) and comparisons (prec 12)
- **Impact:** Type casting expressions won't work

#### Issue 3: Missing `try`/`maybe` Expressions (Priority: High)
- **Tokens exist:** `TokenType.Try`, `TokenType.Maybe`
- **Only statement form:** `ParseTryStatement()` exists for try-except blocks
- **Not expression form:** `try expr` and `maybe expr` not implemented
- **Spec location:** Between null-coalesce (prec 16) and conditional (prec 18)
- **Impact:** Result/Optional creation from expressions won't work

#### Issue 4: Lambda at Wrong Precedence (Priority: Medium)
- **Current location:** `ParsePrimary()` (highest precedence, line 2225)
- **Spec location:** Precedence 19 (second lowest, only above walrus)
- **Impact:** `items |> lambda x: x * 2` requires unnecessary parentheses
- **Note:** Parenthesized lambda still works correctly

#### Issue 5: Missing Walrus Operator `:=` (Priority: Medium)
- **Token missing:** No `TokenType.ColonEquals`
- **Not parsed:** No `ParseWalrusExpression()` method
- **Spec location:** Precedence 20 (lowest)
- **Impact:** Assignment expressions in conditions won't work

### Step 4: Required Implementation Changes

#### 4a. Add Pipe Operator Parsing

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`
**Insert:** New method `ParsePipe()` between `ParseBitwiseOr()` and `ParseComparison()`

```csharp
private Expression ParsePipe()
{
    var left = ParseBitwiseOr();

    while (Current.Type == TokenType.PipeForward)
    {
        Advance();
        var right = ParseBitwiseOr();
        left = new PipeExpression { Left = left, Right = right, ... };
    }

    return left;
}
```

**Update call chain:** `ParseComparison()` should call `ParseTypeCast()` → `ParsePipe()` → `ParseBitwiseOr()`

#### 4b. Add Type Coercion Parsing

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`
**Insert:** New method `ParseTypeCast()` between `ParsePipe()` and `ParseComparison()`

```csharp
private Expression ParseTypeCast()
{
    var left = ParsePipe();

    while (Current.Type == TokenType.To)
    {
        Advance();
        var targetType = ParseTypeAnnotation();
        left = new TypeCast { Expression = left, TargetType = targetType, ... };
    }

    return left;
}
```

#### 4c. Add Try/Maybe Expression Parsing

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`
**Insert:** New method between `ParseNullCoalesce()` and `ParseConditionalExpression()`

```csharp
private Expression ParseTryMaybeExpression()
{
    if (Current.Type == TokenType.Try)
    {
        Advance();
        var expr = ParseNullCoalesce(); // captures everything below
        return new TryExpression { Expression = expr, ... };
    }

    if (Current.Type == TokenType.Maybe)
    {
        Advance();
        var expr = ParseNullCoalesce();
        return new MaybeExpression { Expression = expr, ... };
    }

    return ParseNullCoalesce();
}
```

#### 4d. Fix Lambda Precedence

**Current:** Lambda parsed in `ParsePrimary()` (line 2225)
**Fix:**
1. Remove lambda case from `ParsePrimary()`
2. Add `ParseLambda()` method at precedence level 19
3. Keep parenthesized expressions working via `ParsePrimary()`

#### 4e. Add Walrus Operator

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`
**Add:** `ColonEquals` token type

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`
**Add:** `:=` token recognition

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`
**Add:** `ParseWalrusExpression()` at lowest precedence

---

## 2. Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Parser/Parser.cs` | Add 5 new parsing methods, update call chain |
| `src/Sharpy.Compiler/Lexer/Token.cs` | Add `ColonEquals` token type |
| `src/Sharpy.Compiler/Lexer/Lexer.cs` | Add `:=` token recognition |
| `src/Sharpy.Compiler/AST/Expression.cs` | Add `TryExpression`, `MaybeExpression`, `PipeExpression`, `WalrusExpression` |

---

## 3. Tests to Verify

### Existing Tests (Must Continue Passing)
- `ParseOperatorPrecedence` - `1 + 2 * 3` (Parser.cs:225)
- `ParsePowerRightAssociative` - `2 ** 3 ** 2` (Parser.cs:1137)
- `ParseComparisonChain` - `1 < 2 <= 3` (Parser.cs:260)

### New Tests Required

```csharp
// Pipe operator
[Fact] public void ParsePipePrecedence()
{
    // "5 + 3 |> str()" should parse as (5 + 3) |> str()
    var module = Parse("5 + 3 |> str()");
    var pipe = module.Body[0].As<ExpressionStatement>().Expression.As<PipeExpression>();
    pipe.Left.Should().BeOfType<BinaryOp>(); // 5 + 3
    pipe.Right.Should().BeOfType<FunctionCall>(); // str()
}

// Type coercion
[Fact] public void ParseToPrecedence()
{
    // "x + 1 to int64" should parse as (x + 1) to int64
    var module = Parse("x + 1 to int64");
    var cast = module.Body[0].As<ExpressionStatement>().Expression.As<TypeCast>();
    cast.Expression.Should().BeOfType<BinaryOp>();
}

// Try expression
[Fact] public void ParseTryExpressionPrecedence()
{
    // "try foo() + 5" should parse as try (foo() + 5)
    var module = Parse("try foo() + 5");
    var tryExpr = module.Body[0].As<ExpressionStatement>().Expression.As<TryExpression>();
    tryExpr.Expression.Should().BeOfType<BinaryOp>();
}

// Maybe expression
[Fact] public void ParseMaybeExpressionPrecedence()
{
    // "maybe d.get(k)" should parse as maybe (d.get(k))
    var module = Parse("maybe d.get(k)");
    var maybeExpr = module.Body[0].As<ExpressionStatement>().Expression.As<MaybeExpression>();
}

// Walrus operator
[Fact] public void ParseWalrusOperator()
{
    // "(x := 5) + 1"
    var module = Parse("(x := 5) + 1");
    var binOp = module.Body[0].As<ExpressionStatement>().Expression.As<BinaryOp>();
    binOp.Left.Should().BeOfType<WalrusExpression>();
}

// Combined precedence
[Fact] public void ParseCombinedPrecedence()
{
    // "try x + 1 to int if cond else y"
    // Should parse as: ((try ((x + 1) to int)) if cond else y)
    var module = Parse("try x + 1 to int if cond else y");
    var cond = module.Body[0].As<ExpressionStatement>().Expression.As<ConditionalExpression>();
    cond.ThenValue.Should().BeOfType<TryExpression>();
}
```

---

## 4. Potential Risks or Questions

### Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Breaking existing code** | Changing lambda precedence may break valid programs | Parenthesized lambda still works |
| **AST compatibility** | New AST nodes need CodeGen support | Add stubs with NotImplementedException |
| **Lexer changes** | `:=` must not conflict with `:` then `=` | Lexer handles multi-char tokens already |

### Open Questions

1. **Walrus scope in comprehensions:** Spec says variables don't leak - is this parser or semantic analyzer responsibility?
   - **Answer:** Semantic analyzer (name resolver)

2. **`try` with type specifier:** `try[ValueError] expr` - how to parse the generic-style type?
   - **Needs:** Special case for `try` followed by `[`

3. **`to` with nullable:** `x to Type?` - is `?` part of type or separate?
   - **Answer:** Part of type annotation, handled by `ParseTypeAnnotation()`

---

## 5. Compliance Summary

| Category | Count | Status |
|----------|-------|--------|
| Compliant operators | 15/20 | 75% |
| Missing operators | 5 | **Critical** |
| Wrong precedence | 1 (lambda) | Medium |
| **Overall compliance** | ~70% | **Needs Work** |

---

## 6. Recommended Implementation Order

1. **Phase 1 (This Task):** Complete audit documentation ✅
2. **Phase 2:** Implement `|>` pipe operator (enables data pipelines)
3. **Phase 3:** Implement `to` type coercion (enables safe casting)
4. **Phase 4:** Implement `try`/`maybe` expressions (enables Result/Optional)
5. **Phase 5:** Fix lambda precedence (improves ergonomics)
6. **Phase 6:** Implement `:=` walrus operator (enables assignment expressions)

Each phase can be a separate sub-task with its own tests.
