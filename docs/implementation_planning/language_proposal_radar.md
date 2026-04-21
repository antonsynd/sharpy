# Language Proposal Radar for Sharpy

> Surveyed: Python PEPs, Swift Evolution, Rust RFCs  
> Date: 2026-04-21  
> Scope: Proposals relevant to Sharpy's ergonomics, DX, and type system that are implementable given the C#/.NET target

**Exclusions:** Features already implemented in Sharpy are omitted — walrus operator (PEP 572), `X|Y` union syntax (PEP 604), type aliases (PEP 613), match statements (PEP 634), SelfType (PEP 673), dataclass (PEP 681), `@override` (PEP 698), f-strings (PEP 701), list/dict/set comprehensions, generators (`yield`/`yield from`), `maybe`/`try`/`??` expressions.

---

## Priority Summary

| # | Proposal | Category | Priority | Effort |
|---|----------|----------|----------|--------|
| 1 | PEP 822 — d-strings (dedented multiline) | Syntax Sugar | **High** | Low |
| 2 | PEP 671 — late-bound defaults | Syntax Sugar | **High** | Medium |
| 3 | RFC 3721 — inferred error type in try blocks | Error Handling | **High** | Low |
| 4 | RFC 3513 — inline `gen {}` blocks | Iterators | **High** | Medium |
| 5 | PEP 702 — `@deprecated` decorator | Type System | **High** | Low |
| 6 | RFC 3637 — guard patterns in match | Pattern Matching | **High** | Medium |
| 7 | PEP 696 — type defaults for type parameters | Type System | **High** | Medium |
| 8 | PEP 742 — `TypeIs` narrowing | Type System | **High** | Medium |
| 9 | PEP 750 — template strings (t-strings) | Standard Library | **High** | Medium |
| 10 | PEP 616 — `removeprefix`/`removesuffix` | Standard Library | **High** | Low |
| 11 | PEP 654 — ExceptionGroup / `except*` | Error Handling | Medium | Medium |
| 12 | PEP 646 — variadic generics (TypeVarTuple) | Type System | Medium | High |
| 13 | PEP 675 — `LiteralString` type | Type System | Medium | Medium |
| 14 | PEP 705/767 — ReadOnly annotations | Type System | Medium | Low |
| 15 | PEP 798 — unpacking in comprehensions | Syntax Sugar | Medium | Low |
| 16 | PEP 678 — exception notes | Error Handling | Medium | Low |
| 17 | RFC 3535 — constants in patterns | Pattern Matching | Medium | Low |
| 18 | PEP 814 — `frozendict` built-in | Standard Library | Medium | Low |
| 19 | RFC 3681 — struct default field values | Standard Library | Medium | Medium |
| 20 | PEP 612 — ParamSpec | Type System | Medium | High |
| 21 | SE-0380 — if/switch as expressions | Syntax Sugar | Low | High |
| 22 | PEP 758 — `except` without parentheses | Syntax Sugar | Low | Low |
| 23 | RFC 3137 — let-else | Error Handling | Low | Medium |
| 24 | RFC 3627 — match ergonomics 2024 | Pattern Matching | Low | N/A |
| 25 | SE-0408 — pack iteration | Iterators | Low | High |

---

## Quick Wins

High return, low effort — implementable in a single session:

1. **PEP 616** — `str.removeprefix`/`removesuffix` in `Sharpy.Core/Partial.String/`
2. **PEP 702** — `@deprecated` → emit `[Obsolete]` in `RoslynEmitter` + SPY045x warning at call sites
3. **PEP 822** — `d"""..."""` dedented string lexing in `Lexer/Lexer.cs`, zero codegen change
4. **RFC 3721** — infer `E` from enclosing return type in `try` expressions via `TypeInferenceService`
5. **PEP 705/767** — `ReadOnly[T]` annotation enforcement in `PropertyValidator`, emit `{ get; }` C# property

---

## Syntax Sugar

### PEP 822 — Dedented Multiline d-strings
**Status:** Draft, Python 3.15 target  
**Priority: High | Effort: Low**

`d"""..."""` automatically strips leading indentation matching the closing `"""` line. Embedded SQL, XML, JSON, and code templates in indented functions currently carry spurious whitespace or require `textwrap.dedent()` manually.

**Sharpy implementation:** Pure lexer transformation — detect `d` prefix on triple-quoted strings, strip common leading whitespace at lex time. Zero codegen change. C# 11 raw string literals (`"""`) do the same thing; Sharpy's `d` prefix matches the Python proposal.

**Touches:** `Lexer/Lexer.cs`

---

### PEP 671 — Late-Bound Function Argument Defaults
**Status:** Draft, Python 3.12 target  
**Priority: High | Effort: Medium**

`def f(x, y => x + 1)` — defaults evaluated at call time rather than definition time. Eliminates both the mutable-default footgun (`def f(lst=[])`) and the verbose None-guard pattern (`if y is None: y = x + 1`).

**Sharpy implementation:** Lexer: new `=>` token in parameter position (or reuse `->` with context). Parser: extend `FunctionParameter` AST node with a `DefaultKind.LateBinding` variant. Semantic: validate that late-bound defaults only reference prior parameters. CodeGen: emit the null-check guard body in `ClassMembers.Methods.cs`.

**Touches:** `Lexer/`, `Parser/Ast/`, `Semantic/TypeChecker.Definitions.cs`, `CodeGen/RoslynEmitter.ClassMembers.Methods.cs`

---

### PEP 798 — Unpacking in Comprehensions
**Status:** Accepted, Python 3.15  
**Priority: Medium | Effort: Low**

`[*it for it in its]` flattens nested iterables inline. `{**d for d in dicts}` merges dicts inline. Currently requires manual `itertools.chain.from_iterable` or nested comprehensions.

**Sharpy implementation:** Parser extension on comprehension expression to allow starred prefix. CodeGen: `*` emits `SelectMany`, `**` emits `Aggregate` with dict merge.

**Touches:** `Parser/Ast/`, `CodeGen/RoslynEmitter.Expressions.Comprehensions.cs`

---

### SE-0380 — if and switch as Expressions
**Status:** Accepted, Swift 5.9  
**Priority: Low | Effort: High**

`let x = if condition { a } else { b }` — block-style conditionals usable in expression position. Sharpy already has `a if cond else b` (Python ternary), so the gain is limited to cases where branch bodies require statements.

**Sharpy implementation:** Requires parser disambiguation between statement-level `if` and expression-level `if`. C# switch expressions are the codegen target. High effort relative to benefit given existing ternary support.

---

### PEP 758 — except Without Parentheses
**Status:** Final, Python 3.14  
**Priority: Low | Effort: Low**

`except ValueError, TypeError:` instead of `except (ValueError, TypeError):`. Minor ergonomics; saves two characters.

**Touches:** `Parser/Parser.Statements.cs`

---

## Type System

### PEP 696 — Type Defaults for Type Parameters
**Status:** Final, Python 3.13  
**Priority: High | Effort: Medium**

`class Result[T, E = Exception]:` — type parameters get default types, so `Result[int]` infers `Result[int, Exception]` without an explicit second argument. Directly useful for Sharpy's `Result[T, E]` and `Optional[T]` where the error type is almost always `Exception`.

**Sharpy implementation:** Add `DefaultType` property to `TypeParameterSymbol`. Update `GenericTypeInferenceService` to fall back to the declared default when inference produces no constraint. Pure semantic-phase feature; no codegen change.

**Touches:** `Semantic/Symbols.cs`, `Semantic/TypeResolver.cs`, `Semantic/GenericTypeInferenceService.cs`

---

### PEP 742 — TypeIs for Type Narrowing
**Status:** Final, Python 3.13  
**Priority: High | Effort: Medium**

`def is_str(x: object) -> TypeIs[str]:` — a stronger `TypeGuard` that narrows in both the positive and negative branches, and is covariant. User-defined type predicates work as naturally as `isinstance`.

**Sharpy implementation:** Add `TypeIsType` to the `SemanticType` hierarchy. Update `_narrowingContext` in `TypeChecker` to narrow both branches when the return type is `TypeIs[X]`. The existing `TypeGuard` implementation is the template.

**Touches:** `Semantic/SemanticType.cs`, `Semantic/TypeChecker.cs`, `Semantic/TypeChecker.Expressions.cs`

---

### PEP 702 — @deprecated Decorator
**Status:** Final, Python 3.13  
**Priority: High | Effort: Low**

`@deprecated("Use new_func instead")` marks APIs as deprecated; type checkers emit warnings at call sites. Maps perfectly to .NET's `[Obsolete("...")]` attribute.

**Sharpy implementation:** Add `deprecated` to `DecoratorNames`. Emit `[Obsolete("...")]` in `RoslynEmitter.ClassMembers.cs`. Add SPY045x warning diagnostic at call sites in `TypeChecker.Expressions.Access.Calls.cs`.

**Touches:** `Semantic/`, `CodeGen/RoslynEmitter.ClassMembers.cs`, `Diagnostics/DiagnosticCodes.cs`

---

### PEP 675 — LiteralString Type
**Status:** Final, Python 3.11  
**Priority: Medium | Effort: Medium**

A compile-time type that accepts only string literals and concatenations thereof — not runtime-computed strings. Enables SQL-injection-safe APIs: `def query(sql: LiteralString): ...`.

**Sharpy implementation:** Add `LiteralStringType` as a subtype of `BuiltinType.Str`. Type checker tracks whether a string expression is composed purely of literals. Concatenation of two `LiteralString` values produces `LiteralString`; assignment from a runtime `str` is an error. Emits as `string` in C# — purely compile-time.

**Touches:** `Semantic/SemanticType.cs`, `Semantic/TypeChecker.Expressions.Literals.cs`

---

### PEP 705 / PEP 767 — ReadOnly Type Qualifier
**Status:** PEP 705 Final (Python 3.13), PEP 767 Draft (Python 3.15)  
**Priority: Medium | Effort: Low**

`x: ReadOnly[int]` on a class attribute makes it a read-only property enforced at compile time. A zero-ceremony alternative to explicit `@property` boilerplate for simple immutable fields.

**Sharpy implementation:** Recognize `ReadOnly` as a special annotation in `TypeResolver`. `PropertyValidator` enforces no setter is present. CodeGen emits a `{ get; }` (or `{ get; init; }`) C# property.

**Touches:** `Semantic/TypeResolver.cs`, `Semantic/Validation/PropertyValidator.cs`, `CodeGen/RoslynEmitter.ClassMembers.Properties.cs`

---

### PEP 646 — Variadic Generics / TypeVarTuple
**Status:** Final, Python 3.11  
**Priority: Medium | Effort: High**

`def f[*Ts](*args: *Ts) -> tuple[*Ts]` — type-safe variadic generics. Eliminates `@overload` proliferation for `zip`, `map`, and similar functions.

**Sharpy implementation:** C# has no direct equivalent for heterogeneous type packs. Implementation approach: emit multiple `@overload` stubs at codegen time (up to N arities), or use tuple-based overloads. `TupleType` in `SemanticType.cs` provides semantic grounding. Significant complexity at the .NET boundary.

---

### PEP 612 — ParamSpec
**Status:** Final, Python 3.10  
**Priority: Medium | Effort: High**

`P = ParamSpec('P')` lets decorator authors preserve the full parameter signature of the wrapped function: `def decorator(f: Callable[P, T]) -> Callable[P, T]`. Decorator type signatures currently lose all parameter information.

**Sharpy implementation:** Add `ParamSpecType` to `SemanticType`. Update `GenericTypeInferenceService` for ParamSpec inference. Codegen must preserve the concrete `Func<>` delegate type through the decoration. Sharpy's existing lambda/decorator infrastructure provides some foundation.

---

## Error Handling

### RFC 3721 — Inferred Error Type in try Blocks
**Status:** Accepted, Rust  
**Priority: High | Effort: Low**

`try { ... }?` — the `E` in `Result[T, E]` is inferred from the enclosing function's declared return type rather than requiring an explicit annotation. Sharpy already has `try` expressions; making the error type annotation optional when it can be inferred is a pure ergonomics win.

**Sharpy implementation:** Extend `TypeInferenceService` to look up the enclosing function's return type and extract `E` from `Result[T, E]` when the `try` expression's type is ambiguous.

**Touches:** `Semantic/TypeInferenceService.cs`, `Semantic/TypeChecker.Expressions.cs`

---

### PEP 654 — Exception Groups and except*
**Status:** Final, Python 3.11  
**Priority: Medium | Effort: Medium**

`ExceptionGroup` bundles multiple concurrent exceptions. `except* ValueError as eg:` catches groups of exceptions by type. Designed for async/concurrent error aggregation.

**Sharpy implementation:** `ExceptionGroup` maps to C#'s `AggregateException`. Add `ExceptStar` handler variant to the `ExceptHandler` AST node. CodeGen emits `catch (AggregateException ae) { foreach (var ex in ae.InnerExceptions.OfType<T>()) { ... } }`. A lightweight `ExceptionGroup` wrapper in `Sharpy.Core` handles the tree-structured case.

**Touches:** `Parser/Ast/`, `Semantic/TypeChecker.Statements.cs`, `CodeGen/RoslynEmitter.Statements.cs`, `Sharpy.Core/`

---

### PEP 678 — Enriching Exceptions with Notes
**Status:** Final, Python 3.11  
**Priority: Medium | Effort: Low**

`exception.add_note("hint text")` appends contextual notes to an exception. Notes appear in tracebacks and let intermediate call frames add context without wrapping exceptions.

**Sharpy implementation:** Add `add_note(str)` to the base `Exception` wrapper in `Sharpy.Core` using a `ConditionalWeakTable<Exception, List<string>>`. Update the exception renderer to display notes.

**Touches:** `Sharpy.Core/` (exception extension)

---

### RFC 3137 — let-else
**Status:** Stable, Rust 1.65  
**Priority: Low | Effort: Medium**

`let Ok(x) = result else { return; }` — binds a pattern or diverges; the binding is in the outer scope. Eliminates deeply-nested `match`/`if let` staircases for early-exit validation.

**Sharpy relevance:** Sharpy's `match`/`case` already covers this pattern in two lines. The specific value is in single-expression guard unwrapping for `Result`/`Optional` types, but the existing syntax is not onerous enough to warrant a new statement form.

---

## Pattern Matching

### RFC 3637 — Guard Patterns in Match
**Status:** Accepted, Rust  
**Priority: High | Effort: Medium**

Per-alternative guards within or-patterns: `case (Some(x) if x > 0) | None:` — the guard applies only to the `Some` branch. Currently in Sharpy, a guard on a `case` applies to the entire clause; splitting is required.

**Sharpy implementation:** Extend the pattern grammar to allow guards nested inside or-pattern alternatives. `ExhaustivenessValidator` and `TypeChecker.Statements.Patterns.cs` need updates. CodeGen: nested `if` within the C# switch arm (C# `when` clauses already support this pattern).

**Touches:** `Parser/Ast/`, `Semantic/TypeChecker.Statements.Patterns.cs`, `Semantic/Validation/ExhaustivenessValidator.cs`, `CodeGen/RoslynEmitter.Patterns.cs`

---

### RFC 3535 — Constants in Patterns
**Status:** Accepted, Rust  
**Priority: Medium | Effort: Low**

Named constants (not just literals) directly in match arm position: `case MAX:` instead of `case x if x == MAX:`. The compiler verifies the constant has structural equality.

**Sharpy implementation:** Extend `PatternMatcher` to look up identifiers in the symbol table and treat module-level `const` values as value patterns rather than capture patterns.

**Touches:** `Semantic/TypeChecker.Statements.Patterns.cs`, `CodeGen/RoslynEmitter.Patterns.cs`

---

### RFC 3627 — Match Ergonomics 2024
**Status:** Accepted, Rust Edition 2024  
**Priority: Low | Not Applicable**

Refines how reference patterns interact with structural matching. The underlying problem (borrow vs. owned) does not exist in Sharpy/.NET (GC). The ergonomics principle (implicit vs. explicit matching annotations) is a design note rather than an actionable feature.

---

## Iterators / Generators

### RFC 3513 — gen {} Blocks
**Status:** Accepted, Rust 2024  
**Priority: High | Effort: Medium**

`gen { yield 1; yield 2; }` produces an iterator inline — an anonymous generator expression with full statement-level control flow (loops, conditionals, early returns). Analogous to `async {}` for futures.

**Sharpy implementation:** Sharpy already has `yield`/`yield from` in functions. Extend the parser to allow `gen { ... }` as an expression that produces a generator object. CodeGen: emit an immediately-invoked local iterator function returning `IEnumerable<T>` — a clean C# target.

**Touches:** `Parser/Ast/`, `Semantic/TypeChecker.Expressions.cs`, `Semantic/Validation/GeneratorValidator.cs`, `CodeGen/RoslynEmitter.Expressions.cs`

---

### SE-0408 — Pack Iteration
**Status:** Accepted, Swift 6.0  
**Priority: Low | Effort: High | Blocked**

`for each x in (a, b, c):` iterates over a heterogeneous value pack. Requires variadic generics (PEP 646) as a prerequisite; blocked until that is implemented.

---

### PEP 709 — Inlined Comprehensions
**Status:** Final, Python 3.12  
**Priority: N/A — Already Done**

Comprehensions no longer create a nested function scope. Sharpy's comprehensions already emit as LINQ `Select`/`Where` chains, not as nested lambdas with closure overhead. This is effectively already the case.

---

## Standard Library

### PEP 750 — Template Strings (t-strings)
**Status:** Final, Python 3.14  
**Priority: High | Effort: Medium**

`t"Hello {name}"` produces a `Template` object (not a `str`) that retains the string parts and interpolated values separately. Enables safe SQL, HTML, and shell escaping at the library level without string-level injection risk.

**Sharpy implementation:** Add `TStringLiteral` AST node to the parser. CodeGen emits construction of a `Sharpy.Template` struct in `Sharpy.Core` holding `string[] parts` and `object[] values`. The natural codegen target is C#'s `FormattableString` (via `$"..."` interpolated string handlers) — already in .NET, zero runtime dependency.

**Touches:** `Lexer/`, `Parser/Ast/`, `Semantic/TypeChecker.Expressions.Literals.cs`, `CodeGen/RoslynEmitter.Expressions.Literals.cs`, `Sharpy.Core/` (Template type)

---

### PEP 616 — str.removeprefix / str.removesuffix
**Status:** Final, Python 3.9  
**Priority: High | Effort: Low**

`"foobar".removeprefix("foo")` → `"bar"`. Non-destructive: returns the original string if the prefix is not found. The existing `lstrip` and slice-based approaches are error-prone.

**Sharpy implementation:** Add to `Sharpy.Core/Partial.String/`. Emits to `s.StartsWith(prefix) ? s[prefix.Length..] : s`.

**Touches:** `Sharpy.Core/Partial.String/`

---

### PEP 814 — frozendict Built-in Type
**Status:** Final, Python 3.15  
**Priority: Medium | Effort: Low**

`frozendict` — an immutable, hashable mapping usable as a dict key or set member.

**Sharpy implementation:** Add `FrozenDict[K, V]` to `Sharpy.Core` wrapping `System.Collections.Frozen.FrozenDictionary<K, V>` (available since .NET 8). Register in `ModuleRegistry` as a builtin type. Update `TypeSyntaxMapper`.

**Touches:** `Sharpy.Core/`, `Semantic/Registries/ModuleRegistry.cs`, `CodeGen/TypeSyntaxMapper.cs`

---

### RFC 3681 — Default Field Values for Structs
**Status:** Accepted, Rust  
**Priority: Medium | Effort: Medium**

`x: int = 42` directly in the class body for dataclass/struct definitions — field-level defaults visible at the declaration site rather than only in the constructor signature. Aligns with `dataclasses.field(default=...)` made syntactic.

**Sharpy implementation:** Extend `ClassMembers.Dataclass.cs` and `StructRulesValidator` to recognize field-level defaults in the class body. CodeGen: emit the default value in the constructor body.

**Touches:** `Semantic/Validation/StructRulesValidator.cs`, `CodeGen/RoslynEmitter.ClassMembers.Dataclass.cs`, `CodeGen/RoslynEmitter.ClassMembers.Constructors.cs`

---

## Proposals Explicitly Not Recommended

| Proposal | Reason |
|----------|--------|
| Swift SE-0390/0427/0432 — noncopyable/move-only types | Fundamentally conflicts with .NET GC; cannot be expressed |
| Swift SE-0377 — borrowing/consuming parameters | No borrow checker in .NET; ownership managed by GC |
| Swift macros (SE-0382/0389/0415) | C# Source Generators fill this role; Sharpy would need a separate macro expansion pass before Roslyn; large effort with unclear boundary vs. decorators |
| Rust RFC 3617 — precise capturing (`use<..>`) | Lifetime tracking is a Rust-only concern; irrelevant under GC |
| PEP 703 — optional GIL | CPython internals concern; Sharpy runs on the .NET threading model |
