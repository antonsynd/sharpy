# SharpyLang.Stdlib

Standard library for the [Sharpy](https://github.com/antonsynd/sharpy) programming language. Provides 60+ modules with Pythonic APIs on .NET.

## Modules

Includes implementations of: `json`, `os`, `os.path`, `re`, `math`, `random`, `collections`, `itertools`, `functools`, `datetime`, `pathlib`, `csv`, `hashlib`, `base64`, `uuid`, `socket`, `sqlite3`, `struct`, `numpy`, and many more.

## Usage

This package is a runtime dependency of compiled Sharpy programs that use `import`. You typically don't reference it directly — the Sharpy compiler (`sharpyc`) handles this automatically.

For .NET projects that interop with Sharpy code:

```bash
dotnet add package SharpyLang.Stdlib
```

## Links

- [Documentation](https://antonsynd.github.io/sharpy/)
- [GitHub](https://github.com/antonsynd/sharpy)
