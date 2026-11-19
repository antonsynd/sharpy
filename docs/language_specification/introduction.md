# Introduction

Sharpy is a modern, statically-typed Pythonic language targeting .NET. While Python code will not run in Sharpy without modifications, the additions and changes in Sharpy over Python should be intuitive to and welcomed by all Python developers.

## Goals

* Provide a statically-typed and modern Pythonic language for the .NET CLI
* Seamless bidirectional interop with other .NET libraries
* Multi-target: C# 9.0 minimum (netstandard2.1 for Unity/.NET 5+ compatibility), C# 14 on net10.0

## Core Axioms

Sharpy's design flows from three axioms, one resolution rule, and an ordered list of tie-breaks. All language decisions should be predictable by applying these:

**Axiom 1 — .NET Runtime:** Sharpy compiles to C# and executes on the .NET CLR. Generated code targets C# 9.0 minimum (for netstandard2.1 compatibility) and C# 14 on net10.0. Memory model, type system, inheritance, and runtime semantics follow .NET. That includes run-time behaviour the CLR or C# already defines: when a type initializes, the order its initializers run in, how a generic type is instantiated, numeric width. Sharpy inherits it. The one standing exception is Axiom 3's no-default clause, which outranks this default: N3 ([variable scoping](variable_scoping.md#governing-principle)) rules when module-level and class-level values initialize. Any other departure is a decision, never a side effect: it names the defect it removes and the cost it accepts.

**Axiom 2 — Python Surface:** Sharpy uses Python 3 syntax and idioms. Indentation-based blocks, keywords (`True`/`False`/`None`), comprehensions, decorators, and dunders come from Python. Python is evidence for what a construct *does* only where python3 runs the same program and the construct means the same thing there (its *twin*). Where there is none (the `ref` / `out` / `in` parameter modifiers, `struct`, overloads, a generic class's per-instantiation statics), Python has no say and Axioms 1 and 3 decide. A Python error that exists only because of declaration order is not evidence either (N1, N3).

**Axiom 3 — Static & Null-Safe:** Every type is statically known at compile time. `None` requires explicit opt-in via `T?`. All values must be initialized at declaration. A CLR default (`0`, `null`, `false`) never stands in for an initializer that has not run.

**Resolution Rule:** When Axiom 1 and Axiom 2 conflict, Axiom 1 wins, unless the compiler can reconcile them via zero-cost abstractions (naming transformations, thin wrappers, or polymorphic dispatch).

**Tie-breaks:** When no rule keeps everything, prefer, in this order:

1. **No silently wrong value.** Never a CLR default standing in for an initializer that has not run, an initializer that is skipped, or a value that depends on which initializer happened to run first.
2. **A compile-time error over a run-time error.** Either is better than a silently wrong value.
3. **A correct program keeps its value.** Where a program's result is right today, changing it is a recorded decision, not a consequence of another change.
4. **One rule for every host** (module, class, generic class) over a special case.

## Applying the Axioms

Given any language design question, apply the axioms in order:

| Question | Derivation |
|----------|------------|
| What is `int`? | Axiom 1 → `System.Int32` |
| Can I inherit from multiple classes? | Axiom 1 → No (single class, multiple interfaces) |
| What does `len("😀")` return? | Axiom 1 → `2` (UTF-16 code units) |
| How do closures capture variables? | Axiom 1 → By reference |
| Why indentation instead of braces? | Axiom 2 → Python syntax |
| Why does `/` always return a float? | Axiom 2 → Python 3 semantics |
| Can I use `**kwargs`? | Axiom 3 → No (not statically typeable) |
| Can a variable be uninitialized? | Axiom 3 → No |
| In what order do a class's initializers run? | Axiom 1 → Declaration order (C#'s rule, and Python's for a class body) |
| What does passing an argument `out` mean, when Python has no such thing? | Axiom 2 is silent (no twin) → Axiom 1 (C#'s rule) |
| What does a read give when the value's initializer has not run? | Axiom 3 → Never a default: the initializer runs first, or it is an error ([N3](variable_scoping.md#values-are-ready-before-use-n3)) |
| Why `snake_case` in source but `PascalCase` in .NET? | Resolution → Zero-cost naming transformation |
| Why do builtins like `len()` work on both Sharpy and .NET types? | Resolution → Zero-cost polymorphic dispatch |

## One-Liner Summary

> **Sharpy = .NET runtime + Python syntax + static null-safe types, where .NET wins conflicts unless the compiler bridges them for free.**
