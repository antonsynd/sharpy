# Version Guide

Features are marked with their target version. Each version builds upon the previous, with core features in earlier versions and ergonomic/advanced features in later versions.

| Version | Focus Area | Key Features |
|---------|------------|--------------|
| **v0.1.0** | Core Language | Basic types, functions, classes, control flow, exceptions, imports |
| **v0.1.1** | Nullability & Collections | Nullable types, `?.`, `??`, list/dict/set/tuple, slicing |
| **v0.1.2** | Structs, Interfaces, OOP | Structs, interfaces, inheritance, decorators, function overloading, properties |
| **v0.1.3** | Generics & Lambdas | Generic classes/structs/interfaces/methods, constraints, lambdas |
| **v0.1.4** | Enums & Operators | Simple enums, operator overloading via dunders |
| **v0.1.5** | Extended Syntax | F-strings, extended literals, comparison chaining, loop else |
| **v0.1.6** | Pattern Matching | Match statements, patterns, guards |
| **v0.1.7** | Type Aliases & Shadowing | Type aliases, variable shadowing |
| **v0.1.8** | Comprehensions | List/dict/set comprehensions, walrus operator |
| **v0.2.0+** | Resources & Async | Context managers (`with`), async/await, generators (`yield`), tagged unions (ADTs), `maybe`/`try` expressions, events |
| **v1.0** | Stable release | Battle-tested and stable API and implementations |
| **v2.0+** | Future | Features requiring C# 11+ or .NET 7+ |

## Target Compatibility

| Sharpy Version | Target Runtime | C# Version | Notes |
|----------------|----------------|------------|-------|
| **v0.1.0 - v1.0** | .NET 5+ / Unity | C# 9.0 | Maximum compatibility |
| **v2.0+** | .NET 7+ | C# 11+ | Full modern features |
