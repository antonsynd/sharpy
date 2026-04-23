# LiteralString Type

`LiteralString` is a compile-time type that restricts function parameters to accept only string literals known at compile time. Inspired by Python PEP 675, it helps prevent injection vulnerabilities by ensuring that security-sensitive strings are not constructed from user input.

## Usage

Annotate a parameter with `LiteralString` to require a string literal at the call site:

```python
def safe_query(query: LiteralString) -> str:
    return f"executing: {query}"

# OK: string literal
result = safe_query("SELECT * FROM users")

# ERROR: runtime string variable
user_input: str = "DROP TABLE users"
result = safe_query(user_input)  # Cannot pass 'str' to 'LiteralString'
```

## String Literal Concatenation

Concatenation of string literals produces a `LiteralString`:

```python
def safe_query(query: LiteralString) -> str:
    return f"executing: {query}"

# OK: concatenation of literals is still a LiteralString
result = safe_query("SELECT * " + "FROM users")
result2 = safe_query("A" + "B" + "C")
```

## Type Relationship

`LiteralString` is a subtype of `str`:

- A `LiteralString` value can be used anywhere a `str` is expected
- A `str` value **cannot** be used where a `LiteralString` is expected

```
LiteralString <: str
```

This ensures that functions accepting `str` work with literal strings, but functions requiring `LiteralString` reject runtime-constructed strings.

## Use Cases

`LiteralString` is primarily useful for:

- **SQL queries** — prevent SQL injection
- **Shell commands** — prevent command injection
- **Regular expressions** — ensure patterns are compile-time constants
- **Configuration keys** — ensure keys match known constants

```python
def execute_sql(query: LiteralString) -> None:
    ...

def run_command(cmd: LiteralString) -> None:
    ...

def compile_regex(pattern: LiteralString) -> None:
    ...
```

## Generated C#

`LiteralString` has no runtime representation — it emits as `string` in C#. The compile-time check is performed entirely during type checking:

```python
def safe_query(query: LiteralString) -> str:
    return query
```

generates:

```csharp
public static string SafeQuery(string query)
{
    return query;
}
```

## Diagnostics

When a non-literal `str` is passed to a `LiteralString` parameter, the compiler emits a type error:

```
Cannot pass argument of type 'str' to parameter of type 'LiteralString'
```

*Implementation*
- *✅ Implemented — `LiteralStringType` singleton in `SemanticType.cs`, resolved in `TypeResolver.cs`*
- *Subtyping: `LiteralStringType.IsAssignableTo(str)` returns true*
- *Concatenation: literal + literal preserves `LiteralString` type*
- *Emits as `string` in C# (no runtime distinction)*
