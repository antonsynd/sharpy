# C# 9.0 Gap Analysis

Systematic comparison of C# 9.0 capabilities vs Sharpy's current implementation.
Goal: identify what Sharpy must add to serve as a full C# 9.0 replacement.

> **Methodology:** Audited all C# features (1.0 through 9.0), cross-referenced against
> the Sharpy language specification (`docs/language_specification/`), compiler implementation
> (lexer tokens, AST nodes, codegen), 1,571 integration test fixtures, and open GitHub issues.
> Second pass verified all claims against actual compiler source (lexer, parser AST, codegen
> emitter, semantic analysis) and identified 8 additional gaps not in the initial audit.
> Third pass cross-referenced the complete Microsoft C# version history (every feature from
> 1.0–9.0), verified 15 specific features against compiler source, and incorporated findings
> from 13 skipped test fixtures and GAP markers. Added 6 new Tier 2 gaps (#32–#37) and
> 7 new Tier 3 items.
>
> **C# version target:** Sharpy.Core targets `LangVersion 9.0` / `netstandard2.1`, making
> C# 9.0 the effective ceiling for generated code.
>
> **Last updated:** 2026-03-29

---

## Summary

- **14 Tier 1 gaps** (high priority, blocking for C# replacement)
- **22 Tier 2 gaps** (medium priority, workaroundable)
- **20 Tier 3 items** (intentionally excluded or very niche)
- **50+ C# features** already covered with Pythonic equivalents
- **6 open GitHub issues** tracking specific gaps
- **13 skipped test fixtures** tracking implementation gaps

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

### 32. Expression Trees

- **C# feature:** `Expression<Func<T>>` — represent code as data for LINQ providers (C# 3.0)
- **Sharpy status:** MISSING — lambdas always compile to delegates, never expression trees.
  No way to produce `Expression<Func<T>>` for IQueryable-based APIs.
- **Impact:** Required for Entity Framework Core, LINQ-to-SQL, and any custom IQueryable
  provider. Without this, ORM usage is limited to raw SQL or non-queryable APIs.
- **Scope:** Semantic (detect `Expression<>` target type), CodeGen (emit expression tree
  construction instead of delegate). Large scope — expression tree construction requires
  building `System.Linq.Expressions` API calls for every expression node type.
- **Workaround:** Use raw SQL or method-based LINQ with `Func<T>` delegates (evaluates
  client-side, not in database).

### 33. Caller Info Attributes

- **C# feature:** `[CallerMemberName]`, `[CallerFilePath]`, `[CallerLineNumber]` on
  default parameters — compiler fills in values automatically (C# 5.0)
- **Sharpy status:** MISSING — no syntax for applying these attributes to parameters.
  Arbitrary .NET attributes go on decorators (`@SomeAttribute`), but parameter-level
  attributes have no Sharpy syntax.
- **Impact:** Common in WPF (`INotifyPropertyChanged`), logging frameworks, and diagnostics.
- **Possible syntax:** `def log(msg: str, member: str = @CallerMemberName):` or parameter
  attribute syntax `def log(msg: str, @CallerMemberName member: str = ""):`.
- **Scope:** Parser (parameter-level attributes), CodeGen (emit attribute on parameter)
- **Workaround:** Manually pass values, or use `nameof` (#12) once implemented.

### 34. Await in Catch/Finally

- **C# feature:** `await` expressions allowed inside `catch` and `finally` blocks (C# 6.0)
- **Sharpy status:** MISSING — `await` inside `except` or `finally` is not supported.
- **Impact:** Important for async error handling — logging to async services, async cleanup.
- **Scope:** Semantic (lift restriction), CodeGen (already emits async state machines, but
  catch/finally need special handling)
- **Workaround:** Store exception, exit catch, then await in outer scope.

### 35. Ref Locals and Ref Returns

- **C# feature:** `ref int x = ref arr[0]` (ref local) and `ref int Find(...)` (ref return)
  — extends beyond just ref parameters (C# 7.0)
- **Sharpy status:** MISSING — depends on ref/out/in parameter support (#1). Even when #1
  is implemented, ref locals and ref returns are a separate feature.
- **Impact:** Performance-critical code that avoids copies of large structs.
- **Scope:** Parser (ref in local declarations and return types), Semantic, CodeGen
- **Workaround:** Use ref parameters or accept the copy overhead.

### 36. Tuple Equality

- **C# feature:** `(1, "a") == (1, "a")` — element-wise `==`/`!=` on tuples (C# 7.3)
- **Sharpy status:** MISSING — tuples cannot be compared with `==`/`!=`.
- **Impact:** Common for value comparisons. Small implementation scope.
- **Scope:** Semantic (recognize tuple `==`/`!=`), CodeGen (emit element-wise comparison)
- **Workaround:** Compare individual elements manually.

### 37. Static Local Functions

- **C# feature:** `static` modifier on local functions prevents closure capture (C# 8.0)
- **Sharpy status:** MISSING — nested `def` always allows captures. No `@static` on
  local functions.
- **Impact:** Performance optimization to avoid accidental closure allocations.
- **Possible syntax:** `@static def helper():` inside a function body.
- **Scope:** Parser (allow `@static` on nested def), Semantic (validate no captures),
  CodeGen (emit `static` on local function)
- **Workaround:** Manually ensure no captures (no compile-time enforcement).

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
| Static imports (`using static`) | 6.0 | Sharpy's `from module import func` covers Sharpy modules; for .NET static classes, use full qualification. |
| Nullable reference types (NRT / `#nullable`) | 8.0 | Sharpy has its own null safety model (`Optional[T]` vs `T?`). NRT annotations are a different paradigm. |
| Interpolated verbatim strings (`$@"..."`) | 8.0 | Sharpy has f-strings and raw strings separately. Combined form is rarely needed. |
| Non-trailing named arguments | 7.2 | `DoWork(name: "x", 42)` — niche ordering flexibility. |
| Null-forgiving operator (`x!`) | 8.0 | Only meaningful with NRT system, which Sharpy doesn't use. |
| `[field: ...]` backing field attributes | 7.3 | Niche serialization scenario (`[field: NonSerialized]`). |
| Generalized async return types | 7.0 | `ValueTask<T>` works via .NET interop; custom `[AsyncMethodBuilder]` is niche. |

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

## Known Gaps from Skipped Test Fixtures

These are Sharpy-specific implementation gaps tracked by `.skip` files in the test suite.
Not all are C# feature gaps — some are Sharpy stdlib or semantic issues.

| GAP ID | Test Fixture | Issue |
|--------|-------------|-------|
| GAP-13 | `builtins/list_get` | `list.get()` not implemented in Sharpy.Core |
| GAP-18 | `type_aliases/type_alias_class_scope`, `type_alias_function_scope` | Class/function-scoped type aliases not resolved |
| GAP-19 | `generics/generic_constraint_reorder` | Multiple generic constraints with `+` syntax not parsed |
| GAP-20 | `errors/pipe_to_constructor` | Pipe to constructor passes semantic but fails C# compilation |
| GAP-21 | `pattern_matching/tuple_rest_pattern` | Tuple star-unpack rest pattern not supported |
| GAP-39 | `expressions/chained_identity_operators` | `a is b is c` chaining not supported |
| GAP-46 | `errors/partial_type_argument_error` | Partial type argument error handling |
| GAP-47 | `properties/covariant_property_return` | Covariant property return types |
| #476 | `type_system/narrowing_not_is_none` | `not (x is None)` doesn't narrow types |
| — | `pattern_matching/match_named_tuple` | `collections.namedtuple` import fails |
| — | `generators/yield_nested_function` | Nested function identifiers not resolved in generators |
| — | `errors/type_none_error` | `type(None)` returns `System.Object` instead of compile error |
| — | `arrays/array_negative_index` | CLR arrays don't support negative indexing natively |
| — | `stdlib_argparse` | `type(x).__name__` not supported |
| — | `stdlib_csv_dictwriter` | StringIO doesn't extend `System.IO.TextWriter` |

---

## Recommended Implementation Order

For systematic gap closure, suggested sequence (respects dependencies and
maximizes value per effort). Grouped into phases.

### Phase 1: Quick wins (small scope, high frequency in C# code)

1. **`do...while`** (#6) — smallest scope, no dependencies, completes control flow
2. **`nameof`** (#12) — small scope, high value for argument validation
3. **`private protected`** (#11) — small scope, semantic + codegen only
4. **`default` literal** (#14) — small scope, essential for generic code
5. **Tuple equality** (#36) — small codegen change, element-wise `==`/`!=`

### Phase 2: Control flow completeness

6. **Exception filters (`when`)** (#4) — small parser change, high value
7. **Multi-catch** (#16) — small parser change, reduces boilerplate
8. **Raise expressions** (#15) — allow `raise` in expression position
9. **`lock` statement** (#5) — essential for concurrency
10. **`is` pattern outside `match`** (#17) — very common C# 7+ idiom
11. **Await in catch/finally** (#34) — important for async error handling

### Phase 3: Interop-critical

12. **`ref`/`out`/`in` parameters** (#1, issue #419) — largest interop impact
13. **Ref locals and ref returns** (#35) — extends #1 to locals/returns
14. **`checked`/`unchecked`** (#8) — numeric safety
15. **Object initializers** (#13) — ubiquitous in C# for DTOs, EF, etc.
16. **Caller info attributes** (#33) — common in WPF, logging

### Phase 4: Type system completeness

17. **Nested types** (#3) — unlocks Builder pattern, private helpers
18. **Conversion operators** (#2) — type system completeness
19. **Static constructors** (#7) — one-time initialization
20. **`notnull` constraint** (#18) — generic API correctness
21. **`readonly` fields** (#26) — runtime immutability
22. **Static local functions** (#37) — prevents accidental closure capture

### Phase 5: Large features

23. **Partial classes** (#9) — large scope, unlocks code-gen scenarios
24. **Records + `with` expressions** (#10) — largest scope, C# 9.0 flagship
25. **Expression trees** (#32) — large scope, enables LINQ providers / EF Core

### Phase 6: Polish (lower frequency, workarounds exist)

26. **Block-less disposable scope** (#28) — convenience
27. **Static classes** (#19) — modules cover most cases
28. **`readonly struct`** (#24), **`ref struct`** (#25) — advanced value types
29. **Static lambdas** (#23) — performance optimization
30. **Lambda type annotations** (#30, issue #417) — pending RFC
31. **Remaining items** — multi-dim arrays, LINQ query syntax, volatile, delegate
    combination, etc.
