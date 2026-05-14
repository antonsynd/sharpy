# Introduction

Sharpy is a modern, statically-typed Pythonic language targeting .NET. While Python code will not run in Sharpy without modifications, the additions and changes in Sharpy over Python should be intuitive to and welcomed by all Python developers.

## Goals

* Provide a statically-typed and modern Pythonic language for the .NET CLI
* Seamless bidirectional interop with other .NET libraries
* Multi-target: C# 9.0 minimum (netstandard2.1 for Unity/.NET 5+ compatibility), C# 14 on net10.0

## Core Axioms

Sharpy's design flows from three axioms and one resolution rule. All language decisions should be predictable by applying these:

**Axiom 1 — .NET Runtime:** Sharpy compiles to C# and executes on the .NET CLR. Generated code targets C# 9.0 minimum (for netstandard2.1 compatibility) and C# 14 on net10.0. Memory model, type system, inheritance, and runtime semantics follow .NET.

**Axiom 2 — Python Surface:** Sharpy uses Python 3 syntax and idioms. Indentation-based blocks, keywords (`True`/`False`/`None`), comprehensions, decorators, and dunders come from Python.

**Axiom 3 — Static & Null-Safe:** Every type is statically known at compile time. `None` requires explicit opt-in via `T?`. All values must be initialized at declaration.

**Resolution Rule:** When Axiom 1 and Axiom 2 conflict, Axiom 1 wins, unless the compiler can reconcile them via zero-cost abstractions (naming transformations, thin wrappers, or polymorphic dispatch).

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
| Why `snake_case` in source but `PascalCase` in .NET? | Resolution → Zero-cost naming transformation |
| Why do builtins like `len()` work on both Sharpy and .NET types? | Resolution → Zero-cost polymorphic dispatch |

## One-Liner Summary

> **Sharpy = .NET runtime + Python syntax + static null-safe types, where .NET wins conflicts unless the compiler bridges them for free.**
