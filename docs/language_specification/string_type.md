# String Type

Sharpy has two string types, `str` and `string`. Sharpy's `str` struct type is an implementation of Python's `str` type, with emphasis on being as faithful as possible to its API in all regards. Sharpy's `string` type, on the other hand, maps directly to .NET's `System.String`, which uses UTF-16 encoding internally. This has important implications for string operations.

## When to use

The `str` type should be used when Python string behavior is desired or for APIs that are meant for Sharpy-only consumers where Pythonic behavior is desired. The `string` type should be used for APIs that are meant for interop with C# and .NET. In general, the `string` type is more efficient and should be preferred. However, `str` and `string` are implicitly convertible to each other to aid Python developers to the Sharpy and .NET landscape.

By default, Sharpy string literals are of type `string`. In a future version of the Sharpy language, this can be configured per compilation unit (roughly per file).

Many of Sharpy's inherited interfaces and methods from C#/.NET, e.g. `ToString()` by definition return `string`. Sharpy developers who rely on using `str` instead of `string` should be aware of the implicit conversion if passing/receiving `string` in `str` variables, or vice versa, and the performance implications thereof.

*Implementation*
- *✅ Native - For `str`, direct use of `Sharpy.Core.Str`.*
- *✅ Native - For `string`, direct use of `System.String` with no additional abstraction.*
