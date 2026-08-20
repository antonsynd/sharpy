# SharpyLang.Core

Runtime library for the [Sharpy](https://github.com/antonsynd/sharpy) programming language. Provides Pythonic collection types and built-in functions for .NET.

## What's Included

- **Collections:** `List<T>`, `Dict<K,V>`, `Set<T>`, `FrozenSet<T>`, `Tuple` — with Python semantics (negative indexing, slicing, comprehension-style factories)
- **Error handling:** `Optional<T>`, `Result<T,E>` — functional types for safe error handling
- **Built-in functions:** `print`, `len`, `range`, `enumerate`, `zip`, `sorted`, `reversed`, and more
- **String operations:** Python-compatible string methods and f-string support

## Usage

This package is a runtime dependency of compiled Sharpy programs. You typically don't reference it directly — the Sharpy compiler (`sharpyc`) handles this automatically.

For .NET projects that interop with Sharpy code:

```bash
dotnet add package SharpyLang.Core
```

## Links

- [Documentation](https://antonsynd.github.io/sharpy/)
- [GitHub](https://github.com/antonsynd/sharpy)
