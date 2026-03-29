# C# 9.0 Gap Analysis

Systematic comparison of C# 9.0 capabilities vs Sharpy's current implementation.
Goal: identify what Sharpy must add to serve as a full C# 9.0 replacement.

> **Methodology:** Audited all C# features (1.0 through 9.0), cross-referenced against
> the Sharpy language specification (`docs/language_specification/`), compiler implementation
> (lexer tokens, AST nodes, codegen), 1,571 integration test fixtures, and open GitHub issues.
>
> **C# version target:** Sharpy.Core targets `LangVersion 9.0` / `netstandard2.1`, making
> C# 9.0 the effective ceiling for generated code.
>
> **Date:** 2026-03-29

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

---

## Tier 2: Medium Priority (Common but Workaroundable)

These features have workarounds in Sharpy but add friction when porting C# code.

### 13. Static Classes

- **C# feature:** `static class` — cannot be instantiated, all members static (C# 2.0)
- **Sharpy status:** MISSING — modules serve a similar purpose (module-level functions are
  emitted as static methods on a static class), but user-defined static classes aren't supported.
- **Workaround:** Use module-level functions or a regular class with only `@static` members.

### 14. Multi-Dimensional and Jagged Arrays

- **C# feature:** `int[,]` (2D), `int[,,]` (3D), `int[][]` (jagged) (C# 1.0)
- **Sharpy status:** MISSING — only 1D `array[T]` is supported
- **Workaround:** `list[list[int]]` for jagged, no clean workaround for true multi-dimensional.

### 15. LINQ Query Syntax

- **C# feature:** `from x in y where z select x` (C# 3.0)
- **Sharpy status:** MISSING — LINQ method syntax works via .NET interop
  (`items.where(lambda x: x > 0).select(lambda x: x * 2)`)
- **Workaround:** Method syntax + comprehensions cover most use cases.

### 16. Anonymous Types

- **C# feature:** `new { Name = "x", Value = 1 }` (C# 3.0)
- **Sharpy status:** MISSING
- **Workaround:** Named tuples `(name: "x", value: 1)` cover most use cases.

### 17. Static Lambdas

- **C# feature:** `static x => x + 1` — prevents closure capture (C# 9.0)
- **Sharpy status:** MISSING
- **Impact:** Performance optimization to avoid accidental captures.

### 18. `readonly struct`

- **C# feature:** `readonly struct` — all fields immutable (C# 7.2)
- **Sharpy status:** MISSING — `@dataclass(frozen=True)` on structs provides similar
  semantics but doesn't emit `readonly struct`.

### 19. `ref struct`

- **C# feature:** `ref struct` — stack-only value types, cannot be boxed (C# 7.2)
- **Sharpy status:** MISSING — `Span<T>` and `ReadOnlySpan<T>` can be used via .NET interop
  but users cannot define their own `ref struct`.

### 20. `readonly` Fields

- **C# feature:** `readonly` field modifier — assignable only in constructor (C# 1.0)
- **Sharpy status:** PARTIAL — `const` exists for compile-time constants. No runtime
  `readonly` equivalent (assigned once in constructor, then immutable).

### 21. Range/Index Operators

- **C# feature:** `array[^1]`, `array[1..3]` (C# 8.0)
- **Sharpy status:** MISSING — Python-style slicing `arr[1:3]`, `arr[-1]` covers most
  use cases but doesn't produce `System.Range`/`System.Index` types.

### 22. `volatile` Fields

- **C# feature:** `volatile` modifier for thread-safe field access (C# 1.0)
- **Sharpy status:** MISSING
- **Impact:** Niche but important for lock-free programming.

### 23. Lambda Type Annotations

- **C# feature:** `(int x, int y) => x + y` — typed lambda parameters (C# 3.0)
- **Sharpy status:** RFC open (#417) — syntax ambiguity with `lambda x: int: x + 1`
- **Impact:** Required for disambiguation when type inference is insufficient.

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

For completeness, these C# 9.0 features are already implemented or have Pythonic equivalents:

| C# Feature | Sharpy Equivalent | Status |
|------------|-------------------|--------|
| Classes, inheritance, abstract classes | `class`, `(Base)`, `@abstract` | Implemented |
| Structs | `struct` | Implemented |
| Interfaces + default methods | `interface` + default implementations | Implemented |
| Enums (incl. flags) | `enum` + `@flags` | Implemented |
| Generics + constraints | `class Foo[T: IBar]` | Implemented |
| Generic variance (`in`/`out`) | `class Foo[out T]` | Implemented |
| Properties (auto, get/set/init) | `property` keyword | Implemented |
| Events (auto + custom) | `event` keyword | Implemented |
| Delegates | `delegate` keyword | Implemented |
| Operator overloading | Dunder methods | Implemented |
| Async/await, async streams | `async def` / `await` / `async for` | Implemented |
| Generators/iterators | `yield` / `yield from` | Implemented |
| Pattern matching (all C# 9.0 patterns) | `match` statement | Implemented |
| String interpolation | f-strings | Implemented |
| Null-conditional (`?.`) | Supported | Implemented |
| Null-coalescing (`??`, `??=`) | Supported | Implemented |
| Exception handling (try/except/finally) | Supported | Implemented |
| Context managers / IDisposable | `with` statement | Implemented |
| Named/default arguments | Supported | Implemented |
| Method overloading | Supported | Implemented |
| Constructor chaining | `self.__init__(...)` | Implemented |
| Virtual/override/sealed | `@virtual` / `@override` / `@final` | Implemented |
| Access modifiers | `@public`/`@private`/`@protected`/`@internal` | Implemented |
| Static methods | `@static` / implicit (no `self`) | Implemented |
| Constants | `const` keyword | Implemented |
| Type aliases | `type X = Y` | Implemented |
| Tuples + deconstruction | Named tuples + unpacking | Implemented |
| Collection literals + comprehensions | `[...]`, `{...}`, comprehensions | Implemented |
| Extension methods | Via .NET interop | Implemented |
| Variadic params | `*args` | Implemented |
| Tagged unions | `union` keyword | Implemented |
| Result/Optional types | `Result[T, E]` / `Optional[T]` | Implemented |
| Init-only setters | `property init` | Implemented |
| Covariant return types | Supported | Implemented |
| Local functions | Nested `def` | Implemented |
| Comparison chaining | `a < b < c` | Implemented |
| Walrus operator | `:=` | Implemented |
| Pipe operator | `|>` | Implemented |
| Partial application | `_` placeholder | Implemented |
| Try/Maybe expressions | `try expr` / `maybe expr` | Implemented |

---

## Recommended Implementation Order

For systematic gap closure, suggested sequence (respects dependencies):

1. **`do...while`** — smallest scope, no dependencies, completes control flow
2. **`nameof`** — small scope, high value
3. **Exception filters** — small parser change, high value
4. **`lock` statement** — small scope, high value for concurrency
5. **`ref`/`out`/`in` parameters** (#419) — large scope but highest interop impact
6. **`checked`/`unchecked`** — moderate scope
7. **Nested types** — moderate scope, unlocks patterns
8. **`private protected`** — small scope
9. **Conversion operators** — moderate scope, type system completeness
10. **Static constructors** — moderate scope
11. **Partial classes** — large scope, unlocks code-gen scenarios
12. **Records + `with` expressions** — largest scope, C# 9.0 flagship
