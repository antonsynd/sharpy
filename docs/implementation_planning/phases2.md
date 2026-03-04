<!-- Verified by /project:verify-plan on 2026-02-17 -->
<!-- Verification result: PASS WITH CORRECTIONS -->
<!-- Phase 6+7 marked complete on 2026-02-20 (closes #209) -->
<!-- Phase 9 marked complete on 2026-02-23 (generators fully implemented) -->
<!-- Phase 8.1-8.5 marked complete on 2026-02-28 (all non-union patterns implemented) -->
<!-- Phase 8.6 marked complete on 2026-02-28 (tagged union declarations implemented) -->
<!-- Phase 8.7-8.8 marked complete on 2026-02-28 (union case patterns + exhaustiveness checking) -->
<!-- Phase 10.2 marked complete on 2026-02-28 (await expressions implemented) -->
<!-- Phase 10.3-10.6 marked complete on 2026-02-28 (async for/with, async generators, asyncio.gather) -->
<!-- Phase 11.1-11.2 marked complete on 2026-03-01 (positional-only and keyword-only parameters) -->
<!-- Phase 11.5 marked complete on 2026-03-01 (partial application with operator sections) -->
<!-- Phase 11 marked COMPLETE on 2026-03-01 (11.3 @kwargs + 11.4 @dynamic_kwargs dropped) -->
<!-- Phase 12.1-12.2 marked complete on 2026-03-02 (delegates + generic variance implemented) -->
<!-- Phase 12.3 marked complete on 2026-03-03 (events fully implemented: auto + function-style, 18 test fixtures, 12 diagnostics) -->
<!-- Phase 12.4 marked complete on 2026-03-03 (custom decorator arguments: dotted names, positional/keyword args, 26 test fixtures) -->
<!-- Phase 12.5 marked complete on 2026-03-03 (spec gap audit: ~38 new test fixtures across 13 feature areas, 8 GitHub issues filed for discovered bugs) -->

# Sharpy Language Feature Completeness — Phased Roadmap

## Context

Sharpy's v0.1.x series (16 phases, v0.1.0–v0.1.15) is complete, delivering the core pipeline: lexer, parser, semantic analysis, codegen, all basic types, classes/structs/interfaces/enums, generics with constraints, lambdas/delegates, collections with comprehensions, exception handling, module system, and 29 dunders.

Implementation plans Phase 1–5 were drafted post-v0.1.x. Several items from those plans have been completed (properties, `with` statement, match statement basics, walrus codegen, multi-for comprehensions, named tuples, pipe forward, generic constraints). This roadmap picks up what remains and adds everything else needed for language spec completeness (syntax + semantics, not stdlib).

**Source of truth:** `docs/language_specification/` (112 spec files + grammar.ebnf.txt)

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
- **Phase 6 (v0.2.0):** Constructor chaining, enum `.name`/iteration, generic type aliases, method overloading
- **Phase 7 (v0.2.1):** Complex tuple unpacking, rest patterns, tuple unpacking in comprehensions, spread in collection literals, spread in function calls
- **Phase 9 (v0.2.3):** `yield` statement, `yield from` delegation, generator return type inference
- **Phase 8 (v0.2.2):** Match expressions, or-patterns, type patterns with binding, relational patterns, property/positional patterns, tagged union declarations, union case patterns in match, exhaustiveness checking (bool/enum/union/non-finite types)
- **Phase 10 (v0.2.4):** `async def` functions, `await` expressions, `async for` loops, `async with` statements (IDisposable + dunder protocol), async generators (`yield`/`yield from` in async), `asyncio.gather` → `Task.WhenAll`
- **Phase 11 (v0.2.5):** Positional-only (`/`) and keyword-only (`*`) parameter markers, partial application with operator sections (11.3 `@kwargs` + 11.4 `@dynamic_kwargs` dropped)

### Missing (grouped by phase below)

---

## Phase 6 — v0.2.0: Correctness & Completion

**Goal:** Finish remaining items from Phase 1–5 plans; solidify the existing feature set before adding new syntax.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~6.1~~ | ~~`@final` decorator on classes/methods~~ | ~~S~~ | ~~Already completed~~ [CORRECTED: `@final` is already fully handled — `DecoratorNames.Final` maps to `SealedKeyword` in both `RoslynEmitter.TypeDeclarations.cs` and `RoslynEmitter.ClassMembers.cs`] |
| ~~6.2~~ | ~~`raise X from Y` (inner exception)~~ | ~~M~~ | ~~Intentionally NOT implemented — language spec does not support inner exceptions~~ |
| ~~6.3~~ | ~~`int * str` string repetition (reversed)~~ | ~~S~~ | ~~Already completed~~ [CORRECTED: Both `str * int` and `int * str` are handled in `TypeInferenceService.cs` (lines 194-200) AND in codegen via `GenerateStringRepetition` in `RoslynEmitter.Expressions.Operators.cs` (lines 172-179)] |
| ~~6.4~~ | ~~`to` operator precedence~~ | ~~M~~ | ~~Already correct~~ [CORRECTED: `to` is already parsed at the correct level in `ParseCast()` (Parser.Expressions.cs:421), between pipe and comparisons, matching `docs/language_specification/type_casting.md`] |
| ~~6.5~~ | ~~Interface default methods~~ | ~~M~~ | ~~Already supported~~ [CORRECTED: SPY0251 fires when interface methods have NO body (missing `...`/`pass`). Methods with real bodies (default implementations) are already explicitly allowed by `NameResolver.ValidateInterfaceMethod()`] |
| ~~6.6~~ | ~~Constructor chaining (`self.__init__()`)~~ | ~~M~~ | ~~Completed.~~ `self.__init__()` → `: this(...)`, `super().__init__()` → `: base(...)` |
| ~~6.7~~ | ~~Enum `.name`, iteration~~ | ~~M~~ | ~~Completed.~~ `.name` → `Enum.GetName()`, `.value` → cast, `for x in Enum:` → `Enum.GetValues()` |
| ~~6.8~~ | ~~Generic type aliases (`type Cb[T] = (T) -> None`)~~ | ~~M~~ | ~~Completed.~~ `TypeParameters` on `TypeAlias` AST; substitution in TypeResolver |
| ~~6.9~~ | ~~Method overloading (user-defined)~~ | ~~L~~ | ~~Completed.~~ `MethodOverloads` on TypeSymbol; `SignatureKey`-based dedup; overload resolution in TypeChecker |

**Key files:** `RoslynEmitter.TypeDeclarations.cs`, `RoslynEmitter.ClassMembers.cs`, `NameResolver.cs`, `TypeChecker.cs`, `TypeInferenceService.cs`, `Parser.Expressions.cs`

---

## Phase 7 — v0.2.1: Destructuring & Spread

**Goal:** Complete tuple unpacking and the spread operator — foundational for `*args` call-site spreading and collection manipulation.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~7.1~~ | ~~Complex tuple unpacking~~ | ~~M~~ | ~~Completed.~~ Nested targets: `(a, (b, c)) = nested`; recursive destructuring with `.ItemN` access |
| ~~7.2~~ | ~~Rest patterns in unpacking~~ | ~~M~~ | ~~Completed.~~ `first, *rest = items` and `first, *mid, last = items`; `*rest` typed as `list[T]` |
| ~~7.3~~ | ~~Tuple unpacking in comprehensions~~ | ~~M~~ | ~~Completed.~~ `[a + b for a, b in pairs]`; lowered to lambda with `.ItemN` destructuring |
| ~~7.4~~ | ~~Spread in collection literals~~ | ~~L~~ | ~~Completed.~~ `[*a, *b]`, `{*s1, *s2}`, `{**d1, **d2}` — `SpreadExpression` AST + `AddRange`/`UnionWith`/dict merge codegen |
| ~~7.5~~ | ~~Spread in function calls~~ | ~~L~~ | ~~Completed.~~ `f(*args)` — call-site unpacking with static type checking |

**Key files:** `Parser.Expressions.cs`, `RoslynEmitter.Expressions.Literals.cs`, `RoslynEmitter.Expressions.cs`, `TypeChecker.Expressions.cs`

**Dependencies:** 7.2 enables idiomatic Python patterns. 7.4 + 7.5 together complete the spread operator spec.

---

## Phase 8 — v0.2.2: Pattern Matching & Tagged Unions

**Goal:** Complete algebraic data type support — the highest-impact v0.2.x feature, enabling idiomatic Sharpy.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~8.1~~ | ~~Match expression (expression form)~~ | ~~M~~ | ~~Completed.~~ `ParseMatchExpression()` in parser; `CheckMatchExpression()` in TypeChecker; `GenerateMatchExpression()` → C# switch expression in `RoslynEmitter.Patterns.cs`. Tests: `match_expr_basic_0001`, `match_expr_guard_0001`, `match_expr_nested_0001`, `match_expr_return_0001` |
| ~~8.2~~ | ~~Or-patterns~~ | ~~S~~ | ~~Completed.~~ `case "a" \| "b":` parsed via pipe detection in `ParsePattern()`; SPY0320 rejects bindings in or-patterns; codegen emits C# `or` patterns or when-guards. Tests: `match_or_literal_0001`, `match_or_member_access_0001`, etc. |
| ~~8.3~~ | ~~Type patterns with binding~~ | ~~M~~ | ~~Completed.~~ `case int() as n:` via `ParseTypePatternOrStructural()`; SPY0202/SPY0203 diagnostics; `DeclarationPattern` codegen. Tests: `match_type_basic_0001`, `match_type_binding_0004`, `match_type_binding_0016` |
| ~~8.4~~ | ~~Relational patterns~~ | ~~M~~ | ~~Completed.~~ `case > 0:` via `ParseRelationalPattern()`; `RelationalPattern` AST + `RelationalOperator` enum; SPY0204 type mismatch diagnostic; C# relational pattern codegen. Tests: `match_relational_basic_0001`, `match_relational_combined_0001`, etc. |
| ~~8.5~~ | ~~Property/positional patterns~~ | ~~M~~ | ~~Completed.~~ `case Point(x=0):` via `ParsePropertyPattern()`, `case Point(0, y):` via `ParsePositionalPattern()`; SPY0207/SPY0209 diagnostics; `RecursivePattern` with `PropertyPatternClause` codegen. Tests: `match_property_basic_0001`, `match_positional_basic_0001`, etc. |
| ~~8.6~~ | ~~Tagged union declarations (`union`)~~ | ~~XL~~ | ~~Completed.~~ `union` keyword token + `TypeKind.Union`; `ParseUnionDef()` parser; `ResolveUnionDeclaration()` name resolution with case symbols; `CheckUnion()` type checking with field type resolution; `GenerateUnionDeclaration()` codegen lowering to abstract base + sealed nested classes with `Deconstruct` methods; generic union support with type parameter substitution; `SPY0124` (empty union), `SPY0365` (duplicate case) diagnostics. Tests: `union_basic`, `union_generic`, `union_field_types`, `union_single_case`, `union_no_fields`, `union_mixed_fields`, `union_duplicate_case`, `union_empty`. |
| ~~8.7~~ | ~~Union case patterns in match~~ | ~~M~~ | ~~Completed.~~ `UnionCasePattern` fully wired: `TryResolveUnionCaseFromPattern()` in TypeChecker; `GenerateUnionCasePositionalPattern()` in RoslynEmitter.Patterns.cs; generic union support with type parameter substitution. Tests: `match_union_basic_0001`, `match_union_generic_0001`, `match_union_nested_0001`, `match_union_short_form_0001`, `match_union_unit_0001`, `match_union_wildcard_0001` |
| ~~8.8~~ | ~~Exhaustiveness checking~~ | ~~L~~ | ~~Completed.~~ `ExhaustivenessValidator` (Order 405) checks bool, enum, union exhaustiveness. SPY0416 error for non-exhaustive match expressions; SPY0463 warning for non-exhaustive match statements. Non-finite types require wildcard/binding arm in expressions. Tests: `match_exhaustive_bool_*`, `match_exhaustive_enum_*`, `match_exhaustive_union_*`, `match_expr_non_exhaustive_error`, `match_stmt_non_exhaustive_warning` |

**Key files:** `Parser.Statements.cs`, `Parser.Expressions.cs`, `Pattern.cs`, `Statement.Future.cs`, `TypeChecker.Statements.cs`, `RoslynEmitter.Patterns.cs`, `RoslynEmitter.TypeDeclarations.cs`

**Dependencies:** ~~8.1–8.8 all complete.~~ Full pipeline: tagged union declarations → union case patterns → exhaustiveness checking.

---

## Phase 9 — v0.2.3: Generators & Iterators

**Goal:** Add `yield`/`yield from` for lazy sequence generation — prerequisite for async generators in Phase 10.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~9.1~~ | ~~`yield` statement~~ | ~~L~~ | ~~Completed.~~ `YieldStatement` AST node; parser `ParseYieldStatement()`; semantic marks `IsGenerator` on `FunctionSymbol`; codegen emits C# `yield return`. Diagnostics: SPY0265 (yield outside function), SPY0267 (return with value in generator), SPY0268 (yield in `__next__`), SPY0269 (generator `__iter__` + `__next__` conflict). |
| ~~9.2~~ | ~~`yield from` delegation~~ | ~~M~~ | ~~Completed.~~ `YieldStatement.IsFrom` flag; parser detects `yield from`; codegen emits `foreach (var __yieldItem_N in expr) { yield return __yieldItem_N; }` |
| ~~9.3~~ | ~~Generator return type inference~~ | ~~M~~ | ~~Completed.~~ Functions with `yield` automatically get `IEnumerable<T>` return type via `WrapInIEnumerable()`. `__iter__` and `__reversed__` with `yield` get `IEnumerator<T>`. `GeneratorValidator` enforces constraints. |

**Key files:** `Statement.cs` (YieldStatement), `Parser.Statements.cs` (ParseYieldStatement), `TypeChecker.Statements.cs` (CheckYield), `RoslynEmitter.Statements.cs` (GenerateYield), `GeneratorValidator.cs`

**Dependencies:** 9.1 → 9.2 → 9.3 (linear chain). Must complete before Phase 10.5 (async generators).

---

## Phase 10 — v0.2.4: Async/Await

**Goal:** Full async support — the last major language feature.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~10.1~~ | ~~`async def` functions~~ | ~~L~~ | ~~Completed.~~ `ParseAsyncFunctionDef()` in `Parser.Definitions.cs`; `FunctionDef.IsAsync` property; TypeChecker wraps return in `TaskType`; RoslynEmitter adds `async` modifier + `WrapInTask()`. Integration tests: `async_basic`, `async_class_method`, `async_void`. Error tests: `async_generator_error`, `async_init_error`. |
| ~~10.2~~ | ~~`await` expressions~~ | ~~L~~ | ~~COMPLETE — `ParseAwaitExpression()` in parser, `CheckAwaitExpression()` in TypeChecker (SPY0273/SPY0274), `GenerateAwaitExpression()` via `SyntaxFactory.AwaitExpression()`. Lambda await rejected.~~ |
| ~~10.3~~ | ~~`async for` loops~~ | ~~M~~ | ~~Completed.~~ `ForStatement.IsAsync` flag; TypeChecker validates `IAsyncEnumerable<T>` operand + async context (SPY0360); codegen emits `await foreach`. Tests: `async_for_basic`, `async_for_outside_async_error` |
| ~~10.4~~ | ~~`async with` statements~~ | ~~M~~ | ~~Completed.~~ Dual protocol support: (1) `IDisposable`/`IAsyncDisposable` → `using`/`await using`, (2) `__enter__`/`__exit__` and `__aenter__`/`__aexit__` dunders → try/finally with explicit method calls. `ContextManagerKind` enum in SemanticInfo selects codegen path. Tests: `async_with_basic`, `with_context_manager`, `async_with_sync_dunders_only` (error), `with_enter_without_exit` (error) |
| ~~10.5~~ | ~~Async generators~~ | ~~L~~ | ~~Completed.~~ `async def` + `yield` → `IAsyncEnumerable<T>` return type via `WrapInIAsyncEnumerable()`. `yield from` in async generators supported as Sharpy extension (deviation from Python). `AsyncScope` in emitter tracks async context for `await foreach` emission. Tests: `async_generator_basic`, `async_generator_yield_from`, `async_generator_compiles` |
| ~~10.6~~ | ~~`Task.WhenAll` mapping~~ | ~~M~~ | ~~Completed.~~ Synthetic `asyncio` module (`SyntheticModuleNames.Asyncio`); `asyncio.gather()` → `Task.WhenAll()`, `asyncio.sleep(n)` → `Task.Delay(TimeSpan.FromSeconds(n))`. ImportResolver recognizes synthetic modules. Tests: `async_gather_basic`, `async_sleep_basic` |

**Key files:** `Expression.Future.cs` (AwaitExpression), `Parser.Definitions.cs`, `Parser.Statements.cs`, `TypeChecker.cs`, `RoslynEmitter.Statements.cs`, `RoslynEmitter.Expressions.cs`

**Dependencies:** ~~10.1–10.6 all complete.~~ Full async pipeline implemented.

---

## Phase 11 — v0.2.5: Advanced Function Features

**Goal:** Complete the function parameter system and add partial application.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~11.1~~ | ~~Positional-only parameters (`/`)~~ | ~~M~~ | ~~Completed.~~ `ParameterKind.PositionalOnly` flag; parser handles `/` separator (SPY0126–SPY0129); call-site enforcement via SPY0370; codegen uses named arguments for C# translation |
| ~~11.2~~ | ~~Keyword-only parameters (`*`)~~ | ~~M~~ | ~~Completed.~~ `ParameterKind.KeywordOnly` flag; parser handles bare `*` separator; call-site enforcement via SPY0371; `ReorderParametersForCSharp()` + named argument generation |
| ~~11.3~~ | ~~`@kwargs` decorator~~ | ~~L~~ | ~~Dropped.~~ Compiler-understood transforming decorators violate "no magic" principle; named arguments + user-defined option structs achieve the same goal without invisible code generation |
| ~~11.4~~ | ~~`@dynamic_kwargs` decorator~~ | ~~L~~ | ~~Dropped.~~ Trades type safety for flexibility (conflicts with Axiom 3); `**kwargs` syntax only valid with decorator is surprising; passing `dict[str, T]` explicitly is clearer |
| ~~11.5~~ | ~~Partial application~~ | ~~L~~ | ~~Completed.~~ Parser-level desugaring: `Identifier("_")` in call args and paren exprs lowered to `LambdaExpression`. No new AST node needed. SPY0130–SPY0131 for error cases. TypeChecker body-based param inference for unannotated lambdas. Operator sections `(_ * 2)`, `(_ > 0)`, `(-_)` |

**Key files:** `Parser.Definitions.cs`, `Parser.Expressions.cs`, `TypeChecker.Expressions.cs`, `RoslynEmitter.Expressions.cs`, `ParameterSymbol`

**Dependencies:** ~~All complete.~~ Phase 11 is done.

---

## Phase 12 — v0.2.6: Type System Advances & Polish

**Goal:** Generic variance, delegate declarations, events, and final gap-filling.

| # | Feature | Complexity | Notes |
|---|---------|-----------|-------|
| ~~12.1~~ | ~~Delegate type declarations~~ | ~~M~~ | ~~Completed.~~ `DelegateDef` AST node; `ParseDelegateDef()` parser; `ResolveDelegateDeclaration()` + `CheckDelegate()` semantics; `GenerateDelegateDeclaration()` codegen emits C# delegate type. Lambda-to-delegate assignment + delegate invocation supported. 6 test fixtures (5 positive, 1 error). |
| ~~12.2~~ | ~~Generic variance (`out T`, `in T`)~~ | ~~L~~ | ~~Completed.~~ `TypeParameterVariance` enum on `TypeParameterDef`; parser recognizes `out`/`in` annotations; `VarianceValidator` (Order 415) checks position correctness (SPY0417–SPY0419); codegen emits C# `out`/`in` keywords; `SymbolSerializer` v6. 10 test fixtures (6 positive, 4 error). |
| ~~12.3~~ | ~~Events~~ | ~~L~~ | ~~Completed.~~ `EventDef` AST node; `ParseEventDef()` parser (auto-events + function-style with add/remove accessors); `ResolveEventDeclaration()` + `CheckEvent()` semantics; `EventValidator` (Order 412, SPY0420–SPY0423); codegen: auto-events → field-like C# events, function-style → event accessors, `+=`/`-=` subscribe/unsubscribe, `?.invoke()` thread-safe raise; `EventSymbol` record; SPY0135–SPY0136 parser + SPY0373–SPY0378 semantic diagnostics; interface event declarations; decorator support (@virtual/@abstract/@override/@static/@final). 18 test fixtures (9 positive, 9 error). |
| ~~12.4~~ | ~~Custom decorator arguments~~ | ~~M~~ | ~~Completed.~~ `Decorator` record extended with `Arguments`, `KeywordArguments`, `QualifiedParts`; parser handles `@dotted.name(args, keyword=value)`; `DecoratorValidator` rejects args on built-in decorators (SPY0322) and validates compile-time constants (SPY0425); codegen emits C# attributes via `SyntaxFactory.AttributeList()` with name mangling, dotted names, positional + named args, `type(X)` → `typeof(X)`. |
| ~~12.5~~ | ~~Spec gap audit + integration test sweep~~ | ~~M~~ | ~~Completed.~~ Systematic audit of ~95 testable spec files vs. 1,064 existing fixtures. Added ~38 new file-based integration test fixtures covering 13 feature areas (assert, bitwise, ??=, comments, exceptions, loop-else, dunders, identity, numerics, strings, casting, shorthands, context managers, scoping, precedence, promotion). Filed 8 GitHub issues for discovered bugs (#278–#285). 2 fixtures skipped with `.skip` files pending bug fixes. |

**Key files:** `Statement.Future.cs` (new `EventDef` AST), `Parser.Definitions.cs` (`ParseEventDef()`), `NameResolver.cs` (`ResolveEventDeclaration()`), `TypeChecker.Definitions.cs` (`CheckEvent()`), `RoslynEmitter.ClassMembers.cs` (`GenerateAutoEvent()`/`GenerateFunctionStyleEvent()`), `RoslynEmitter.Statements.cs` (event `+=`/`-=` codegen)

**Dependencies:** ~~12.1 before 12.2 (variance needs delegates to exist).~~ ~~12.3 depends on 12.1 (events use delegate types).~~ ~~12.4 independent.~~ 12.5 independent (audit only).

---

## Phase Summary

| Phase | Version | Theme | Items | Key Deliverables |
|-------|---------|-------|-------|-----------------|
| **6** | v0.2.0 | Correctness & Completion | ~~5~~ ✅ Complete | Constructor chaining, enum polish, generic type aliases, method overloading (6.2 `raise from` intentionally skipped) |
| **7** | v0.2.1 | Destructuring & Spread | ~~5~~ ✅ Complete | Complex unpacking, `*rest`, spread in literals/calls |
| **8** | v0.2.2 | Pattern Matching & Tagged Unions | ~~8~~ ✅ Complete | Match expressions, or/type/relational/property/positional patterns, tagged unions, union case patterns, exhaustiveness checking |
| **9** | v0.2.3 | Generators & Iterators | ~~3~~ ✅ Complete | `yield`/`yield from`, generator inference, 4 new diagnostics (SPY0265–SPY0269) |
| **10** | v0.2.4 | Async/Await | ~~6~~ ✅ Complete | `async def`, `await`, `async for`, `async with` (dual protocol), async generators, `asyncio.gather` |
| **11** | v0.2.5 | Advanced Functions | ~~5~~ ✅ Complete | ~~Pos-only/kw-only~~ ✅, ~~partial application~~ ✅ (11.3 `@kwargs` + 11.4 `@dynamic_kwargs` dropped — see Out of Scope) |
| **12** | v0.2.6 | Type System & Polish | ~~5~~ ✅ Complete | ~~Delegates~~ ✅, ~~variance~~ ✅, ~~events~~ ✅, ~~custom decorators~~ ✅, ~~spec audit~~ ✅ |

**Total: Phase 12 (v0.2.6) COMPLETE** — Phases 6–12 all complete (38 items delivered, 2 dropped). Phase 12.5 added ~38 integration test fixtures and filed 8 issues for discovered bugs.

---

## Critical Path

```
✅ Phase 6 (v0.2.0) ──→ ✅ Phase 7 (v0.2.1) ──→ ✅ Phase 8 (v0.2.2)  ──→ ┐
                                                                             ├──→ Phase 12 (v0.2.6)
✅ Phase 9 (v0.2.3) ──→ ✅ Phase 10 (v0.2.4)  ──→ ✅ Phase 11 (v0.2.5) ──→ ┘
```

- ✅ Phases 6–12 all complete
- **Phase 12 COMPLETE** — all 5 items delivered (delegates, variance, events, custom decorator args, spec gap audit)
- ~~12.1 (delegates) → 12.2 (variance) is the only dependency chain~~; ~~12.3–12.5 all complete~~

---

## Ordering Rationale

1. ~~**Phase 6 first** — fixes correctness issues in shipped features; no new syntax risk~~ ✅ Done
2. ~~**Phase 7 before 8** — spread/unpacking is foundational; tagged union destruction uses similar patterns~~ ✅ Done
3. ~~**Phase 8 = highest impact** — pattern matching + tagged unions enable idiomatic Sharpy~~ ✅ Complete (8.1–8.8 all done: match expressions, all pattern types, tagged unions, union case patterns, exhaustiveness checking)
4. ~~**Phase 9 before 10** — generators are prerequisite for async generators~~ ✅ Done
5. ~~**Phase 10 completes the async story** — last major syntax feature~~ ✅ Complete (10.1–10.6 all done: async def, await, async for/with, async generators with yield from, asyncio.gather mapping)
6. **Phase 12 COMPLETE** — Phases 6–12 all complete; 12.1–12.4 (delegates, variance, events, custom decorator args) + 12.5 (spec gap audit with ~38 new fixtures)

---

## Out of Scope (by spec design, NOT missing)

Intentional language design decisions:
- `global`/`nonlocal` — C# scoping rules (Axiom 1)
- `**kwargs` (any form) — statically untypeable (Axiom 3); pass `dict[str, T]` explicitly instead
- `@kwargs` decorator — compiler-understood transforming decorators violate "no magic" principle; named arguments + user-defined option structs achieve the same goal without invisible code generation
- `@dynamic_kwargs` decorator — trades type safety for flexibility (Axiom 3 violation); `**kwargs` syntax only valid with decorator is surprising; explicit dict parameter is clearer
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
| **Diagnostic codes** | SPY0251 | Confirmed in `DiagnosticCodes.cs` (SPY0515/0516/0517 removed — dead code) |
| **AST nodes (existing)** | MatchExpression, OrPattern, TypePattern, BindingPattern, UnionDef, AwaitExpression, RaiseStatement.Cause | All confirmed |
| **AST nodes (new needed)** | RelationalPattern, ~~SpreadExpression~~, ~~YieldStatement~~, PlaceholderExpression | RelationalPattern and PlaceholderExpression still needed; SpreadExpression and YieldStatement now implemented |
| **Completed features** | Properties, `with`, match, walrus, generics, comprehensions, named tuples, pipe, collections, try/maybe, chains | All confirmed in codegen |
| **Spec file count** | 112 .md files + grammar.ebnf.txt | Updated from 108 → 112 (generators.md, method_overloading.md, delegates.md, context_managers.md added) |
| **Deferred C# versions** | C# 10 (record structs), C# 11 (@file, list patterns, static abstract), C# 13 (field), C# 14 (extensions, +=) | All match `docs/language_specification/deferred_features.md` |
| **Out-of-scope items** | `__call__`, `@classmethod`/`@static`, `**kwargs`, reverse operators | All confirmed correct |

### Missing Steps

- None identified. The plan covers all pipeline phases (Lexer → Parser → Semantic → Validation → CodeGen → Tests) for each feature where relevant.

### Unchecked Claims

- **6.2 semantic handling of `Cause`**: Codegen confirmed to ignore it; TypeChecker handling not checked (low risk — the fix is straightforward either way).
- **6.8 TypeResolver substitution for generic type aliases**: The technical approach (substitution in TypeResolver) was not validated against TypeResolver internals.
- **8.5 `Deconstruct` awareness**: Did not verify whether any Deconstruct pattern exists in codegen.
- **10.6 `asyncio.gather` mapping**: Did not verify if any stdlib mapping exists for this.

---

## Status Audit (2026-02-23)

**Phase 9 verified COMPLETE** — all 3 items (yield, yield from, generator return type inference) fully implemented across parser, semantic, validation, and codegen.

**Phase 8 audit (2026-02-28)** — All 8 items confirmed COMPLETE. 8.1–8.5: Match expressions, or-patterns (SPY0320), type patterns with binding (SPY0202/SPY0203), relational patterns (SPY0204), property/positional patterns (SPY0207/SPY0209). 8.6: Tagged union declarations with `union` keyword, `ParseUnionDef()`, codegen lowering to abstract base + sealed nested classes. 8.7: Union case patterns wired through `TryResolveUnionCaseFromPattern()` → `GenerateUnionCasePositionalPattern()` with generic support. 8.8: `ExhaustivenessValidator` (Order 405) covers bool/enum/union finite types + non-finite type wildcard requirement. SPY0416/SPY0463. 20+ union pattern test fixtures, 10+ exhaustiveness test fixtures.

**Phase 10 audit (2026-02-28)** — All 6 items confirmed COMPLETE. 10.1: `async def` with `FunctionDef.IsAsync`. 10.2: `await` with SPY0273/SPY0274. 10.3: `async for` → `await foreach` with `IAsyncEnumerable<T>` validation (SPY0360). 10.4: `async with` dual protocol — `IAsyncDisposable` → `await using` + `__aenter__`/`__aexit__` → try/finally with `ContextManagerKind` enum in SemanticInfo. 10.5: Async generators → `IAsyncEnumerable<T>` return type, `yield from` in async generators as Sharpy extension. 10.6: Synthetic `asyncio` module with `gather` → `Task.WhenAll`, `sleep` → `Task.Delay`. 42 async test fixtures total.

**Phase 11 audit (updated 2026-03-01)** — PHASE COMPLETE. 11.1 (positional-only `/`) and 11.2 (keyword-only `*`) confirmed COMPLETE: `ParameterKind` enum, parser separators (SPY0126–SPY0129), call-site enforcement (SPY0370/SPY0371), codegen parameter reordering with named arguments, comprehensive test fixtures. 11.5 (partial application) COMPLETE: parser-level desugaring of `Identifier("_")` in call args and paren exprs to `LambdaExpression`; SPY0130–SPY0131 diagnostics; TypeChecker body-based param inference; operator sections. 11.3 (`@kwargs`) and 11.4 (`@dynamic_kwargs`) DROPPED: compiler-understood transforming decorators violate "no magic" principle; dynamic kwargs conflicts with Axiom 3; named arguments + explicit option structs suffice.

**Phase 12 audit (updated 2026-03-03)** — ALL 5 items COMPLETE. 12.1 (delegates): `DelegateDef` AST node, `ParseDelegateDef()`, `ResolveDelegateDeclaration()` + `CheckDelegate()`, `GenerateDelegateDeclaration()`, lambda-to-delegate assignment, delegate invocation — 6 test fixtures. 12.2 (variance): `TypeParameterVariance` enum, parser `out`/`in` recognition, `VarianceValidator` (SPY0417–SPY0419), codegen `out`/`in` keywords, `SymbolSerializer` v6 — 10 test fixtures. 12.3 (events): `EventDef` AST, `ParseEventDef()`, `ResolveEventDeclaration()` + `CheckEvent()`, `EventValidator` (Order 412), auto-events + function-style codegen, `+=`/`-=` subscribe, `?.invoke()` raise, SPY0135–0136/SPY0373–0378/SPY0420–0423 — 18 test fixtures. 12.4 (custom decorator args): `Decorator` record with `Arguments`/`KeywordArguments`/`QualifiedParts`, dotted name parsing, `DecoratorValidator` (SPY0322/SPY0425), `GenerateAttributeListsFromDecorators()` codegen — 26 test fixtures. 12.5 (spec gap audit): Audited ~95 testable spec files vs. 1,064 existing fixtures; added ~38 new file-based integration tests across 13 feature areas; filed 8 GitHub issues (#278–#285) for discovered bugs (dunder codegen, f-string internals, exception aliases, float32 literals).

### Language Spec Accuracy Issues Found

| Spec File | Issue | Severity | Resolution |
|-----------|-------|----------|------------|
| `async_programming.md` | Claims "✅ Native" for async/await but features are NOT implemented | HIGH | **FIXED** — added "not yet implemented" banner and ❌ markers on all sections |
| `match_statement.md` | Lists Or/Property/Relational/Positional patterns as ✅ but they're NOT parsed | HIGH | **FIXED** — marked ❌ for Or, Property, Positional, Relational |
| `match_statement.md` | Lists "Type with binding" (`case int() as n:`) as ✅ but `TypePattern` is entirely unwired — no parser, semantic, or codegen path | HIGH | **FIXED** — marked ❌, moved to Phase 8 items |
| `comprehensions.md` | Previously showed nested comprehension as blocked by SPY0515 | MEDIUM | **FIXED** — removed ❌, feature is implemented (SPY0515 was dead code) |
| `spread_operator.md` | No implementation status banner | LOW | **FIXED** — added status banner noting partial implementation |
| `delegates.md` | Missing document — referenced by `generic_variance.md` and `events_alt.md` | LOW | **FIXED** — created stub with Phase 12 implementation status |
| `generic_variance.md` | Contradictory: intro says "not yet implemented" but footer says "✅ Native" | HIGH | **FIXED** — updated footer to ❌ Not yet implemented |
| `parameter_modifiers.md` | Contradictory: intro says "deferred post-v0.2.x" but footer says "✅ Native" | HIGH | **FIXED** — updated footer to match deferred status |
| `conversion_operators.md` | No formal implementation status banner | LOW | **FIXED** — added deferred status banner |
| `README.md` | Duplicate "Operators" section header; missing entries for several spec files | MEDIUM | **FIXED** — reorganized sections, added missing entries |
| `generators.md` | Accurate — all features verified | OK | — |
| `method_overloading.md` | Accurate — diagnostics SPY0353/0354/0355 confirmed | OK | — |
| `enums.md` | Accurate — .name, .value, iteration all working | OK | — |
| `constructors.md` | Accurate — constructor chaining working | OK | — |
| `exception_handling.md` | Accurate — correctly documents `raise from` not supported (SPY0122) | OK | — |
