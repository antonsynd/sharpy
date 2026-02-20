<!-- TODO(#209): Update this file to mark Phase 6+7 features as complete -->
<!-- Verified by /project:verify-plan on 2026-02-17 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Sharpy Language Feature Completeness — Phased Roadmap

## Context

Sharpy's v0.1.x series (16 phases, v0.1.0–v0.1.15) is complete, delivering the core pipeline: lexer, parser, semantic analysis, codegen, all basic types, classes/structs/interfaces/enums, generics with constraints, lambdas/delegates, collections with comprehensions, exception handling, module system, and 29 dunders.

Implementation plans Phase 1–5 were drafted post-v0.1.x. Several items from those plans have been completed (properties, `with` statement, match statement basics, walrus codegen, multi-for comprehensions, named tuples, pipe forward, generic constraints). This roadmap picks up what remains and adds everything else needed for language spec completeness (syntax + semantics, not stdlib).

**Source of truth:** `docs/language_specification/` (108 spec files + grammar.ebnf.txt)

---

## Status Audit: What's Done vs. Missing

### Completed (beyond v0.1.x)
- Properties (auto + function-style with get/set/init)
- `with` statement (IDisposable -> `using`)
- Match statement (literal, binding, wildcard, tuple, guard patterns)
- Walrus operator (`:=`) full stack
- Generic constraints (`T: IFoo`, `T: class`, `T: struct`, `T: new()`)
- Multi-for comprehensions (list, set, dict)
- Named tuples (`tuple[x: float, y: float]`)
- Pipe forward (`|>`) codegen
- Collection type wrappers (Sharpy.List, Sharpy.Set, Sharpy.Dict)
- `try`/`maybe` expressions with Result/Optional types
- Comparison chains, null-conditional/coalescing, type narrowing

### Missing (grouped by phase below)

---

## Phase 6 — v0.2.0: Correctness & Completion

**Goal:** Finish remaining items from Phase 1–5 plans; solidify the existing feature set before adding new syntax.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~6.1~~ | ~~`@final` decorator on classes/methods~~ | ~~S~~ | ~~Already completed~~ [CORRECTED: `@final` is already fully handled — `DecoratorNames.Final` maps to `SealedKeyword` in both `RoslynEmitter.TypeDeclarations.cs` and `RoslynEmitter.ClassMembers.cs`] |
| 6.2 | `raise X from Y` (inner exception) | M | Parser populates `RaiseStatement.Cause` but semantic+codegen ignore it; pass as inner exception ctor arg |
| ~~6.3~~ | ~~`int * str` string repetition (reversed)~~ | ~~S~~ | ~~Already completed~~ [CORRECTED: Both `str * int` and `int * str` are handled in `TypeInferenceService.cs` (lines 194-200) AND in codegen via `GenerateStringRepetition` in `RoslynEmitter.Expressions.Operators.cs` (lines 172-179)] |
| ~~6.4~~ | ~~`to` operator precedence~~ | ~~M~~ | ~~Already correct~~ [CORRECTED: `to` is already parsed at the correct level in `ParseCast()` (Parser.Expressions.cs:421), between pipe and comparisons, matching `docs/language_specification/type_casting.md`] |
| ~~6.5~~ | ~~Interface default methods~~ | ~~M~~ | ~~Already supported~~ [CORRECTED: SPY0251 fires when interface methods have NO body (missing `...`/`pass`). Methods with real bodies (default implementations) are already explicitly allowed by `NameResolver.ValidateInterfaceMethod()`] |
| 6.6 | Constructor chaining (`self.__init__()`) | M | Detect `self.__init__()` in constructor body; emit `ThisConstructorInitializer` (`: this(...)`). Note: `super().__init__()` → `: base(...)` already works. |
| 6.7 | Enum `.name`, iteration | M | `.name` → `.ToString()` or `Enum.GetName()`, `for x in Enum:` → `Enum.GetValues()` [CORRECTED: `.value` is already implemented for integer enums via cast in `RoslynEmitter.Expressions.Access.cs`] |
| 6.8 | Generic type aliases (`type Cb[T] = (T) -> None`) | M | Add `TypeParameters` to `TypeAlias` AST; handle substitution in TypeResolver |
| 6.9 | Method overloading (user-defined) | L | `Scope.Define()` currently throws `InvalidOperationException` on duplicate non-variable symbols [CORRECTED: it throws, not silently overwrites]; add `MethodOverloads` to TypeSymbol; overload resolution in TypeChecker |

**Key files:** `RoslynEmitter.TypeDeclarations.cs`, `RoslynEmitter.ClassMembers.cs`, `NameResolver.cs`, `TypeChecker.cs`, `TypeInferenceService.cs`, `Parser.Expressions.cs`

---

## Phase 7 — v0.2.1: Destructuring & Spread

**Goal:** Complete tuple unpacking and the spread operator — foundational for `*args` call-site spreading and collection manipulation.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 7.1 | Complex tuple unpacking | M | `a, (b, c) = nested` — nested targets beyond simple identifiers (currently SPY0517) |
| 7.2 | Rest patterns in unpacking | M | `first, *rest = items` and `first, *middle, last = items`; `rest` typed as `list[T]` |
| 7.3 | Tuple unpacking in comprehensions | M | `[a for a, b in pairs]` — currently SPY0516; lower to destructuring in LINQ lambda parameter |
| 7.4 | Spread in collection literals | L | `[*a, *b]`, `{*s1, *s2}`, `{**d1, **d2}`, `(*t1, *t2)` — new `SpreadExpression` AST node + codegen lowering to `AddRange`/`UnionWith`/dict merge |
| 7.5 | Spread in function calls | L | `f(*args_list)`, `f(**kwargs_dict)` — call-site unpacking with static type checking |

**Key files:** `Parser.Expressions.cs`, `RoslynEmitter.Expressions.Literals.cs`, `RoslynEmitter.Expressions.cs`, `TypeChecker.Expressions.cs`

**Dependencies:** 7.2 enables idiomatic Python patterns. 7.4 + 7.5 together complete the spread operator spec.

---

## Phase 8 — v0.2.2: Pattern Matching & Tagged Unions

**Goal:** Complete algebraic data type support — the highest-impact v0.2.x feature, enabling idiomatic Sharpy.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 8.1 | Match expression (expression form) | M | `result = match x: case ...: expr` → C# switch expression. `MatchExpression` AST already in Future.cs |
| 8.2 | Or-patterns | S | `case "a" \| "b":` — `OrPattern` AST exists; wire through parser + codegen |
| 8.3 | Type patterns with binding | M | `case int() as n:` — `TypePattern` + `BindingPattern` AST exists; wire parser + codegen |
| 8.4 | Relational patterns | M | `case > 0:` — new `RelationalPattern` AST node; parser + codegen → C# relational pattern |
| 8.5 | Property/positional patterns | M | `case Point(x=0):` and `case Point(0, y):` — match on type properties; requires `Deconstruct` awareness |
| 8.6 | Tagged union declarations (`union`) | XL | `UnionDef` AST exists in Future.cs; full parser + semantic + codegen. Lower to abstract base class + sealed nested case classes with `Deconstruct` methods |
| 8.7 | Union case patterns in match | M | `case Ok(value):` — destructure union cases; infer short constructor names from match subject type |
| 8.8 | Exhaustiveness checking | L | New `ExhaustivenessValidator` for enums, `bool`, tagged unions; wildcard `_` satisfies remainder |

**Key files:** `Parser.Statements.cs`, `Parser.Expressions.cs`, `Pattern.cs`, `Statement.Future.cs`, `TypeChecker.Statements.cs`, `RoslynEmitter.Statements.cs`, `RoslynEmitter.TypeDeclarations.cs`

**Dependencies:** 8.1–8.5 are independent pattern completions. 8.6 (tagged unions) must precede 8.7 (union case patterns). 8.8 depends on all patterns being parseable.

---

## Phase 9 — v0.2.3: Generators & Iterators

**Goal:** Add `yield`/`yield from` for lazy sequence generation — prerequisite for async generators in Phase 10.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 9.1 | `yield` statement | L | New `YieldStatement` AST node; parser + semantic (function becomes generator) + codegen (C# `yield return`) |
| 9.2 | `yield from` delegation | M | `yield from other_iter` → C# `foreach (var x in other) yield return x;` |
| 9.3 | Generator return type inference | M | If function body contains `yield`, infer return type `Iterator[T]` → `IEnumerable<T>` |

**Key files:** New AST nodes in `Statement.cs`, `Parser.Statements.cs`, `TypeChecker.Definitions.cs`, `RoslynEmitter.Statements.cs`

**Dependencies:** 9.1 → 9.2 → 9.3 (linear chain). Must complete before Phase 10.5 (async generators).

---

## Phase 10 — v0.2.4: Async/Await

**Goal:** Full async support — the last major language feature.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 10.1 | `async def` functions | L | Parser recognizes `async` prefix on `def`; semantic marks function as async; return type wrapped in `Task<T>` |
| 10.2 | `await` expressions | L | `AwaitExpression` AST already exists; wire parser + semantic (operand must be `Task<T>`, result is `T`) + codegen (C# `await`) |
| 10.3 | `async for` loops | M | `async for item in aiter:` → C# `await foreach`; operand must be `IAsyncEnumerable<T>` |
| 10.4 | `async with` statements | M | `async with resource() as r:` → C# `await using`; operand must be `IAsyncDisposable` |
| 10.5 | Async generators | L | `async def` + `yield` → `AsyncIterator[T]` → `IAsyncEnumerable<T>` (depends on Phase 9) |
| 10.6 | `Task.WhenAll` mapping | M | Map `asyncio.gather(*tasks)` or equivalent to `Task.WhenAll()`; concurrent task execution |

**Key files:** `Expression.Future.cs` (AwaitExpression), `Parser.Definitions.cs`, `Parser.Statements.cs`, `TypeChecker.cs`, `RoslynEmitter.Statements.cs`, `RoslynEmitter.Expressions.cs`

**Dependencies:** 10.1+10.2 are the foundation. 10.3–10.4 build on them. 10.5 requires Phase 9 (generators).

---

## Phase 11 — v0.2.5: Advanced Function Features

**Goal:** Complete the function parameter system and add partial application.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 11.1 | Positional-only parameters (`/`) | M | `def f(a: T, /)` — params before `/` are positional-only; enforce at call sites during type checking |
| 11.2 | Keyword-only parameters (`*`) | M | `def f(*, b: T)` — params after bare `*` require keyword syntax at call sites |
| 11.3 | `@kwargs` decorator | L | Compiler-understood transforming decorator; generates typed kwargs struct + method overload |
| 11.4 | `@dynamic_kwargs` decorator | L | Enables `**kwargs: dict[str, T]` parameter on decorated function; explicit opt-in for dynamic typing |
| 11.5 | Partial application | L | `f(5, _)` → lambda; new `PlaceholderExpression` AST; operator sections `(_ * 2)`, `(_ > 0)`, `(-_)` |

**Key files:** `Parser.Definitions.cs`, `Parser.Expressions.cs`, `TypeChecker.Expressions.cs`, `RoslynEmitter.Expressions.cs`, `ParameterSymbol`

**Dependencies:** 11.1+11.2 together. 11.3+11.4 together. 11.5 independent.

---

## Phase 12 — v0.2.6: Type System Advances & Polish

**Goal:** Generic variance, delegate declarations, events, and final gap-filling.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| 12.1 | Delegate type declarations | M | `delegate Factory[out T]() -> T` — new AST node; parser + codegen emit C# delegate type |
| 12.2 | Generic variance (`out T`, `in T`) | L | Covariant/contravariant on interfaces and delegates; compiler validates position correctness (return vs. parameter positions) |
| 12.3 | Events | L | `event clicked: (object, EventArgs) -> None`; `+=`/`-=` subscribe; `?.invoke()` thread-safe invocation; only declaring class can fire |
| 12.4 | Nested comprehensions | S | Comprehension inside comprehension (currently SPY0515); lower to nested LINQ |
| 12.5 | Custom decorator arguments | M | Extend `Decorator` record with `Arguments`; parse `@decorator(args)`; attribute mapping |
| 12.6 | Spec gap audit + integration test sweep | M | Systematic pass through all 108 spec files vs. test fixtures; file issues for any remaining gaps |

**Key files:** New AST nodes, `Parser.Definitions.cs`, `Parser.Types.cs`, `TypeChecker.cs`, `RoslynEmitter.TypeDeclarations.cs`, `RoslynEmitter.Expressions.Literals.cs`

**Dependencies:** 12.1 before 12.2 (variance needs delegates to exist). 12.3–12.6 independent.

---

## Phase Summary

| Phase | Version | Theme | Items | Key Deliverables |
|-------|---------|-------|-------|-----------------|
| **6** | v0.2.0 | Correctness & Completion | 5 (was 9; 4 already done) | `raise from`, constructor chaining, enum polish, generic type aliases, method overloading [CORRECTED: reduced from 9 to 5 items — 6.1, 6.3, 6.4, 6.5 are already implemented] |
| **7** | v0.2.1 | Destructuring & Spread | 5 | Complex unpacking, `*rest`, spread in literals/calls |
| **8** | v0.2.2 | Pattern Matching & Tagged Unions | 8 | Match expressions, all patterns, `union` keyword, exhaustiveness |
| **9** | v0.2.3 | Generators & Iterators | 3 | `yield`/`yield from`, generator inference |
| **10** | v0.2.4 | Async/Await | 6 | `async def`, `await`, `async for/with`, async generators |
| **11** | v0.2.5 | Advanced Functions | 5 | Pos-only/kw-only, `@kwargs`, partial application |
| **12** | v0.2.6 | Type System & Polish | 6 | Variance, delegates, events, custom decorators, spec audit |

**Total: ~38 items across 7 phases (v0.2.0–v0.2.6)** [CORRECTED: was ~42, reduced by 4 already-completed Phase 6 items]

---

## Critical Path

```
Phase 6 (v0.2.0) ──→ Phase 7 (v0.2.1) ──→ Phase 8 (v0.2.2) ──→ ┐
                                                                    ├──→ Phase 12 (v0.2.6)
Phase 9 (v0.2.3) ──→ Phase 10 (v0.2.4) ──→ Phase 11 (v0.2.5) ──→ ┘
```

- Phases 6 and 9 can begin in parallel (independent)
- Phase 7 depends on Phase 6 (method overloading needed for spread resolution)
- Phase 8 depends on Phase 7 (unpacking patterns relate to destructuring)
- Phase 10 depends on Phase 9 (async generators need generators)
- Phases 11–12 can begin once 10 is done (or in parallel with 10 if capacity allows)

---

## Ordering Rationale

1. **Phase 6 first** — fixes correctness issues in shipped features; no new syntax risk
2. **Phase 7 before 8** — spread/unpacking is foundational; tagged union destruction uses similar patterns
3. **Phase 8 = highest impact** — pattern matching + tagged unions enable idiomatic Sharpy
4. **Phase 9 before 10** — generators are prerequisite for async generators
5. **Phase 10 completes the async story** — last major syntax feature
6. **Phases 11–12 are polish** — advanced function params, type system, and gap-filling

---

## Out of Scope (by spec design, NOT missing)

Intentional language design decisions:
- `global`/`nonlocal` — C# scoping rules (Axiom 1)
- `**kwargs` without decorator — statically untypeable (Axiom 3)
- Multiple class inheritance — C# single inheritance (Axiom 1)
- `@classmethod` — use `@static` instead
- `__call__` — no callable protocol
- Async comprehensions — unsupported (no special handling; simply doesn't parse) [CORRECTED: there is no deliberate parse error handler; async comprehensions fail because `async` is not a recognized comprehension keyword]
- `__radd__` and reverse operators — C# compile-time overload resolution
- `__iadd__` and in-place operators — deferred to C# 14

## Deferred Post-v0.2.x

| Feature | Reason |
|---------|--------|
| `del` statement | Low priority; dict key deletion via method instead |
| `ref`/`out`/`in` parameter modifiers | Low priority; most Sharpy code won't need pass-by-ref |
| `@implicit`/`@explicit` conversions | Spec not finalized |
| Expression blocks (`do:`) | Nice-to-have; lambdas/helpers suffice |
| `@file` access modifier | Requires C# 11 |
| List patterns (`case [a, b]:`) | Requires C# 11 |
| Static abstract interface members | Requires C# 11 + .NET 7 |
| Record structs | Requires C# 10 |
| `field` keyword in properties | Requires C# 13 |
| Extension properties/operators | Requires C# 14 |
| User-defined `+=` operators (`__iadd__`) | Requires C# 14 |

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-02-17
**Plan file:** `~/.claude/plans/quirky-exploring-tulip.md`

### Corrections Made

| # | Claim | Correction |
|---|-------|-----------|
| 6.1 | `@final` not handled in codegen | **Already fully handled.** `DecoratorNames.Final` → `SealedKeyword` in both `RoslynEmitter.TypeDeclarations.cs` (line 878) and `RoslynEmitter.ClassMembers.cs` (line 900). Item struck through. |
| 6.3 | `int * str` reversed case missing | **Already fully handled.** `TypeInferenceService.cs` (lines 194-200) checks both orderings. Codegen in `RoslynEmitter.Expressions.Operators.cs` (lines 172-179) dispatches to `GenerateStringRepetition` for both. Item struck through. |
| 6.4 | `to` parsed at postfix level | **Already at correct precedence.** `ParseCast()` is called from `ParseComparison()` and calls `ParsePipe()`, placing `to` exactly between pipe and comparisons per spec. Item struck through. |
| 6.5 | SPY0251 rejects interface method bodies | **Inverted.** SPY0251 fires when interface methods have NO body. Default implementations (methods with real bodies) are already explicitly allowed by `NameResolver.ValidateInterfaceMethod()`. Item struck through. |
| 6.7 | `.value` missing for enums | **Partially done.** `.value` for integer enums is already implemented via cast in `RoslynEmitter.Expressions.Access.cs` (line 359+). Only `.name` and iteration remain. |
| 6.9 | `Define()` silently overwrites | **Throws, not overwrites.** `Scope.Define()` throws `InvalidOperationException` for duplicate non-variable symbols. End result (overloading blocked) is the same, but mechanism was mischaracterized. |
| Summary | Phase 6 has 9 items | **Reduced to 5 actual items.** 4 items (6.1, 6.3, 6.4, 6.5) are already completed. Total plan items reduced from ~42 to ~38. |
| Out of scope | Async comprehensions = "deliberate parse error" | **No deliberate handler exists.** `async` simply isn't a recognized comprehension keyword. |

### Warnings

- **Phase 6 scope significantly overestimated.** 4 of 9 items were already implemented, suggesting the status audit was based on stale information. Consider re-auditing other phases for similar over-counting.
- **6.6 (Constructor chaining):** The plan doesn't mention that `super().__init__()` → `: base(...)` is already working (in `RoslynEmitter.ClassMembers.cs` lines 292-332). The same pattern could be adapted for `self.__init__()` → `: this(...)`.

### Verified Claims (Correct)

| Dimension | Items Checked | Result |
|-----------|--------------|--------|
| **File paths** | All key files referenced in Phases 6-12 | All exist |
| **Diagnostic codes** | SPY0515, SPY0516, SPY0517, SPY0251 | All confirmed in `DiagnosticCodes.cs` |
| **AST nodes (existing)** | MatchExpression, OrPattern, TypePattern, BindingPattern, UnionDef, AwaitExpression, RaiseStatement.Cause | All confirmed |
| **AST nodes (new needed)** | RelationalPattern, SpreadExpression, YieldStatement, PlaceholderExpression | Confirmed none exist yet |
| **Completed features** | Properties, `with`, match, walrus, generics, comprehensions, named tuples, pipe, collections, try/maybe, chains | All confirmed in codegen |
| **Spec file count** | 108 .md files + grammar.ebnf.txt | Confirmed (109 total) |
| **Deferred C# versions** | C# 10 (record structs), C# 11 (@file, list patterns, static abstract), C# 13 (field), C# 14 (extensions, +=) | All match `docs/language_specification/deferred_features.md` |
| **Out-of-scope items** | `__call__`, `@classmethod`/`@static`, `**kwargs`, reverse operators | All confirmed correct |

### Missing Steps

- None identified. The plan covers all pipeline phases (Lexer → Parser → Semantic → Validation → CodeGen → Tests) for each feature where relevant.

### Unchecked Claims

- **6.2 semantic handling of `Cause`**: Codegen confirmed to ignore it; TypeChecker handling not checked (low risk — the fix is straightforward either way).
- **6.8 TypeResolver substitution for generic type aliases**: The technical approach (substitution in TypeResolver) was not validated against TypeResolver internals.
- **8.5 `Deconstruct` awareness**: Did not verify whether any Deconstruct pattern exists in codegen.
- **10.6 `asyncio.gather` mapping**: Did not verify if any stdlib mapping exists for this.
