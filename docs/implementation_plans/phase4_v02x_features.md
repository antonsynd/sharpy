# Phase 4: v0.2.x Features -- Implementation Plan

This document provides step-by-step implementation plans for four major v0.2.x
features that require significant new compiler infrastructure. Each section is
self-contained and follows the canonical implementation order:

```
Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
```

**Before starting any feature**, read the General Guidance section at the end
of this document.

---

## Table of Contents

1. [Feature 1: Match Statement (Pattern Matching)](#feature-1-match-statement-pattern-matching)
2. [Feature 2: Tagged Unions (Algebraic Data Types)](#feature-2-tagged-unions-algebraic-data-types)
3. [Feature 3: Async/Await](#feature-3-asyncawait)
4. [Feature 4: Generic Variance](#feature-4-generic-variance)
5. [Dependency Graph and Recommended Order](#dependency-graph-and-recommended-order)
6. [Cross-Cutting Concerns](#cross-cutting-concerns)
7. [General Guidance](#general-guidance)

---

## Feature 1: Match Statement (Pattern Matching)

### Context and Motivation

Pattern matching is foundational infrastructure. Tagged unions (Feature 2) depend
on it for `match` over union cases. Without match statements, union types are
usable only via `if`/`isinstance` chains, which defeats the purpose of ADTs.

Match statement maps directly to C# `switch` statement/expression (C# 8.0+), so
the codegen story is clean. The primary complexity is in the parser
(disambiguating expression vs statement form) and semantic analysis
(exhaustiveness checking).

**Spec**: `docs/language_specification/match_statement.md`

The spec defines two forms:
- **Statement form**: Standalone `match value:` with indented case blocks
- **Expression form**: `result = match value:` with inline case expressions

The spec also defines 9 supported patterns: literal, type, type with binding,
wildcard, guard, or, tuple, property, and relational. All map to C# 9.0
pattern matching.

### Current State Assessment

| What | Where | Status |
|------|-------|--------|
| `match` and `case` keywords | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` lines 63-64 | Lexed as `TokenType.Match` and `TokenType.Case` |
| `match` and `case` in keyword map | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Lexer.cs` lines 120-121 | Recognized as reserved keywords |
| `MatchStatement` AST node | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` lines 27-61 | Complete: `Scrutinee`, `Cases` (array of `MatchCase`), `ValidateInvariants()`, `GetChildNodes()` |
| `MatchCase` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` lines 66-89 | Complete: `Pattern`, `Guard`, `Body`, source locations |
| `MatchExpression` AST node | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs` lines 60-93 | Complete: `Scrutinee`, `Arms` (array of `MatchArm`), `ValidateInvariants()`, `GetChildNodes()` |
| `MatchArm` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs` lines 98-121 | Complete: `Pattern`, `Guard`, `Result`, source locations |
| Pattern hierarchy (10 types) | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Pattern.cs` (entire file, 290 lines) | Complete: `WildcardPattern`, `BindingPattern`, `LiteralPattern`, `TypePattern`, `UnionCasePattern`, `TuplePattern`, `ListPattern`, `OrPattern`, `AndPattern`, `GuardPattern` |
| `AstVisitor` dispatch | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs` lines 140-144, 222-226, 378-379, 427-428, 516-517, 550-551, 681-682, 730-731 | Both `MatchStatement` and `MatchExpression` dispatched in void and generic visitors |
| Variable name collection for match | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` lines 468-474 | `CollectSourceVariableNames` handles `MatchStatement` cases |
| Pattern variable name collection | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` lines 502-556 | `CollectVariableNamesFromPattern` handles all 10 pattern types |
| TypeChecker fallback | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` line 371-378 | Falls through to `default:` which reports `UnrecognizedStatementType` (SPY0255) |
| TypeChecker expression fallback | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` line 62 | Falls through to `HandleUnrecognizedExpression` (SPY0256) |
| `ControlFlowGraphBuilder` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs` | No `MatchStatement` handling yet |
| `TokenType.As` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` line 54 | Already lexed as a keyword (used for imports: `import x as y`) |

**Missing**: No parser methods (`ParseMatchStatement`, `ParsePattern`, etc.).
No `RelationalPattern` AST node (the spec defines `case > 0:` relational
patterns, but no AST node exists -- add one or defer).

### Files to Modify

**Parser**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs` -- Add `TokenType.Match` case to `ParseStatement()` switch (line 353) and to `IsSyncToken()` (line 280)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Statements.cs` -- Add `ParseMatchStatement()` and `ParseMatchCase()` methods
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs` or `Parser.Primaries.cs` -- Add `ParseMatchExpression()` wired into expression parsing
- New file: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Patterns.cs` -- Add `ParsePattern()` and sub-pattern methods (recommend a separate partial file for clarity)

**Semantic**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` -- Add `CheckMatchStatement()` method
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` -- Add `CheckMatchExpression()` method, add `MatchExpression` case in the expression switch
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` -- Add `MatchStatement` case in `CheckStatement()` (before the `default:` at line 371)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` -- Add pattern matching diagnostics in SPY0350-SPY0359 range

**Validation**:
- New file: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Validation/ExhaustivenessValidator.cs` -- Implement `ISemanticValidator` for exhaustiveness checking

**CodeGen**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` -- Add `MatchStatement` case in `GenerateBodyStatement()`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` -- Add `MatchExpression` case in `GenerateExpression()`

**CFG**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs` -- Add `MatchStatement` branching

### Step-by-Step Implementation

#### Sub-task 1A: Parser -- `ParseMatchStatement()` and `ParsePattern()` (Literal + Wildcard)

**Goal**: Parse the simplest match statements with literal and wildcard patterns.
No type patterns, no guards, no expression form yet.

**Entry point**: Add `TokenType.Match => ParseMatchStatement()` to the switch in
`/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs`
line 353. Also add `TokenType.Match` to the `IsSyncToken()` switch at line 280.

**Disambiguation** (statement vs expression): Per the spec, the parser determines
form based on context:
- If `match` appears at statement level (top of `ParseStatement()`), parse as
  `MatchStatement`
- If `match` appears inside an expression context (RHS of assignment, return
  value, function argument), parse as `MatchExpression`
- Within a case: if `case pattern:` is followed by `NEWLINE INDENT`, it is
  statement form. If followed by an expression on the same line, it is
  expression form.

**`ParseMatchStatement()`** implementation:

```csharp
private MatchStatement ParseMatchStatement()
{
    var startToken = Current;
    Advance(); // consume 'match'

    var scrutinee = ParseExpression();
    Expect(TokenType.Colon, ":");
    ExpectNewline();
    Expect(TokenType.Indent, "INDENT");

    var cases = new List<MatchCase>();
    while (Current.Type == TokenType.Case)
    {
        cases.Add(ParseMatchCase());
    }

    Expect(TokenType.Dedent, "DEDENT");

    return new MatchStatement
    {
        Scrutinee = scrutinee,
        Cases = cases.ToImmutableArray(),
        LineStart = startToken.Line,
        ColumnStart = startToken.Column,
        // ...
    };
}
```

**`ParseMatchCase()`** implementation:

```csharp
private MatchCase ParseMatchCase()
{
    var startToken = Current;
    Expect(TokenType.Case, "case");

    var pattern = ParsePattern();

    Expression? guard = null;
    if (Current.Type == TokenType.If)
    {
        Advance(); // consume 'if'
        guard = ParseExpression();
    }

    Expect(TokenType.Colon, ":");
    ExpectNewline();
    Expect(TokenType.Indent, "INDENT");
    var body = ParseBlock();
    Expect(TokenType.Dedent, "DEDENT");

    return new MatchCase
    {
        Pattern = pattern,
        Guard = guard,
        Body = body.ToImmutableArray(),
        LineStart = startToken.Line,
        ColumnStart = startToken.Column,
        // ...
    };
}
```

**`ParsePattern()`** -- start with the simplest patterns:

```csharp
private Pattern ParsePattern()
{
    var pattern = ParseAtomicPattern();

    // Handle or-patterns: pattern | pattern
    if (Current.Type == TokenType.Pipe)
    {
        var alternatives = new List<Pattern> { pattern };
        while (Current.Type == TokenType.Pipe)
        {
            Advance(); // consume '|'
            alternatives.Add(ParseAtomicPattern());
        }
        pattern = new OrPattern
        {
            Alternatives = alternatives.ToImmutableArray(),
            // ...
        };
    }

    return pattern;
}

private Pattern ParseAtomicPattern()
{
    switch (Current.Type)
    {
        case TokenType.Identifier when Current.Value == "_":
            var wildcardToken = Current;
            Advance();
            return new WildcardPattern { /* source location */ };

        case TokenType.Integer:
        case TokenType.Float:
        case TokenType.String:
        case TokenType.True:
        case TokenType.False:
        case TokenType.None:
            var literal = ParseAtom(); // reuse existing literal parsing
            return new LiteralPattern { Literal = literal, /* ... */ };

        case TokenType.Identifier:
            // Could be: binding pattern, type pattern, or union case
            return ParseNamedPattern();

        case TokenType.LeftParen:
            return ParseTuplePattern();

        case TokenType.LeftBracket:
            return ParseListPattern();

        default:
            throw ReportError(
                $"Expected pattern, got '{Current.Value}'",
                Current.Line, Current.Column,
                DiagnosticCodes.Parser.UnexpectedToken);
    }
}
```

**Key decisions**:
- `TokenType.As` already exists (line 54 of Token.cs), so `int() as n` can use
  `Current.Type == TokenType.As` directly. No contextual keyword needed.
- For negative literal patterns (`case -1:`), check for `TokenType.Minus`
  followed by a numeric literal.

**Testing**: Parser unit tests verifying AST shape for:
- `match x: case 0: pass; case _: pass`
- `match x: case "hello": pass`
- `match x: case True: pass; case False: pass`

**Commit message**: `feat: Parse match statements with literal and wildcard patterns`

#### Sub-task 1B: Semantic Analysis -- `CheckMatchStatement()` (Basic)

**Goal**: Type-check match statements with literal and wildcard patterns.

**Files to modify**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` -- Add `case MatchStatement matchStmt: CheckMatchStatement(matchStmt); break;` in `CheckStatement()` before the `default:` at line 371.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` -- Add `CheckMatchStatement()` method.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` -- Add diagnostic codes:
  ```csharp
  // Pattern matching (SPY0350-SPY0359)
  public const string NonExhaustiveMatch = "SPY0350";
  public const string IncompatiblePattern = "SPY0351";
  public const string DuplicatePattern = "SPY0352";
  public const string OrPatternVariableMismatch = "SPY0353";
  public const string AwaitInPattern = "SPY0354";
  ```

**`CheckMatchStatement()`** implementation:

```csharp
private void CheckMatchStatement(MatchStatement match)
{
    var scrutineeType = CheckExpression(match.Scrutinee);

    foreach (var matchCase in match.Cases)
    {
        PushScope(); // Each case has its own scope for bindings

        CheckPattern(matchCase.Pattern, scrutineeType);

        if (matchCase.Guard != null)
        {
            var guardType = CheckExpression(matchCase.Guard);
            if (guardType is not BuiltinType { Name: "bool" })
            {
                AddError("Guard condition must be bool", ...);
            }
        }

        foreach (var stmt in matchCase.Body)
        {
            CheckStatement(stmt);
        }

        PopScope();
    }
}
```

**`CheckPattern()`** -- pattern type checking:

```csharp
private void CheckPattern(Pattern pattern, SemanticType scrutineeType)
{
    switch (pattern)
    {
        case WildcardPattern:
            // Always valid, no bindings
            break;

        case LiteralPattern lit:
            var litType = CheckExpression(lit.Literal);
            if (!litType.IsAssignableTo(scrutineeType)
                && !scrutineeType.IsAssignableTo(litType))
            {
                AddError($"Pattern type '{litType.GetDisplayName()}' is "
                    + $"incompatible with match type '{scrutineeType.GetDisplayName()}'",
                    ..., DiagnosticCodes.Semantic.IncompatiblePattern);
            }
            break;

        case BindingPattern binding:
            // Register variable with scrutinee type
            RegisterLocal(binding.Name, scrutineeType);
            break;

        case TypePattern typePattern:
            // Validate type is a subtype of scrutinee
            var checkedType = ResolveTypeAnnotation(typePattern.Type);
            if (typePattern.BindingName != null)
            {
                RegisterLocal(typePattern.BindingName, checkedType);
            }
            break;

        // ... other patterns ...
    }
}
```

**Testing**: Semantic tests that compile (but don't necessarily run) match
statements and verify no spurious errors.

**Commit message**: `feat: Add semantic analysis for match statement patterns`

#### Sub-task 1C: CodeGen -- Emit `switch` Statement

**Goal**: Emit C# `switch` statements from `MatchStatement` AST nodes.

**Files to modify**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` -- Add `case MatchStatement matchStmt:` in `GenerateBodyStatement()`.

**Pattern-to-C# mapping** (using `SyntaxFactory`):

| Sharpy Pattern | C# Pattern | SyntaxFactory Method |
|---------------|------------|---------------------|
| `case 0:` | `case 0:` | `CaseSwitchLabel(ConstantPattern(LiteralExpression(...)))` |
| `case _:` | `default:` | `DefaultSwitchLabel()` |
| `case int() as n:` | `case int n:` | `CasePatternSwitchLabel(DeclarationPattern(type, SingleVariableDesignation(name)))` |
| `case int() as n if n > 0:` | `case int n when n > 0:` | `CasePatternSwitchLabel(...).WithWhenClause(WhenClause(guardExpr))` |
| `case "a" \| "b":` | `case "a" or "b":` | `BinaryPattern(SyntaxKind.OrPattern, left, right)` |
| `case (x, y):` | positional pattern | `RecursivePattern().WithPositionalPatternClause(...)` |

**CodeGen for simple `switch` statement**:

```csharp
private StatementSyntax GenerateMatchStatement(MatchStatement match)
{
    var scrutinee = GenerateExpression(match.Scrutinee);
    var sections = new List<SwitchSectionSyntax>();

    foreach (var matchCase in match.Cases)
    {
        var label = GeneratePatternLabel(matchCase.Pattern, matchCase.Guard);
        var bodyStatements = matchCase.Body
            .Select(GenerateBodyStatement)
            .ToList();

        // Add break if the last statement is not a return/throw
        if (!EndsWithJump(bodyStatements))
        {
            bodyStatements.Add(BreakStatement());
        }

        sections.Add(SwitchSection(
            SingletonList(label),
            List(bodyStatements.Cast<StatementSyntax>())));
    }

    return SwitchStatement(scrutinee, List(sections));
}
```

**Testing**:
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/match/match_literal_001.spy` + `.expected`
- `src/Sharpy.Compiler.Tests/Integration/TestFixtures/match/match_wildcard_001.spy` + `.expected`

**Commit message**: `feat: Emit C# switch statements for match (literal + wildcard)`

#### Sub-task 1D: Type Patterns, Binding, and Guards

**Goal**: Support type patterns (`case int() as n:`), binding patterns, and
guard conditions (`if n > 0`).

Extends 1A/1B/1C with:
- `ParseNamedPattern()` handling `int() as n` syntax
- `TypePattern` semantic checking with binding variable registration
- `DeclarationPattern` codegen with `WhenClause` for guards

**Testing**:
- `match/match_type_pattern_001.spy` through `_003.spy`
- `match/match_guard_001.spy`

**Commit message**: `feat: Add type patterns and guard conditions to match statement`

#### Sub-task 1E: Or Patterns and Match Expressions

**Goal**: Support `case "a" | "b":` or-patterns and the expression form
`result = match value: case 1: "one" case _: "other"`.

For **match expressions**, wire `ParseMatchExpression()` into the expression
parser. The expression form maps to C# `switch` expression:

```csharp
// Sharpy:  result = match x: case 1: "one"  case _: "other"
// C#:      var result = x switch { 1 => "one", _ => "other" };

var switchExpr = SwitchExpression(
    scrutinee,
    SeparatedList(arms.Select(arm =>
        SwitchExpressionArm(
            GeneratePattern(arm.Pattern),
            GenerateExpression(arm.Result))
        .WithWhenClause(arm.Guard != null
            ? WhenClause(GenerateExpression(arm.Guard))
            : null)
    ))
);
```

**Or-pattern** semantic constraint: All alternatives in an or-pattern must bind
the same set of variables with compatible types.

**Testing**:
- `match/match_or_pattern_001.spy`
- `match/match_expression_001.spy` through `_003.spy`

**Commit message**: `feat: Add or-patterns and match expressions`

#### Sub-task 1F: Exhaustiveness Checking, Tuple/List Patterns, CFG

**Goal**: Add exhaustiveness validator, tuple/list pattern parsing and codegen,
and control flow graph integration.

**Exhaustiveness validator**:
New file `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Validation/ExhaustivenessValidator.cs`:
- Implement `ISemanticValidator` with `Order` in the 405-410 range
- Check `bool` matches: both `True` and `False` must be covered
- Check `enum` matches: all enum values must be covered
- For other types: wildcard or binding pattern required
- Tagged union exhaustiveness: defer to Feature 2

**CFG update**:
In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs`:
- Create a block for scrutinee evaluation
- For each case, create a conditional branch
- Each case body is its own subgraph
- All exits converge to a post-match block
- If exhaustive, no fallthrough edge from scrutinee

**Testing**:
- Error tests: non-exhaustive bool match, non-exhaustive enum match
- `match/match_tuple_pattern_001.spy`
- `match/match_list_pattern_001.spy`

**Commit message**: `feat: Add exhaustiveness checking and tuple/list patterns to match`

### Decision Guidance

**Pattern parsing ambiguity: `Point(x, y)` -- is it a type pattern or function call?**

The parser cannot always distinguish a type pattern from a function call without
type information. Options:

| Approach | Pros | Cons |
|----------|------|------|
| **Option A**: Parse uniformly, resolve in semantic phase | Clean parser; defers ambiguity resolution | Semantic phase needs to handle pattern AST rewriting |
| **Option B** (recommended): Use syntactic markers per spec | Spec already distinguishes forms: `int()` for type, `Point(x=0)` for property | Positional patterns `Point(0, y)` are still ambiguous |

Go with Option B for v0.2.0. The spec uses `int() as n` for type patterns
(empty parens) and `Point(x=0, y=y)` for property patterns (keyword syntax).
Positional patterns like `Point(0, y)` are syntactically identical to function
calls, so defer positional patterns or require the `Point(x=0)` form.

**Guard syntax: `if` vs `when`**

The Sharpy spec uses `if` for guards (matching Python's PEP 634). C# uses
`when`. The parser should accept `if` and the emitter should output `when`.
This is a straightforward syntactic translation.

**Exhaustiveness approach**

| Approach | Pros | Cons |
|----------|------|------|
| **Simple coverage check** (recommended for v0.2.0) | Easy to implement; handles bool, enum, wildcard | Cannot detect overlapping or redundant patterns |
| **Full decision tree / usefulness algorithm** | Detects redundant patterns; complete coverage analysis | Significantly more complex; quadratic in pattern count |

Start with simple coverage checking. The C# compiler will also verify
exhaustiveness at the emitted level, providing a safety net.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Parser ambiguity between expression and statement form | Medium | Medium | Use context-based disambiguation per spec (statement-level vs expression) |
| Exhaustiveness checking for complex patterns | Medium | High | Start with simple types (bool, enum), defer complex union case exhaustiveness to Feature 2 |
| CFG integration | Low | Medium | Match is structurally similar to if/elif/else, which CFG already handles |
| Variable scoping in patterns | Medium | Medium | Follow scope push/pop pattern used by `if`/`for` in type checker |
| `as` keyword parsing in patterns | Low | Low | `TokenType.As` already exists (line 54 of Token.cs) |

---

## Feature 2: Tagged Unions (Algebraic Data Types)

### Context and Motivation

Tagged unions enable sum types -- types that can be one of several variants,
where each variant can carry different associated data. This is the core
abstraction for Result/Optional types and state machines.

The spec explicitly states that `Result[T, E]` and `Optional[T]` are
special-cased as structs for performance, while user-defined unions use
class-based representation (abstract base class + sealed nested classes).

**Depends on**: Feature 1 (Match Statement) -- pattern matching is the primary
consumption mechanism for tagged unions.

**Specs**:
- `docs/language_specification/tagged_unions.md` (main)
- `docs/language_specification/tagged_unions_result.md` (Result type)
- `docs/language_specification/tagged_unions_optional.md` (Optional type)

Key spec observations:
- Unions can have methods (e.g., `is_ok()`, `unwrap()`)
- Case constructors support both long form (`Result.Ok(42)`) and short form
  (`Ok(42)`) when the type is inferrable from context
- Unit cases (no data) can omit parentheses in both definition and pattern
- `Result[T, E]` and `Optional[T]` have struct implementations in Sharpy.Core
  (separate from user-defined unions)

### Current State Assessment

| What | Where | Status |
|------|-------|--------|
| `UnionDef` AST node | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` lines 107-151 | Complete: `Name`, `TypeParameters`, `Cases`, `Decorators`, `DocString` |
| `UnionCaseDef` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` lines 161-179 | Complete: `Name`, `Fields`, source locations |
| `UnionCaseField` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` lines 184-202 | Complete: `Name` (nullable for positional), `Type` |
| `UnionType` semantic type | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 718-738 | Complete: `Name`, `Symbol`, `CaseTypes`, `IsAssignableTo` |
| `UnionCasePattern` AST | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Pattern.cs` lines 113-140 | Complete: `UnionType`, `CaseName`, `FieldPatterns` |
| `SymbolSerializer` support | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Project/SymbolSerializer.cs` lines 273-274, 622-633 | Serialization and deserialization of `UnionType` implemented |
| `TypeMapper` placeholder | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs` lines 107-109 | `throw new NotSupportedException("Union types are not yet supported...")` |
| `AstVisitor` dispatch | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs` lines 225-226, 428, 551, 731 | `UnionDef` dispatched in both void and generic visitors |
| Variable name collection | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` lines 518-523 | `UnionCasePattern` handled for variable name collection |
| `ResultType` semantic type | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 437-468 | Complete: `OkType`, `ErrorType`, `IsAssignableTo`, `IsValueType = true` |
| `OptionalType` semantic type | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 384-423 | Complete: `UnderlyingType`, `IsAssignableTo`, `IsValueType = true` |
| Free union error | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs` lines 31-35 | Error message references `union` declarations: "Use 'union' declarations for custom sum types" |
| `union` keyword | NOT in lexer | Must be added to `Token.cs` and `Lexer.cs` keyword map |

**Missing scaffolding** (must be added):
- `union` keyword token in `Token.cs` and `Lexer.cs`
- `UnionDef` has no `Methods` or `Body` property for methods on unions (spec
  says unions can have methods like `is_ok()`, `unwrap()`)
- No `TypeKind.Union` in the `TypeKind` enum (current values at
  `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs`
  line 264: `Class`, `Struct`, `Interface`, `Enum`)

### Files to Modify

**Lexer**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` -- Add `Union` to `TokenType` enum
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Lexer.cs` -- Add `{ "union", TokenType.Union }` to keyword map

**AST**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` -- Add `ImmutableArray<Statement> Methods` (or `Body`) property to `UnionDef`

**Parser**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs` -- Add `TokenType.Union` to `ParseStatement()` switch and `IsSyncToken()`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs` -- Add `ParseUnionDef()` method

**Semantic**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs` -- Add `TypeKind.Union` to the `TypeKind` enum (line 264)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/NameResolver.cs` -- Add `UnionDef` handling in `ResolveDeclarations()`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeResolver.cs` -- Resolve type annotations on case fields
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` -- Add `UnionDef` case in `CheckStatement()`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` -- Add codes in SPY0360-SPY0369

**CodeGen**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` -- Add union emission
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs` -- Replace `throw new NotSupportedException` at line 108 with actual type mapping

### Step-by-Step Implementation

#### Sub-task 2A: Lexer and AST Preparation

**Goal**: Add `union` keyword and extend `UnionDef` with methods support.

**Lexer changes**:

In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs`,
add `Union` alongside the other definition keywords:

```csharp
// Keywords - Control Flow
Def,
Class,
Struct,
Interface,
Enum,
Union,    // <-- add here
```

In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Lexer.cs`,
add to the keyword dictionary:

```csharp
{ "union", TokenType.Union },
```

**AST changes**:

In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs`,
add a `Methods` property to `UnionDef`:

```csharp
public record UnionDef : Statement
{
    // ... existing properties ...

    /// <summary>
    /// Methods defined on the union (e.g., is_ok, unwrap).
    /// Emitted on the abstract base class.
    /// </summary>
    public ImmutableArray<Statement> Methods { get; init; }
        = ImmutableArray<Statement>.Empty;
}
```

Also update `GetChildNodes()` to yield method bodies.

**Symbol.cs**: Add `Union` to `TypeKind`:

```csharp
public enum TypeKind
{
    Class,
    Struct,
    Interface,
    Enum,
    Union     // <-- add here
}
```

**Commit message**: `feat: Add union keyword, TypeKind.Union, and Methods property to UnionDef`

#### Sub-task 2B: Parser -- `ParseUnionDef()`

**Goal**: Parse union definitions with named fields and unit cases.

**File**: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs`

**Grammar**:
```
union_def := 'union' NAME type_params? ':' NEWLINE INDENT case_def+ method_def* DEDENT
case_def  := 'case' NAME ( '(' field_list ')' )? NEWLINE
field_list := field (',' field)*
field     := NAME ':' type_annotation    // named field
           | type_annotation             // positional field
```

**Implementation**:

```csharp
private UnionDef ParseUnionDef()
{
    var startToken = Current;
    Advance(); // consume 'union'

    var name = Expect(TokenType.Identifier, "union name").Value;

    var typeParams = ImmutableArray<TypeParameterDef>.Empty;
    if (Current.Type == TokenType.LeftBracket)
    {
        typeParams = ParseTypeParameters();
    }

    Expect(TokenType.Colon, ":");
    ExpectNewline();
    Expect(TokenType.Indent, "INDENT");

    // Parse case definitions
    var cases = new List<UnionCaseDef>();
    while (Current.Type == TokenType.Case)
    {
        cases.Add(ParseUnionCaseDef());
    }

    // Parse optional method definitions
    var methods = new List<Statement>();
    while (Current.Type == TokenType.Def || Current.Type == TokenType.At)
    {
        methods.Add(ParseStatement());
    }

    Expect(TokenType.Dedent, "DEDENT");

    return new UnionDef
    {
        Name = name,
        TypeParameters = typeParams,
        Cases = cases.ToImmutableArray(),
        Methods = methods.ToImmutableArray(),
        LineStart = startToken.Line,
        ColumnStart = startToken.Column,
        // ...
    };
}

private UnionCaseDef ParseUnionCaseDef()
{
    var startToken = Current;
    Expect(TokenType.Case, "case");

    var caseName = Expect(TokenType.Identifier, "case name").Value;

    var fields = ImmutableArray<UnionCaseField>.Empty;
    if (Current.Type == TokenType.LeftParen)
    {
        Advance(); // consume '('
        var fieldList = new List<UnionCaseField>();

        while (Current.Type != TokenType.RightParen)
        {
            // Check for named field: name: type
            if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Colon)
            {
                var fieldName = Current.Value;
                Advance(); // consume name
                Advance(); // consume ':'
                var fieldType = ParseTypeAnnotation();
                fieldList.Add(new UnionCaseField { Name = fieldName, Type = fieldType, /* ... */ });
            }
            else
            {
                // Positional field: just a type
                var fieldType = ParseTypeAnnotation();
                fieldList.Add(new UnionCaseField { Name = null, Type = fieldType, /* ... */ });
            }

            if (Current.Type == TokenType.Comma) Advance();
        }

        Expect(TokenType.RightParen, ")");
        fields = fieldList.ToImmutableArray();
    }

    ExpectNewline();

    return new UnionCaseDef
    {
        Name = caseName,
        Fields = fields,
        LineStart = startToken.Line,
        ColumnStart = startToken.Column,
        // ...
    };
}
```

**Wire into parser**: Add `TokenType.Union => ParseUnionDef()` in
`ParseStatement()` and in `ParseDecoratedStatement()`.

**Testing**: Parse-only tests verifying correct AST structure for:
- Simple union with two named-field cases
- Union with unit cases (no parentheses)
- Generic union with type parameters
- Union with methods

**Commit message**: `feat: Parse union type definitions`

#### Sub-task 2C: Semantic -- Name Resolution and Type Registration

**Goal**: Register unions in the symbol table and resolve case field types.

**Files to modify**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeResolver.cs`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**NameResolver**: In `ResolveDeclarations()`, add a case for `UnionDef`:

```csharp
case UnionDef unionDef:
    var unionSymbol = new TypeSymbol
    {
        Name = unionDef.Name,
        Kind = SymbolKind.Type,
        TypeKind = TypeKind.Union,
        TypeParameters = unionDef.TypeParameters
            .Select(tp => new TypeParameterDef { Name = tp.Name, Constraints = tp.Constraints })
            .ToList(),
        DeclarationLine = unionDef.LineStart,
        DeclarationColumn = unionDef.ColumnStart,
    };
    _symbolTable.Define(unionSymbol);

    // Register case constructors as static factory methods
    foreach (var caseDef in unionDef.Cases)
    {
        var ctorSymbol = new FunctionSymbol
        {
            Name = $"{unionDef.Name}.{caseDef.Name}",
            Kind = SymbolKind.Function,
            IsStatic = true,
            // Parameters from case fields, return type is the union type
        };
        // Register in symbol table or as members of the union type
    }
    break;
```

**TypeChecker**: Add `CheckUnionDef(UnionDef union)`:
- Validate case names are unique
- Type-check method bodies (reuse existing method checking infrastructure)
- For `self` references inside methods, the type is the union type

**Type inference for case constructors**: When a function return type is a union
type and the return expression is a bare case constructor (`return Ok(42)`),
infer the full type from the function signature. This requires resolving `Ok`
and `Err` as case constructors in the current scope.

**Commit message**: `feat: Add semantic analysis for union type definitions`

#### Sub-task 2D: Code Generation -- Abstract Class Hierarchy

**Goal**: Emit C# abstract base class with sealed nested case classes.

**File**: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`

**C# lowering strategy** (matches spec exactly):

```csharp
public abstract class Result<T, E>
{
    private Result() { }  // Prevent external subclassing

    public sealed class Ok : Result<T, E>
    {
        public T Value { get; }
        public Ok(T value) => Value = value;
        public void Deconstruct(out T value) => value = Value;
    }

    public sealed class Err : Result<T, E>
    {
        public E Error { get; }
        public Err(E error) => Error = error;
        public void Deconstruct(out E error) => error = Error;
    }

    // User-defined methods here
}
```

**Key codegen decisions**:
- Abstract base class with private constructor (sealed hierarchy)
- Each case is a `sealed` nested class inheriting from the base
- Named fields become read-only auto-properties
- Each case gets a `Deconstruct` method for positional pattern matching
- Unit cases (no fields) are singletons: emit `public static readonly` instance
- Union methods are emitted on the abstract base class

**Using `SyntaxFactory`** for the base class:

```csharp
var baseClass = ClassDeclaration(unionName)
    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AbstractKeyword))
    .WithTypeParameterList(typeParamList)
    .AddMembers(
        // Private constructor
        ConstructorDeclaration(unionName)
            .AddModifiers(Token(SyntaxKind.PrivateKeyword))
            .WithBody(Block()),
        // Nested case classes...
        // User-defined methods...
    );
```

**Update `TypeMapper.cs`**: Replace the `throw new NotSupportedException` at
line 108 with:

```csharp
UnionType ut => ut.Symbol != null
    ? IdentifierName(NameMangler.ToPascalCase(ut.Name))
    : ParseTypeName(ut.Name),
```

**Testing**:
- `union/union_basic_001.spy` + `.expected` -- Create union values, print
- `union/union_generic_001.spy` + `.expected` -- Generic union with type args

**Commit message**: `feat: Emit abstract class hierarchy for union types`

#### Sub-task 2E: Pattern Matching Integration

**Goal**: Connect `UnionCasePattern` in type checker and emitter for matching
on union cases.

Once Feature 1 and this sub-task are both complete:
- `UnionCasePattern` in the type checker validates case name exists in the union
  and field patterns match case fields
- Exhaustiveness checking for unions verifies all cases are covered
- Codegen emits C# type pattern matching against nested classes:

```csharp
// Sharpy: case Ok(value): ...
// C#:     case Result<int, string>.Ok { Value: var value }: ...
//    or:  case Result<int, string>.Ok(var value): ...  (if Deconstruct exists)
```

**Testing**:
- `union/union_match_001.spy` + `.expected`
- `union/union_exhaustive_001.spy` + `.expected`
- `union/union_exhaustive_001.error` -- non-exhaustive match error

**Commit message**: `feat: Add union case pattern matching and exhaustiveness checking`

#### Sub-task 2F: Methods, Unit Cases, Short-Form Constructors

**Goal**: Support methods on unions, unit case singletons, and type-inferred
short-form case constructors.

**Methods**: Emit on the abstract base class. `self` parameter has the union type.

**Unit case singletons**:
```csharp
public sealed class None : Option<T>
{
    public static readonly None Instance = new None();
    private None() { }
}
```

**Short-form constructors** (`Ok(42)` without `Result.Ok(42)`): In the type
checker, when encountering a function call to an unknown name and the expected
type (from annotation or return type) is a union, check if the name matches a
case. If so, resolve it as a qualified case constructor call.

**Testing**:
- `union/union_methods_001.spy` + `.expected`
- `union/union_shortform_001.spy` + `.expected`

**Commit message**: `feat: Add union methods, unit case singletons, and short-form constructors`

### Decision Guidance

**Class hierarchy vs discriminated union struct**

| Approach | Pros | Cons |
|----------|------|------|
| **Abstract class + sealed nested classes** (recommended) | Matches spec exactly; natural C# pattern matching; supports recursive types (`BinaryTree`); `Deconstruct` enables positional patterns | Heap allocation; virtual dispatch overhead |
| **Struct with tag field + overlapping data** | Zero allocation; value semantics | Cannot support recursive types; pattern matching awkward; max size is largest case |
| **Source-generated discriminated union** | Could use Roslyn source generators | Build complexity; not directly emittable via SyntaxFactory |

**Recommendation**: Use the abstract class hierarchy. The spec already defines
this as the lowering strategy. `Result[T, E]` and `Optional[T]` are planned as
structs in Sharpy.Core for performance-critical cases.

**Case constructor resolution: `Ok(42)` vs `Result.Ok(42)`**

The spec supports both forms. The short form is resolved by checking if the
expected type (from annotation or return type) is a union type and the name
matches a case. This is type-directed name resolution.

**Implementation approach**: When the type checker encounters a function call to
an unknown name, check if the expected type is a union, and if the name matches
a case. If so, rewrite internally as a qualified constructor call. Implement
long form first, add short form as a separate step.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Recursive union types (`BinaryTree[T]`) | Medium | High | Class hierarchy naturally supports this; test explicitly |
| Generic union type parameter resolution | High | High | Reuse existing generic type infrastructure from `ClassDef` |
| Short-form case constructors (`Ok(42)`) | Medium | Medium | Implement long form first, add short form separately |
| Missing `Methods` on `UnionDef` AST | Low | Low | Add property before parser work; backward-compatible change |
| Interaction with incremental compilation cache | Medium | Medium | `SymbolSerializer` already handles `UnionType`; test cache invalidation |
| Result/Optional struct vs user-defined union class | Medium | Medium | Keep them separate: Result/Optional stay as structs in Sharpy.Core |

---

## Feature 3: Async/Await

### Context and Motivation

Async/await enables non-blocking I/O and concurrent programming. Sharpy's async
maps directly to C#'s `async`/`await` with `Task<T>`, making the codegen
relatively straightforward. The primary complexity is in semantic analysis
(ensuring `await` is only used inside `async` functions, unwrapping `Task<T>`
to `T`).

The C# compiler handles async state machine generation. Sharpy only needs to
emit the `async` modifier and `await` expressions -- no custom state machine
logic required.

**No dependencies on other Phase 4 features.** Can be developed in parallel
with Features 1 and 2.

**Spec**: `docs/language_specification/async_programming.md`

Key spec observations:
- `async def` maps to C# `async` method returning `Task<T>` or `Task`
- `await expr` maps to C# `await expr`
- `async for` maps to C# `await foreach` over `IAsyncEnumerable<T>`
- `async with` maps to C# `await using`
- Async comprehensions are a **parse error** (not semantic error)
- `await` inside regular comprehensions is also not supported

### Current State Assessment

| What | Where | Status |
|------|-------|--------|
| `async` and `await` keywords | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` lines 67-68 | Lexed as `TokenType.Async` and `TokenType.Await` |
| Keyword map entries | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Lexer.cs` lines 124-125 | Recognized as reserved keywords |
| `AwaitExpression` AST node | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs` lines 27-46 | Complete: `Operand` property, `ValidateInvariants()`, `GetChildNodes()` |
| `TaskType` semantic type | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 753-771 | Complete: `ResultType` (null for `Task`, set for `Task<T>`), `ClrType` mapping |
| `CodeGenInfo.AsyncStateId` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/CodeGenInfo.cs` line 78 | Reserved field (not yet used) |
| `BasicBlock.ContainsAwait` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/BasicBlock.cs` line 59 | Field exists for CFG analysis |
| `IdentifyAsyncRegions()` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs` lines 123-156 | Implemented: identifies blocks containing `await` |
| `AsyncStateRegion` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs` lines 197-201 | Complete: `StateId`, `Blocks`, `AwaitExpression` |
| `AstVisitor` dispatch | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/AstVisitor.cs` lines 140-141, 378, 516, 681 | Dispatched in both void and generic visitors |
| Commented-out `AwaitExpression` case | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` line 141 | Comment: "Await expressions are valid (if we had them in AST)" |
| `SymbolSerializer` for `TaskType` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Project/SymbolSerializer.cs` lines 276-279, 636-643 | Serialization and deserialization implemented |
| `TypeMapper` for `TaskType` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs` lines 112-116 | Maps to `System.Threading.Tasks.Task` / `Task<T>` (fully implemented) |
| `FunctionDef` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` lines 366-399 | No `IsAsync` property |
| `FunctionSymbol` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs` lines 89-107 | No `IsAsync` property |

**Missing scaffolding**:
- `FunctionDef` has no `IsAsync` property -- must be added
- `FunctionSymbol` has no `IsAsync` property -- must be added
- No parser support for `async def` or `await expr`
- No `AsyncForStatement` or `AsyncWithStatement` AST nodes

### Files to Modify

**AST**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` -- Add `bool IsAsync { get; init; }` to `FunctionDef` (after line 374)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs` -- Add `bool IsAsync { get; init; }` to `FunctionSymbol` (after line 96)

**Parser**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs` -- Add `TokenType.Async` handling in `ParseStatement()` and `IsSyncToken()`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs` -- Modify `ParseFunctionDef()` to accept `isAsync` parameter
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs` or `Parser.Primaries.cs` -- Add `await` expression parsing

**Semantic**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` -- Add `_inAsyncFunction` tracking
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` -- Add `AwaitExpression` case
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` -- Update `CheckFunction()` for async return type wrapping
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` -- Add codes in SPY0370-SPY0379

**CodeGen**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` -- Uncomment `AwaitExpression` case (line 141)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` -- Add `AwaitExpression` emission
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` or `.TypeDeclarations.cs` -- Add `async` modifier to method emission

### Step-by-Step Implementation

#### Sub-task 3A: AST Enhancement and Parser

**Goal**: Add `IsAsync` to `FunctionDef`/`FunctionSymbol`, parse `async def`
and `await expr`.

**AST changes**:

In `FunctionDef` (`Statement.cs` line 368):
```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public bool IsAsync { get; init; }  // <-- add here
    // ... rest unchanged
}
```

In `FunctionSymbol` (`Symbol.cs` after line 96):
```csharp
public bool IsAsync { get; init; }
```

**Parser -- `async def`**:

In `ParseStatement()` (`Parser.cs` line 353), add before the switch:
```csharp
if (Current.Type == TokenType.Async)
{
    if (Peek().Type == TokenType.Def)
    {
        Advance(); // consume 'async'
        return ParseFunctionDef(isAsync: true);
    }
    else if (Peek().Type == TokenType.For)
    {
        // async for -- defer to milestone 3
        throw ReportError("'async for' is not yet supported", ...);
    }
    else if (Peek().Type == TokenType.With)
    {
        // async with -- defer to milestone 3
        throw ReportError("'async with' is not yet supported", ...);
    }
    else
    {
        throw ReportError("'async' must be followed by 'def', 'for', or 'with'", ...);
    }
}
```

Modify `ParseFunctionDef()` to accept `bool isAsync = false` parameter and set
`IsAsync = isAsync` on the resulting `FunctionDef`.

**Parser -- `await expr`**:

In the expression parser, handle `TokenType.Await` as a unary prefix operator.
Add to `ParseUnaryExpression()` or `ParseAtom()`:

```csharp
case TokenType.Await:
    var awaitToken = Current;
    Advance(); // consume 'await'
    var operand = ParseUnaryExpression(); // or appropriate precedence level
    return new AwaitExpression
    {
        Operand = operand,
        LineStart = awaitToken.Line,
        ColumnStart = awaitToken.Column,
        Span = GetSpanFromToken(awaitToken),
    };
```

`await` should bind tighter than assignment but looser than most binary
operators. It is similar to `not` in precedence -- place it at the same
level as unary operators.

**Async comprehension rejection**: Per the spec, `async for` inside
comprehensions is a parse error. In the comprehension parser, if
`TokenType.Async` appears, report:
```csharp
throw ReportError("'async' is not valid in this context. "
    + "Async comprehensions are not supported.", ...);
```

**Testing**: Parser unit tests for:
- `async def foo(): pass`
- `await some_task()`
- Error: `async class` (not valid)

**Commit message**: `feat: Parse async def and await expressions`

#### Sub-task 3B: Semantic Analysis

**Goal**: Validate `await` context and handle async return type wrapping.

**Files to modify**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`

**Diagnostic codes** (add to `DiagnosticCodes.Semantic`):
```csharp
// Async/await (SPY0370-SPY0379)
public const string AwaitOutsideAsync = "SPY0370";
public const string AsyncNotFunction = "SPY0371";
public const string AwaitNonTask = "SPY0372";
public const string AsyncComprehension = "SPY0373";
```

**Track async context**: Add a `_inAsyncFunction` boolean field to
`TypeChecker`. Set `true` when entering an `async def`, reset on exit.

**`await` type checking** (add to `CheckExpression` switch in
`TypeChecker.Expressions.cs`):

```csharp
case AwaitExpression awaitExpr:
    if (!_inAsyncFunction)
    {
        AddError("'await' can only be used inside an 'async' function",
            awaitExpr.LineStart, awaitExpr.ColumnStart,
            DiagnosticCodes.Semantic.AwaitOutsideAsync, awaitExpr.Span);
        return SemanticType.Unknown;
    }

    var operandType = CheckExpression(awaitExpr.Operand);
    if (operandType is TaskType taskType)
    {
        var resultType = taskType.ResultType ?? SemanticType.Void;
        _semanticInfo.RecordType(awaitExpr, resultType);
        return resultType;
    }
    else
    {
        AddError($"Cannot await non-task type '{operandType.GetDisplayName()}'",
            awaitExpr.LineStart, awaitExpr.ColumnStart,
            DiagnosticCodes.Semantic.AwaitNonTask, awaitExpr.Span);
        return SemanticType.Unknown;
    }
```

**Async function return type**: When type-checking a `FunctionDef` with
`IsAsync = true`:
- If declared return type is `T`, actual C# return type is `Task<T>`
- If declared return type is `None`/void, actual C# return type is `Task`
- Wrap in `TaskType` for semantic purposes
- Return statements inside async functions expect unwrapped type `T`, not
  `Task<T>` -- C# handles the wrapping automatically

**Commit message**: `feat: Add semantic analysis for async/await`

#### Sub-task 3C: Code Generation

**Goal**: Emit `async` modifier and `await` expressions.

**Async method modifier**: When emitting a `FunctionDef` with `IsAsync = true`,
add the `async` modifier:

```csharp
if (functionDef.IsAsync)
{
    modifiers = modifiers.Add(Token(SyntaxKind.AsyncKeyword));
}
```

**Await expression**: Add to `GenerateExpression()`:

```csharp
case AwaitExpression awaitExpr:
    return SyntaxFactory.AwaitExpression(GenerateExpression(awaitExpr.Operand));
```

**Uncomment** the `AwaitExpression` case in `IsValidCSharpStatementExpression()`
at line 141 of `RoslynEmitter.Statements.cs`.

**Return type wrapping**: The emitted return type for async functions should be
`Task<T>` or `Task`. `TypeMapper` already handles `TaskType` correctly (lines
112-116), so this should work automatically if the semantic phase wraps the
return type in `TaskType`.

**Testing**:
- `async/async_basic_001.spy` + `.expected` -- Simple async function
- `async/async_await_001.spy` + `.expected` -- Awaiting a task
- `async/async_return_001.spy` + `.expected` -- Returning from async

**Commit message**: `feat: Emit async modifier and await expressions`

#### Sub-task 3D: Error Diagnostics and Validation

**Goal**: Comprehensive error reporting for async misuse.

- Error: `await` outside async function (SPY0370)
- Error: `async` on non-function (SPY0371)
- Error: `await` on non-task expression (SPY0372)
- Error: `async for` / `await` inside comprehensions (SPY0373, parse error)
- Optional warning: `async def` without any `await` inside

**Testing**:
- `async/async_await_outside.spy` + `.error`
- `async/async_non_task.spy` + `.error`
- `async/async_comprehension.spy` + `.error`

**Commit message**: `feat: Add async/await error diagnostics`

#### Sub-task 3E: `async for` and `async with`

**Goal**: Support `async for` (C# `await foreach`) and `async with`
(C# `await using`).

**AST**: Either add `IsAsync` flag to `ForStatement` and `WithStatement` (if
it exists by then), or create new `AsyncForStatement` / `AsyncWithStatement`
nodes in `Statement.Future.cs`. The flag approach is simpler and recommended.

**Parser**: In `ParseStatement()`, the `async for` case consumes `async`, then
calls `ParseForStatement(isAsync: true)`.

**Semantic**: Validate that the iterable implements `IAsyncEnumerable<T>`.

**CodeGen**:
- `async for`: Emit `await foreach` via
  `ForEachStatement(...).WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))`
- `async with`: Emit `await using` via
  `UsingStatement(...).WithAwaitKeyword(Token(SyntaxKind.AwaitKeyword))`

**Testing**:
- `async/async_for_001.spy` + `.expected`
- `async/async_with_001.spy` + `.expected`

**Commit message**: `feat: Add async for and async with support`

#### Sub-task 3F: CFG and Async Generators

**Goal**: Update control flow graph for await points and support async
generators.

**CFG**: Update `ControlFlowGraphBuilder` to mark blocks containing `await`.
The scaffolding in `BasicBlock.ContainsAwait` and
`ControlFlowAnalysis.IdentifyAsyncRegions()` already exists.

**Async generators**: `async def` with `yield` returns `AsyncIterator[T]`,
which maps to `IAsyncEnumerable<T>`. This requires recognizing the combination
of `IsAsync` and `yield` in the function body.

**Testing**:
- `async/async_generator_001.spy` + `.expected`

**Commit message**: `feat: Add async CFG support and async generators`

### Decision Guidance

**`async def` syntax: keyword on `FunctionDef` vs separate AST node**

| Approach | Pros | Cons |
|----------|------|------|
| **`IsAsync` flag on `FunctionDef`** (recommended) | Minimal AST change; reuses all function infrastructure; matches C# modeling | Slightly less type-safe |
| **Separate `AsyncFunctionDef` node** | Type-safe; explicit in AST | Duplicates all FunctionDef handling; significant code duplication |

**Recommendation**: Use `IsAsync` flag. C# itself models async as a modifier,
not a different declaration kind.

**State machine generation**

C# generates async state machines from `async`/`await` keywords. Sharpy does
NOT need its own state machine. The `ControlFlowAnalysis.IdentifyAsyncRegions()`
scaffolding and `CodeGenInfo.AsyncStateId` are for potential future
optimizations but are not needed for basic async support.

**Recommendation**: Ignore state machine scaffolding. Emit `async` and `await`
keywords and let the C# compiler handle everything.

**Task vs ValueTask**

| Type | Use Case | Notes |
|------|----------|-------|
| **`Task<T>`** (recommended for v0.2.0) | General async return type | Standard; well-understood; compatible with all .NET APIs |
| **`ValueTask<T>`** | Hot-path async that often completes synchronously | Better perf for sync-completing paths; more complex lifetime rules |

Start with `Task<T>`. Add `ValueTask<T>` support later if perf analysis justifies it.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `await` precedence in expression parsing | Medium | Medium | Test with complex expressions: `x = await foo() + 1` should be `x = (await foo()) + 1` |
| Return type wrapping confusion | Medium | Medium | Semantic analysis uses unwrapped types; codegen wraps in `Task<T>` |
| Async lambda support | Low | Low | Defer to later version; lambda parsing would need `async` prefix |
| `asyncio` module in Sharpy.Core | Low | Low | Start with basic async/await; `asyncio.gather` etc. added incrementally |
| ControlFlowValidator interaction | Medium | Medium | Async methods returning `Task` (void) don't need explicit returns |

---

## Feature 4: Generic Variance

### Context and Motivation

Generic variance (`in`/`out` annotations on type parameters) enables safe
substitution of generic types. `IProducer[Dog]` can be used where
`IProducer[Animal]` is expected if `T` is covariant (`out`). This is a
fundamental .NET feature that enables idiomatic use of interfaces and delegates.

This is the smallest of the four features in terms of code volume, but it
touches the core type system assignability logic.

**No dependencies on other Phase 4 features.** Can be developed independently.

**Spec**: `docs/language_specification/generic_variance.md`

Key spec observations:
- Variance only valid on **interface** and **delegate** type parameters
- `out T` = covariant (only in output/return positions)
- `in T` = contravariant (only in input/parameter positions)
- Nested variance flips: covariant in contravariant = contravariant
- Built-in .NET interfaces already have variance (`IEnumerable<out T>`, etc.)

### Current State Assessment

| What | Where | Status |
|------|-------|--------|
| `TypeParameterDef` record | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` lines 571-586 | Has `Name` and `Constraints` but NO variance field |
| `TypeParameterType` semantic type | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 658-680 | Has `Name`, `DeclaringType`, `Constraints` but NO variance field |
| Variance TODO in `GenericType.IsAssignableTo` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` line 218 | Comment: "Check covariance/contravariance rules here in future" |
| Variance comment in `TypeChecker.Utilities.cs` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` line 141 | Comment: "extends the basic IsAssignableTo to handle nullable types and generic variance" |
| Hardcoded covariance for `list`/`set` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` lines 166-178 | Special-cased: `list[Dog]` assignable to `list[Animal]` (hardcoded, not general) |
| `FunctionType` variance | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` lines 537-548 | Parameter contravariance and return covariance already implemented for function types |
| `TokenType.In` | `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` line 39 | Already a keyword (used in `for x in y`) |

**Missing scaffolding**:
- No `Variance` field on `TypeParameterDef` or `TypeParameterType`
- No `TypeParameterVariance` enum
- No parser support for `out T` or `in T` in type parameter lists
- No position checking (covariant type parameter used in input position)
- No general variance-aware assignability in `GenericType.IsAssignableTo`

### Files to Modify

**AST/Types**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` -- Add `TypeParameterVariance` enum and `Variance` property to `TypeParameterDef`
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` -- Add `Variance` property to `TypeParameterType`

**Parser**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs` -- Modify `ParseTypeParameters()` to detect `out`/`in` before parameter name

**Semantic**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` -- Update `GenericType.IsAssignableTo` with variance logic
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` -- Replace hardcoded `list`/`set` covariance with general algorithm
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` -- Add codes in SPY0380-SPY0389

**Validation**:
- New file or existing: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Validation/VarianceValidator.cs` -- Position checking for variant type parameters

**CodeGen**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` -- Add `out`/`in` modifier to type parameter emission

### Step-by-Step Implementation

#### Sub-task 4A: AST and Type System Changes

**Goal**: Add `TypeParameterVariance` enum and `Variance` property to relevant
records.

In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs`,
add near `TypeParameterDef` (around line 570):

```csharp
/// <summary>
/// Variance annotation for a type parameter.
/// </summary>
public enum TypeParameterVariance
{
    /// <summary>No annotation -- exact match required.</summary>
    Invariant,

    /// <summary>out T -- type flows out (returned), more derived allowed.</summary>
    Covariant,

    /// <summary>in T -- type flows in (consumed), less derived allowed.</summary>
    Contravariant
}
```

Add to `TypeParameterDef`:
```csharp
public TypeParameterVariance Variance { get; init; } = TypeParameterVariance.Invariant;
```

In `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs`,
add to `TypeParameterType` (around line 660):
```csharp
public TypeParameterVariance Variance { get; init; } = TypeParameterVariance.Invariant;
```

**Commit message**: `feat: Add TypeParameterVariance enum and Variance property`

#### Sub-task 4B: Parser -- `out T` and `in T`

**Goal**: Parse variance annotations in type parameter lists.

**File**: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs`

Find the `ParseTypeParameters()` method and modify it to check for variance
keywords before the type parameter name:

```csharp
// Inside ParseTypeParameters(), before parsing the type parameter name:
var variance = TypeParameterVariance.Invariant;

if (Current.Type == TokenType.Identifier && Current.Value == "out")
{
    variance = TypeParameterVariance.Covariant;
    Advance(); // consume 'out'
}
else if (Current.Type == TokenType.In)
{
    // TokenType.In is already a keyword token (for x in y)
    // In type parameter context, it means contravariance
    variance = TypeParameterVariance.Contravariant;
    Advance(); // consume 'in'
}

var paramName = Expect(TokenType.Identifier, "type parameter name").Value;

// ... parse constraints ...

typeParams.Add(new TypeParameterDef
{
    Name = paramName,
    Variance = variance,
    Constraints = constraints,
    // ...
});
```

**Important**: `out` is parsed as a contextual identifier (not a keyword). `in`
is `TokenType.In` which is already a keyword. Inside `ParseTypeParameters()`,
seeing `TokenType.In` means contravariance, not "membership test". This
contextual parsing is safe because `in` as a membership test never appears
inside `[...]` type parameter lists.

**Testing**: Parser tests for:
- `interface IProducer[out T]: ...`
- `interface IConsumer[in T]: ...`
- `interface IConverter[in TIn, out TOut]: ...`

**Commit message**: `feat: Parse out/in variance annotations on type parameters`

#### Sub-task 4C: Semantic -- Restriction Validation

**Goal**: Validate that variance annotations are only used on interfaces and
delegates, and that variant type parameters appear only in legal positions.

**Diagnostic codes** (add to `DiagnosticCodes.Semantic`):
```csharp
// Generic variance (SPY0380-SPY0389)
public const string InvalidVariancePosition = "SPY0380";
public const string VarianceOnNonInterface = "SPY0381";
```

**Restrictions** (per spec):
1. Variance only valid on **interface** and **delegate** type parameters.
   Classes and structs must be invariant.
2. **Covariant (`out T`)**: T can only appear in return types (output) and
   covariant positions of other types.
3. **Contravariant (`in T`)**: T can only appear in parameter types (input) and
   contravariant positions of other types.
4. **Nested variance flips**: Entering a contravariant-position generic type
   parameter flips the expected position.

**Implementation**: Add variance checking in the type checker when processing
interface definitions:

```csharp
private void CheckVariancePosition(
    SemanticType type,
    VariancePosition expectedPosition,
    List<TypeParameterDef> typeParams)
{
    if (type is TypeParameterType tpt)
    {
        var matchingDef = typeParams.FirstOrDefault(tp => tp.Name == tpt.Name);
        if (matchingDef != null && matchingDef.Variance != TypeParameterVariance.Invariant)
        {
            if (matchingDef.Variance == TypeParameterVariance.Covariant
                && expectedPosition == VariancePosition.Contravariant)
            {
                AddError($"Type parameter '{tpt.Name}' is covariant but "
                    + "appears in contravariant position", ...);
            }
            if (matchingDef.Variance == TypeParameterVariance.Contravariant
                && expectedPosition == VariancePosition.Covariant)
            {
                AddError($"Type parameter '{tpt.Name}' is contravariant but "
                    + "appears in covariant position", ...);
            }
        }
    }
    else if (type is GenericType gt && gt.GenericDefinition != null)
    {
        // For nested generics, flip variance when entering contravariant positions
        for (int i = 0; i < gt.TypeArguments.Count; i++)
        {
            var paramDef = gt.GenericDefinition.TypeParameters[i];
            var nestedPosition = paramDef.Variance == TypeParameterVariance.Contravariant
                ? Flip(expectedPosition)
                : expectedPosition;
            CheckVariancePosition(gt.TypeArguments[i], nestedPosition, typeParams);
        }
    }
}

private enum VariancePosition { Covariant, Contravariant }

private static VariancePosition Flip(VariancePosition pos)
    => pos == VariancePosition.Covariant
        ? VariancePosition.Contravariant
        : VariancePosition.Covariant;
```

For each interface method, check:
- Parameter types in `Contravariant` position
- Return type in `Covariant` position

**Error for classes/structs with variance**: If `TypeParameterDef.Variance` is
non-invariant on a `ClassDef` or `StructDef`, report SPY0381.

**Testing**:
- Error test: covariant type parameter in input position
- Error test: variance on class type parameter
- Valid: variance on interface type parameter

**Commit message**: `feat: Add variance position validation for type parameters`

#### Sub-task 4D: Variance-Aware Assignability

**Goal**: Replace hardcoded `list`/`set` covariance with general variance
checking.

**Files to modify**:
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` -- Update `GenericType.IsAssignableTo` (around line 214)
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` -- Update `IsAssignable` (around line 166)

**Replace the hardcoded check** at `TypeChecker.Utilities.cs` lines 166-178:

```csharp
// Before (hardcoded):
if (sourceGeneric.Name == "list" || sourceGeneric.Name == "set")
{
    return IsAssignable(sourceGeneric.TypeArguments[0], targetGeneric.TypeArguments[0]);
}

// After (general):
if (sourceGeneric.GenericDefinition != null)
{
    bool allMatch = true;
    for (int i = 0; i < sourceGeneric.TypeArguments.Count; i++)
    {
        var typeParamDef = sourceGeneric.GenericDefinition.TypeParameters[i];
        var sourceArg = sourceGeneric.TypeArguments[i];
        var targetArg = targetGeneric.TypeArguments[i];

        switch (typeParamDef.Variance)
        {
            case TypeParameterVariance.Covariant:
                if (!IsAssignable(sourceArg, targetArg))
                    allMatch = false;
                break;
            case TypeParameterVariance.Contravariant:
                if (!IsAssignable(targetArg, sourceArg))
                    allMatch = false;
                break;
            case TypeParameterVariance.Invariant:
                if (!sourceArg.Equals(targetArg))
                    allMatch = false;
                break;
        }
        if (!allMatch) break;
    }
    return allMatch;
}
```

**Note**: The existing `list`/`set` covariance behavior must be preserved.
Since `list` and `set` are built-in generic types (not user-defined with
variance annotations), their `GenericDefinition` may not have variance set.
Options: (a) set variance on the built-in type parameters, or (b) keep a
fallback that treats built-in collections as covariant. Option (a) is cleaner.

**Testing**:
- `IProducer[Dog]` assignable to `IProducer[Animal]` (covariant)
- `IConsumer[Animal]` assignable to `IConsumer[Dog]` (contravariant)
- `IMutable[Dog]` NOT assignable to `IMutable[Animal]` (invariant)
- Existing `list`/`set` tests still pass

**Commit message**: `feat: Implement general variance-aware type assignability`

#### Sub-task 4E: Code Generation

**Goal**: Emit `out`/`in` modifiers on C# type parameters.

**File**: `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`

When emitting interface type parameters, add the variance modifier:

```csharp
var typeParam = TypeParameter(Identifier(paramName));

if (variance == TypeParameterVariance.Covariant)
{
    typeParam = typeParam.WithVarianceKeyword(
        Token(SyntaxKind.OutKeyword));
}
else if (variance == TypeParameterVariance.Contravariant)
{
    typeParam = typeParam.WithVarianceKeyword(
        Token(SyntaxKind.InKeyword));
}
```

This is the simplest codegen change among all four features -- just adding a
keyword modifier to type parameter declarations.

**Testing**:
- `variance/variance_covariant_001.spy` + `.expected`
- `variance/variance_contravariant_001.spy` + `.expected`
- `variance/variance_mixed_001.spy` + `.expected`
- `variance/variance_covariant_001.expected.cs` -- C# snapshot test

**Commit message**: `feat: Emit out/in variance modifiers on C# type parameters`

### Decision Guidance

**`in` keyword conflict**

`in` is already `TokenType.In` (used in `for x in y`). Options:

| Approach | Pros | Cons |
|----------|------|------|
| **Contextual parsing** (recommended) | No new token type; `in` means variance only inside `[...]` type param lists | Requires context tracking in parser |
| **New token `TokenType.InVariance`** | Explicit token type | Lexer cannot distinguish without context |
| **Reuse `TokenType.In`** | Simple | Already means "membership test" |

**Recommendation**: Contextual parsing. Inside `ParseTypeParameters()`, when
`TokenType.In` is followed by an identifier, interpret as contravariance. This
is how C# itself handles contextual keywords.

**Should `FunctionType` variance be connected to generic variance?**

No. `FunctionType` already implements structural variance (parameter
contravariance, return covariance) in its `IsAssignableTo` method. This is
independent of declared generic variance. The two systems are complementary:
- `FunctionType` variance is structural (built into the type system)
- Generic variance is declared (annotated by the programmer)

No changes needed to `FunctionType`.

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `in` keyword conflict with `for x in y` | Medium | Medium | Contextual parsing in type parameter lists only |
| Nested variance flipping | Medium | Medium | Implement and test the flip algorithm; test `IConsumer[IProducer[T]]` |
| Removing hardcoded `list`/`set` covariance | Medium | Medium | Verify existing tests pass; may need to set variance on built-in type params |
| Interaction with generic type inference | Low | Low | Variance does not affect inference -- only assignability |
| Delegate type parameters | Medium | Medium | Check if delegates are fully implemented before adding variance |

---

## Dependency Graph and Recommended Order

```
Feature 4 (Generic Variance)          Feature 3 (Async/Await)
        |                                      |
  (independent)                          (independent)
        |                                      |
        v                                      v
   Can ship alone                        Can ship alone


Feature 1 (Match Statement)
        |
        | (Feature 2 depends on Feature 1)
        v
Feature 2 (Tagged Unions)
        |
        | (Union pattern matching requires both)
        v
   Full union + match integration
```

**Recommended implementation order**:
1. **Generic Variance** (smallest scope, fewest dependencies, can ship first)
2. **Match Statement** (medium scope, blocks tagged unions)
3. **Async/Await** (independent, can be developed in parallel with Match)
4. **Tagged Unions** (largest scope, depends on match statement)

Features 1 and 3 can be developed in parallel by different engineers. Feature 4
can be done by anyone at any time. Feature 2 should wait until Feature 1
Milestone 3 (guard conditions) is complete.

---

## Cross-Cutting Concerns

### Incremental Compilation Cache

All four features create new AST nodes and semantic types. The
`SymbolSerializer` already handles `UnionType` and `TaskType`. When adding new
symbol kinds or semantic types, update:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Project/SymbolSerializer.cs` -- `SerializeType()` for new `SemanticType` subclasses
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Project/SymbolSerializer.cs` -- `ResolveTypeFromId()` for deserialization
- Bump the schema version in `SymbolCache` to invalidate old caches

### ControlFlowGraphBuilder

Match statements and async/await both need CFG updates:
- **Match**: Each case is a branch (similar to if/elif/else chains, which the
  CFG already handles)
- **Async**: Await points split basic blocks (scaffolding exists in
  `BasicBlock.ContainsAwait` and `ControlFlowAnalysis.IdentifyAsyncRegions()`)

### AstVisitor

All new AST nodes are already registered in `AstVisitor.cs`. No changes needed
for the visitor infrastructure itself. Any code that uses `AstVisitor` to
traverse the entire AST (unused variable detection, unused import detection)
will automatically traverse new nodes via `GetChildNodes()`.

### DiagnosticRenderer

No changes needed -- the renderer works with any diagnostic code in the `SPY`
prefix format.

### Diagnostic Code Allocation

| Feature | Range | Example Codes |
|---------|-------|---------------|
| Match statement | SPY0350-SPY0359 | `NonExhaustiveMatch`, `IncompatiblePattern`, `DuplicatePattern`, `OrPatternVariableMismatch` |
| Tagged unions | SPY0360-SPY0369 | `DuplicateUnionCase`, `InvalidUnionMethod`, `UnknownUnionCase` |
| Async/await | SPY0370-SPY0379 | `AwaitOutsideAsync`, `AsyncNotFunction`, `AwaitNonTask`, `AsyncComprehension` |
| Generic variance | SPY0380-SPY0389 | `InvalidVariancePosition`, `VarianceOnNonInterface` |

---

## General Guidance

### Implementation Order

For every feature, implement components in this order:

```
Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
```

This ensures dependencies flow forward. You can commit after each phase if the
intermediate state compiles (even if the feature is not yet end-to-end
functional).

### Key Principles

1. **Start with the simplest working case, then extend.** For match statements,
   start with literal patterns and wildcards before tackling type patterns or
   exhaustiveness. For async, start with `async def` / `await` before
   `async for`.

2. **Keep placeholders for unimplemented branches.** Use
   `throw new NotImplementedException("Match expression: list patterns not yet supported")`
   rather than silently ignoring a case. This prevents silent correctness bugs
   and makes progress visible.

3. **Use the existing AST nodes.** `Statement.Future.cs`, `Expression.Future.cs`,
   and `Pattern.cs` define the AST structure. The design work is done -- you are
   producing and consuming these nodes, not redesigning them.

4. **AST nodes are immutable records.** Annotations and computed data go in
   `SemanticInfo` (or `SemanticBinding`), not on the AST node itself. The
   exception is parser-level data like source locations and literal values.

5. **RoslynEmitter uses SyntaxFactory exclusively.** Never use string
   interpolation or template strings to generate C# code. Always construct the
   syntax tree using `SyntaxFactory` methods:
   ```csharp
   // Correct:
   SwitchStatement(scrutinee, List(sections))

   // Incorrect:
   var code = $"switch ({scrutinee}) {{ ... }}";
   ```

6. **Sharpy.Core targets C# 9.0 / netstandard2.1.** Generated code must also
   work within these constraints. Record types and `init` accessors are
   available. File-scoped namespaces and global usings are NOT available.

7. **When in doubt, look at existing patterns.** For match, study how
   `ParseIfStatement()` and `ParseForStatement()` work. For async, study how
   `GenerateMethod()` handles modifiers. For variance, study how
   `ParseTypeParameters()` handles constraints.

8. **Never modify `.expected` files to make tests pass.** Fix the implementation
   instead.

9. **Verify Python behavior first** when implementing Python-like semantics. Run
   `python3 -c "..."` to confirm expected behavior before coding.

10. **Language spec is authoritative.** If the spec says one thing and the code
    does another, change the code.

11. **Add diagnostic codes for new errors.** All error messages need a code from
    `DiagnosticCodes.cs`. Follow the allocated ranges above.

12. **TODO/BUG/FIXME comments must have GitHub issues.** When deferring
    functionality, create an issue first and reference it:
    `// TODO(#123): Support positional patterns in match statements`.

### Testing Strategy

For each sub-task:

1. **Unit tests** for the specific component (parser tests, type checker tests,
   codegen tests).
2. **Integration test fixtures** (`.spy` + `.expected` pairs) that verify
   end-to-end behavior. Place in
   `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/{feature_name}/`.
3. **Error test fixtures** (`.spy` + `.error` pairs) for invalid input that
   should produce specific error messages.
4. **C# snapshot tests** (`.expected.cs` for a few representative cases to
   detect codegen regressions).
5. Run `dotnet test` to verify nothing regresses.
6. Run `dotnet format whitespace` before committing.

### File Reference

Key files referenced across multiple features:

| File | Purpose |
|------|---------|
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` | AST statement nodes |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.Future.cs` | Future AST nodes (match, union) |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.cs` | AST expression nodes |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.Future.cs` | Future expression nodes (await, match expr) |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Pattern.cs` | Pattern AST nodes |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Types.cs` | Type annotation AST |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs` | Main parser (statement dispatch, line 353) |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs` | Class/struct/function parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Statements.cs` | Control flow statement parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs` | Expression parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Primaries.cs` | Literal and primary parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs` | Type annotation and type parameter parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs` | Symbol hierarchy |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` | Type hierarchy |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/NameResolver.cs` | Name resolution pass |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Type checking pass (CheckStatement at line 290) |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs` | Expression type checking |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Statements.cs` | Statement type checking |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs` | Type assignability and inference utilities |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Emitter entry, variable name collection |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | Class member codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` | Function/type codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Expression codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` | Statement codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Sharpy-to-C# type mapping |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/NameMangler.cs` | Name mangling rules |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` | Error code catalog |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` | Token type enum |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Lexer.cs` | Lexer keyword map |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowGraphBuilder.cs` | Control flow graph construction |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Analysis/ControlFlow/ControlFlowAnalysis.cs` | Async region analysis |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Project/SymbolSerializer.cs` | Incremental compilation cache |
