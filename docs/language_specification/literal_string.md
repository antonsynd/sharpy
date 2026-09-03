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

## Accepted Forms

An argument is a `LiteralString` when it is a string literal, a `+` concatenation whose operands are
themselves accepted forms, or either of those wrapped in redundant parentheses — parentheses never
change meaning (the canonical-form contract, #1170):

```python
def safe_query(query: LiteralString) -> str:
    return f"executing: {query}"

def main():
    print(safe_query(("SELECT * FROM users")))   # executing: SELECT * FROM users
    print(safe_query(("SELECT * ") + ("FROM users")))
```

PEP 675 also treats `"a" * 3`, an f-string with literal-only holes, and implicit concatenation
`"a" "b"` as `LiteralString`; Sharpy deliberately does **not** accept those forms today (the first
two are refused as `str`, the third does not parse). Widening is a separate decision.

## Store Positions

A literal-derived string is accepted at **every** store position where the slot is
`LiteralString` — the same scope as integer constant conversion:

```python
class Config:
    key: LiteralString = "default"        # field declaration

def query(sql: LiteralString) -> str:
    return sql

def run(sql: LiteralString = "SELECT 1") -> str:  # parameter default
    return sql

def main() -> None:
    x: LiteralString = "hello"            # declaration
    x = "world"                           # plain store
    print(query("SELECT 1"))              # positional argument
    print(query(sql="SELECT 1"))          # keyword argument
    print(run())                          # default
    xs: list[LiteralString] = ["a", "b"]  # collection-literal elements
```

The expression's type stays `str`; `LiteralString` is the **slot's** declared type.
A `str` variable is always refused — the literal-derived check is a compile-time
fact, not a type.

Refused forms: f-strings (`f"..."` — interpolation is runtime), `"a" * 3`, and any
non-literal `str` expression. See #1741 for the full forms table.

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
