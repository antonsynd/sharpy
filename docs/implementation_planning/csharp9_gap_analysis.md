# C# 9.0 Gap Analysis

Systematic comparison of C# 9.0 capabilities vs Sharpy's current implementation.
Goal: identify what Sharpy must add to serve as a full C# 9.0 replacement.

> **Methodology:** Audited all C# features (1.0 through 9.0), cross-referenced against
> the Sharpy language specification (`docs/language_specification/`), compiler implementation
> (lexer tokens, AST nodes, codegen), 1,571 integration test fixtures, and open GitHub issues.
> Second pass verified all claims against actual compiler source (lexer, parser AST, codegen
> emitter, semantic analysis) and identified 8 additional gaps not in the initial audit.
>
> **C# version target:** Sharpy.Core targets `LangVersion 9.0` / `netstandard2.1`, making
> C# 9.0 the effective ceiling for generated code.
>
> **Last updated:** 2026-03-29

---

## Summary

- **14 Tier 1 gaps** (high priority, blocking for C# replacement)
- **17 Tier 2 gaps** (medium priority, workaroundable)
- **14 Tier 3 items** (intentionally excluded or very niche)
- **50+ C# features** already covered with Pythonic equivalents
- **6 open GitHub issues** tracking specific gaps

---

## Open Issues Already Tracking Gaps

| Issue | Title | Category |
|-------|-------|----------|
| #417 | RFC: Lambda parameter type annotations syntax | Syntax |
| #419 | Implement struct parameter modifiers (`in[]`, `mut[]`, `out[]`) | Parameters |
| #424 | RFC: Combining exception types in try expressions | Error handling |
| #476 | `not (x is None)` does not narrow types | Type narrowing |
| #416 | Proposal: Property observers (willset/didset) | Properties |
| #108 | str.format() parser | Stdlib |

---

## Tier 1: High Priority (Blocking for C# Replacement)

These features are commonly used in C# codebases. Without them, Sharpy cannot express
many real-world C# 9.0 programs.

### 1. `ref`/`out`/`in` Parameter Modifiers

- **C# feature:** `ref`, `out`, `in` on parameters and locals (C# 1.0, extended in 7.0/7.2)
- **Sharpy status:** SPEC-ONLY — `ref[T]`, `out[T]`, `in[T]` syntax specified in
  `docs/language_specification/parameter_modifiers.md` but not implemented. Deferred post-v0.2.x.
- **Tracking:** #419
- **Impact:** Blocks interop with a huge .NET API surface (`int.TryParse`, `Dictionary.TryGetValue`,
  `Interlocked.CompareExchange`, `Span<T>` APIs, etc.)
- **Scope:** Lexer (contextual keywords), Parser (parameter modifier syntax), Semantic
  (validation), CodeGen (emit `ref`/`in`/`out` C# modifiers)

### 2. Implicit/Explicit Conversion Operators

- **C# feature:** `implicit operator` / `explicit operator` (C# 1.0)
- **Sharpy status:** SPEC-ONLY — `@implicit` / `@explicit` decorators specified in
  `docs/language_specification/conversion_operators.md` but not implemented. Deferred post-v0.2.x.
- **Impact:** Required for complete type system. C# libraries define custom conversions
  (`TimeSpan` from `int`, `JsonElement` to primitives, etc.)
- **Scope:** Parser (decorator recognition), Semantic (conversion validation), CodeGen
  (emit `implicit operator`/`explicit operator`)

### 3. Nested Types

- **C# feature:** Classes, structs, interfaces, enums, delegates declared inside other types (C# 1.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Very common C# pattern. Used for Builder pattern, private helper types,
  `Node` inside `LinkedList<T>`, enum-like nested classes, etc.
- **Scope:** Parser (allow type declarations inside class/struct bodies), Semantic (nested
  scope resolution, access to outer type), CodeGen (emit nested C# types)

### 4. Exception Filters (`when` clause)

- **C# feature:** `catch (Exception ex) when (ex.Message.Contains("x"))` (C# 6.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Important for robust error handling — filter without catching (preserves stack trace).
  Used in ASP.NET middleware, retry logic, logging.
- **Possible syntax:** `except ValueError as e when e.code == 42:`
- **Scope:** Parser (add `when` clause to except), CodeGen (emit `catch ... when`)

### 5. `lock` Statement

- **C# feature:** `lock (obj) { ... }` for thread synchronization (C# 1.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Essential for any multi-threaded code. Every concurrent C# program uses `lock`.
- **Possible syntax:** `lock obj:` (Python-style block) or reuse `with` + a lock context manager
- **Scope:** Parser (new statement or `with` pattern), CodeGen (emit `lock`)

### 6. `do...while` Loops

- **C# feature:** `do { body } while (condition);` (C# 1.0)
- **Sharpy status:** MISSING — `do` is a reserved keyword in the lexer but not parsed
- **Impact:** Common loop pattern where body must execute at least once. Currently requires
  `while True:` + `if not cond: break` workaround.
- **Possible syntax:** `do: body while condition` or `loop: body while condition`
- **Scope:** Parser (new statement), CodeGen (emit `do...while`)

### 7. Static Constructors

- **C# feature:** `static ClassName() { ... }` — runs once before first use (C# 1.0)
- **Sharpy status:** MISSING — no spec, no implementation. Sharpy has `const` for static
  initialization but no imperative static init block.
- **Impact:** Used for expensive one-time initialization, static caches, registration.
- **Possible syntax:** `@static def __init__():` (class-level, no `self`)
- **Scope:** Semantic (validate static constructor rules), CodeGen (emit `static ClassName()`)

### 8. `checked`/`unchecked` Arithmetic

- **C# feature:** `checked { }` / `unchecked { }` blocks and expressions (C# 1.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Numeric safety — `checked` throws on overflow, `unchecked` wraps. Important
  for financial code, hashing, crypto.
- **Possible syntax:** `checked:` / `unchecked:` blocks, or decorator-based
- **Scope:** Parser, CodeGen

### 9. Partial Classes

- **C# feature:** `partial class` — split a class across multiple files (C# 2.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Widely used in C# for code generators (EF, WinForms, source generators),
  separating auto-generated code from hand-written code.
- **Possible syntax:** `@partial class Foo:` or automatic merging of same-named classes
- **Scope:** Parser (decorator or keyword), Semantic (merge declarations), CodeGen (emit
  `partial class`)
- **Note:** Extended partial methods (C# 9.0) depend on this.

### 10. Records and `with` Expressions

- **C# feature:** `record` type with value equality, `with` expressions for non-destructive
  mutation (C# 9.0)
- **Sharpy status:** MISSING — `@dataclass` provides similar value-equality semantics
  (auto-generated `__eq__`, `__hash__`, `__repr__`, `__init__`) but is not the same as C# records:
  - No `with` expressions (`p2 = p1 with { X = 5 }`)
  - No record inheritance
  - No `PrintMembers` pattern
  - No positional deconstruction
- **Impact:** C# 9.0 flagship feature. Modern C# code uses records extensively for DTOs,
  value objects, immutable data.
- **Possible approach:** Extend `@dataclass` to support `with`-like syntax, or add `record`
  keyword that compiles to C# `record`
- **Scope:** Major feature — parser, semantic, codegen

### 11. `private protected` Access Modifier

- **C# feature:** `private protected` — accessible within same assembly AND derived types (C# 7.2)
- **Sharpy status:** MISSING — Sharpy has `@public`, `@private`, `@protected`, `@internal`
  but not the combined `@private @protected` / `private protected`
- **Impact:** Moderate — used in library design to restrict access to derived types within
  the same assembly.
- **Scope:** Semantic (validate combined modifier), CodeGen (emit `private protected`)

### 12. `nameof` Equivalent

- **C# feature:** `nameof(x)` — compile-time name of symbol as string (C# 6.0)
- **Sharpy status:** MISSING — no spec, no implementation
- **Impact:** Widely used for refactoring-safe argument validation (`ArgumentNullException(nameof(param))`),
  `INotifyPropertyChanged`, logging.
- **Possible syntax:** `nameof(x)` builtin function
- **Scope:** Parser (builtin or special form), Semantic (resolve at compile time), CodeGen
  (emit string literal)

### 13. Object Initializers

- **C# feature:** `new Foo { X = 1, Y = 2 }` — construct + set properties in one expression (C# 3.0)
- **Sharpy status:** MISSING — only constructor calls supported. No way to construct an object
  and set properties in a single expression.
- **Impact:** Ubiquitous in C# for DTOs, EF entities, builder patterns, UI frameworks, LINQ
  projections (`select new { ... }`). Independent of records (#10).
- **Possible syntax:** `Foo(x=1, y=2)` (extend named args to set properties) or
  `Foo() with { x: 1, y: 2 }` (reuse `with` syntax)
- **Scope:** Parser (new expression syntax), Semantic (resolve property assignments), CodeGen
  (emit `ObjectCreationExpression` with initializer list)

### 14. `default` Literal and `default(T)` Expression

- **C# feature:** `default(T)` (C# 2.0) and bare `default` literal (C# 7.1) — produces the
  default value for any type (`0` for `int`, `null` for reference types, zeroed struct, etc.)
- **Sharpy status:** MISSING — no `default` keyword in lexer or parser
- **Impact:** Essential in generic code (`return default;`, `T value = default;`), used for
  sentinel values, optional parameter defaults, and collection initialization.
- **Scope:** Lexer (keyword), Parser (expression), Semantic (resolve type from context),
  CodeGen (emit `default` / `default(T)`)

---

## Tier 2: Medium Priority (Common but Workaroundable)

These features have workarounds in Sharpy but add friction when porting C# code.

### 15. Raise/Throw Expressions

- **C# feature:** `throw` as an expression — `x ?? throw new ArgumentException()`,
  `condition ? value : throw ...` (C# 7.0)
- **Sharpy status:** MISSING — `raise` is statement-only. Cannot be used in `??`, ternary,
  or null-coalescing contexts. The compiler internally generates C# throw expressions in
  codegen, but users cannot write them.
- **Impact:** Common pattern for argument validation and null-guard one-liners.
- **Possible syntax:** Allow `raise` in expression position: `x ?? raise ValueError("missing")`
- **Scope:** Parser (allow raise in expression context), CodeGen (emit `ThrowExpression`)
- **Workaround:** Multi-line `if`/`raise` blocks.

### 16. Multi-Catch Exception Types

- **C# feature:** `catch (IOException | TimeoutException ex)` — catch multiple types in
  one clause (C# 6.0, via exception filters)
- **Sharpy status:** MISSING — each `except` clause catches a single type. No union syntax.
- **Tracking:** #424 (RFC open for try expressions; also applies to except clauses)
- **Impact:** Reduces boilerplate when the same handler applies to multiple exception types.
- **Possible syntax:** `except ValueError | TypeError as e:` (Python 3 style)
- **Scope:** Parser (union type in except), CodeGen (emit multiple catch clauses or filter)
- **Workaround:** Separate `except` clauses with duplicated bodies.

### 17. `is` Type Pattern Outside `match`

- **C# feature:** `if (x is Type t) { ... }` — inline type test + binding (C# 7.0+),
  also `x is not null`, `x is > 5` (C# 9.0 relational patterns in expressions)
- **Sharpy status:** PARTIAL — `is None`, `is not None`, and `isinstance(x, Type)` work
  in `if` statements with type narrowing. But type-binding patterns (`if x is int n:`) and
  relational patterns (`if x is > 5:`) are only available inside `match` statements.
- **Impact:** Very common C# 7+ idiom. Nearly every C# codebase uses `if (x is T t)`.
- **Possible syntax:** `if x is int as n:` or `if isinstance(x, int) as n:`
- **Scope:** Parser (allow pattern expressions in `if`), Semantic (type narrowing + binding),
  CodeGen (emit `is` pattern)

### 18. `notnull` Generic Constraint

- **C# feature:** `where T : notnull` — constrains type parameter to non-nullable (C# 8.0)
- **Sharpy status:** MISSING — constraint AST only defines `ClassConstraint`, `StructConstraint`,
  `NewConstraint`, and `TypeConstraint`. No `notnull` or `unmanaged` variants.
- **Impact:** Important for generic APIs that must reject nullable type arguments.
  `unmanaged` constraint (C# 7.3) is also missing but more niche.
- **Scope:** Parser (new constraint type), Semantic (validate nullability), CodeGen (emit constraint)

### 19. Static Classes

- **C# feature:** `static class` — cannot be instantiated, all members static (C# 2.0)
- **Sharpy status:** MISSING — modules serve a similar purpose (module-level functions are
  emitted as static methods on a static class), but user-defined static classes aren't supported.
- **Workaround:** Use module-level functions or a regular class with only `@static` members.

### 20. Multi-Dimensional and Jagged Arrays

- **C# feature:** `int[,]` (2D), `int[,,]` (3D), `int[][]` (jagged) (C# 1.0)
- **Sharpy status:** MISSING — only 1D `array[T]` is supported
- **Workaround:** `list[list[int]]` for jagged, no clean workaround for true multi-dimensional.

### 21. LINQ Query Syntax

- **C# feature:** `from x in y where z select x` (C# 3.0)
- **Sharpy status:** MISSING — LINQ method syntax works via .NET interop
  (`items.where(lambda x: x > 0).select(lambda x: x * 2)`)
- **Workaround:** Method syntax + comprehensions cover most use cases.

### 22. Anonymous Types

- **C# feature:** `new { Name = "x", Value = 1 }` (C# 3.0)
- **Sharpy status:** MISSING
- **Workaround:** Named tuples `(name: "x", value: 1)` cover most use cases.

### 23. Static Lambdas

- **C# feature:** `static x => x + 1` — prevents closure capture (C# 9.0)
- **Sharpy status:** MISSING
- **Impact:** Performance optimization to avoid accidental captures.

### 24. `readonly struct`

- **C# feature:** `readonly struct` — all fields immutable (C# 7.2)
- **Sharpy status:** MISSING — `@dataclass(frozen=True)` on structs provides similar
  semantics but doesn't emit `readonly struct`.

### 25. `ref struct`

- **C# feature:** `ref struct` — stack-only value types, cannot be boxed (C# 7.2)
- **Sharpy status:** MISSING — `Span<T>` and `ReadOnlySpan<T>` can be used via .NET interop
  but users cannot define their own `ref struct`.

### 26. `readonly` Fields

- **C# feature:** `readonly` field modifier — assignable only in constructor (C# 1.0)
- **Sharpy status:** PARTIAL — `const` exists for compile-time constants. No runtime
  `readonly` equivalent (assigned once in constructor, then immutable).

### 27. Range/Index Operators

- **C# feature:** `array[^1]`, `array[1..3]` (C# 8.0)
- **Sharpy status:** MISSING — Python-style slicing `arr[1:3]`, `arr[-1]` covers most
  use cases but doesn't produce `System.Range`/`System.Index` types.

### 28. Block-less Disposable Scope

- **C# feature:** `using var x = new Foo();` — scopes disposable to enclosing block without
  nesting (C# 8.0 using declaration)
- **Sharpy status:** MISSING — `with` statement requires an indented block. No way to scope
  a disposable to the enclosing function/block without introducing a nesting level.
- **Impact:** Reduces nesting in methods with multiple disposables.
- **Possible syntax:** `use x = Resource():` at statement level (scoped to enclosing block)
- **Scope:** Parser (new statement), CodeGen (emit `using` declaration)
- **Workaround:** `with Resource() as x:` (adds one nesting level per disposable).

### 29. `volatile` Fields

- **C# feature:** `volatile` modifier for thread-safe field access (C# 1.0)
- **Sharpy status:** MISSING
- **Impact:** Niche but important for lock-free programming.

### 30. Lambda Type Annotations

- **C# feature:** `(int x, int y) => x + y` — typed lambda parameters (C# 3.0)
- **Sharpy status:** RFC open (#417) — syntax ambiguity with `lambda x: int: x + 1`
- **Impact:** Required for disambiguation when type inference is insufficient.

### 31. General Delegate Combination

- **C# feature:** `combined = del1 + del2`, `del1 - del2` — multicast delegate arithmetic (C# 1.0)
- **Sharpy status:** PARTIAL — `+=`/`-=` work for events, but arbitrary delegate combination
  (`combined = handler1 + handler2`) is not supported.
- **Impact:** Low — events cover the primary multicast use case.
- **Workaround:** Use events, or manually invoke multiple delegates.

---

## Tier 3: Low Priority / Intentionally Excluded

These features are either niche, contraddict Sharpy's design axioms, or have clean alternatives.

| Feature | C# Version | Reason for Low Priority |
|---------|-----------|------------------------|
| `goto` / labels | 1.0 | Python philosophy — no goto. Sharpy uses structured control flow. |
| `dynamic` type | 4.0 | Contradicts Axiom 3 (type safety). |
| Unsafe code (`unsafe`, `fixed`, `stackalloc`, pointers) | 1.0 | Safety by design. |
| Function pointers (`delegate*`) | 9.0 | Unsafe / low-level interop only. |
| Preprocessor directives (`#if`, `#define`, `#pragma`) | 1.0 | Could add `@conditional` decorator instead. |
| `nint` / `nuint` (native-sized integers) | 9.0 | Niche platform-dependent types. |
| P/Invoke (`extern` + `[DllImport]`) | 1.0 | Niche native interop. |
| Finalizers (`~ClassName()`) | 1.0 | Spec explicitly says "Use IDisposable instead." |
| `sizeof` operator | 1.0 | Unmanaged types only. |
| Module initializers (`[ModuleInitializer]`) | 9.0 | Niche startup hook. |
| Extended partial methods | 9.0 | Depends on partial classes (Tier 1 #9). |
| `new` member hiding | 1.0 | Generally considered a C# anti-pattern. |
| Target-typed `new` (`Foo f = new(...)`) | 9.0 | N/A — Sharpy uses `Foo(...)` directly. |
| Top-level statements | 9.0 | N/A — Sharpy already has module-level code. |

---

## What Sharpy Already Covers

For completeness, these C# 9.0 features are already implemented or have Pythonic equivalents.
Verified against compiler source (lexer, parser AST, codegen, semantic analysis) and test fixtures.

| C# Feature | Sharpy Equivalent | Status |
|------------|-------------------|--------|
| Classes, inheritance, abstract classes | `class`, `(Base)`, `@abstract` | Implemented |
| Structs | `struct` | Implemented |
| Interfaces + default methods | `interface` + default implementations | Implemented |
| Enums (incl. flags) | `enum` + `@flags` | Implemented |
| Generics + constraints (`class`, `struct`, `new()`, interfaces) | `class Foo[T: IBar]`, `T: class & new()` | Implemented |
| Generic variance (`in`/`out`) | `class Foo[out T]` | Implemented |
| Properties (auto, get/set/init) | `property` keyword | Implemented |
| Events (auto + custom) | `event` keyword | Implemented |
| Delegates | `delegate` keyword | Implemented |
| Operator overloading | Dunder methods | Implemented |
| `operator true`/`operator false` | `__bool__` dunder (emits both) | Implemented |
| Indexers (`this[T]`) | `__getitem__`/`__setitem__` dunders | Implemented |
| Async/await, async streams | `async def` / `await` / `async for` | Implemented |
| Generators/iterators | `yield` / `yield from` | Implemented |
| Pattern matching (all C# 9.0 patterns) | `match` statement | Implemented |
| — Relational patterns (`> 5`, `>= 0`) | `case > 5:` | Implemented |
| — Property patterns (`{ Prop: value }`) | `case Foo(prop=value):` | Implemented |
| — Positional patterns | `case Point(0, y):` | Implemented |
| — Combinatorial patterns (`or`) | `case "a" \| "b":` | Implemented |
| — Wildcard / discard (`_`) | `case _:` | Implemented |
| — Guard clauses | `case x if x > 0:` | Implemented |
| Pattern exhaustiveness checking | Warnings (statements) / errors (expressions) | Implemented |
| String interpolation + format specifiers | f-strings (`f"{val:.2f}"`, `f"{val:04d}"`) | Implemented |
| Null-conditional (`?.`) incl. chaining | `a?.b?.c` with nullable flattening | Implemented |
| Null-coalescing (`??`, `??=`) | Supported | Implemented |
| Exception handling (try/except/finally) | Supported | Implemented |
| Context managers / IDisposable | `with` statement | Implemented |
| Named/default arguments | Supported (compile-time constants only for defaults) | Implemented |
| Method overloading | Supported | Implemented |
| Constructor chaining | `self.__init__(...)` | Implemented |
| Virtual/override/sealed | `@virtual` / `@override` / `@final` | Implemented |
| Access modifiers | `@public`/`@private`/`@protected`/`@internal` | Implemented |
| Static methods | `@static` / implicit (no `self`) | Implemented |
| Constants | `const` keyword (compile-time only) | Implemented |
| Type aliases | `type X = Y` | Implemented |
| Type casting | `x to Type` (direct), `x to Type?` (safe) | Implemented |
| `typeof(T)` | `type(T)` builtin | Implemented |
| Tuples + deconstruction | Named tuples + unpacking | Implemented |
| Collection literals + comprehensions | `[...]`, `{...}`, comprehensions | Implemented |
| Extension methods | Via .NET interop | Implemented |
| Variadic params (`params T[]`) | `*args` | Implemented |
| Arbitrary .NET attributes | Unknown decorators → `[Attribute]` | Implemented |
| Explicit interface implementation | `IInterface.member` syntax | Implemented |
| Tagged unions | `union` keyword | Implemented |
| Result/Optional types | `Result[T, E]` / `Optional[T]` | Implemented |
| Init-only setters | `property init` | Implemented |
| Covariant return types | Supported | Implemented |
| Local functions | Nested `def` | Implemented |
| Digit separators | `1_000_000` (same syntax) | Implemented |
| Binary/hex/octal literals | `0b1010`, `0xFF`, `0o77` | Implemented |
| Raw strings | `r"..."`, `r"""..."""` → C# `@"..."` | Implemented |
| Comparison chaining | `a < b < c` | Implemented |
| Walrus operator | `:=` | Implemented |
| Pipe operator | `\|>` | Implemented |
| Partial application | `_` placeholder | Implemented |
| Try/Maybe expressions | `try expr` / `maybe expr` | Implemented |
| `is None` / `is not None` checks | Type narrowing in `if` | Implemented |
| `isinstance()` type narrowing | Narrows in `if` blocks | Implemented |

---

## Recommended Implementation Order

For systematic gap closure, suggested sequence (respects dependencies and
maximizes value per effort). Grouped into phases.

### Phase 1: Quick wins (small scope, high frequency in C# code)

1. **`do...while`** (#6) — smallest scope, no dependencies, completes control flow
2. **`nameof`** (#12) — small scope, high value for argument validation
3. **`private protected`** (#11) — small scope, semantic + codegen only
4. **`default` literal** (#14) — small scope, essential for generic code

### Phase 2: Control flow completeness

5. **Exception filters (`when`)** (#4) — small parser change, high value
6. **Multi-catch** (#16) — small parser change, reduces boilerplate
7. **Raise expressions** (#15) — allow `raise` in expression position
8. **`lock` statement** (#5) — essential for concurrency
9. **`is` pattern outside `match`** (#17) — very common C# 7+ idiom

### Phase 3: Interop-critical

10. **`ref`/`out`/`in` parameters** (#1, issue #419) — largest interop impact
11. **`checked`/`unchecked`** (#8) — numeric safety
12. **Object initializers** (#13) — ubiquitous in C# for DTOs, EF, etc.

### Phase 4: Type system completeness

13. **Nested types** (#3) — unlocks Builder pattern, private helpers
14. **Conversion operators** (#2) — type system completeness
15. **Static constructors** (#7) — one-time initialization
16. **`notnull` constraint** (#18) — generic API correctness
17. **`readonly` fields** (#26) — runtime immutability

### Phase 5: Large features

18. **Partial classes** (#9) — large scope, unlocks code-gen scenarios
19. **Records + `with` expressions** (#10) — largest scope, C# 9.0 flagship

### Phase 6: Polish (lower frequency, workarounds exist)

20. **Block-less disposable scope** (#28) — convenience
21. **Static classes** (#19) — modules cover most cases
22. **`readonly struct`** (#24), **`ref struct`** (#25) — advanced value types
23. **Static lambdas** (#23) — performance optimization
24. **Lambda type annotations** (#30, issue #417) — pending RFC
25. **Remaining items** — multi-dim arrays, LINQ query syntax, volatile, etc.
