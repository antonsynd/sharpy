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

A string expression is **literal-derived** — admissible into a `LiteralString` slot — when one
bottom-up rule holds over PEP 675's form set. This is the owner's #1741 ruling of 2026-09-03
([comment 5528943408](https://github.com/antonsynd/sharpy/issues/1741#issuecomment-5528943408):
"#1741 adds f-strings whose holes are literal-derived … `.format`/`.join`/… on literal-derived
receivers"), which supersedes the earlier "deliberately not accepted … widening is a separate
decision" wording. The expression's type stays `str`; literal-derivedness is a compile-time fact
on the node, not a distinct type.

| Form | Example | Status |
|------|---------|--------|
| String literal | `"SELECT 1"` | ✅ derived |
| `+` concatenation of derived operands | `"SELECT * " + "FROM users"` | ✅ derived |
| Redundant parentheses (canonical-form contract, #1170) | `("a") + ("b")` | ✅ derived |
| Repetition, `derived * int` or `int * derived` | `"ab" * 3` | ✅ derived |
| f-string, every hole derived (holeless is derived vacuously; `!r`/`!s`/`!a` and format specs do not matter) | `f"{q} !"` | ✅ derived |
| PEP 675 str-returning method on a derived receiver, every `str`-typed argument derived | `q.upper()`, `q.replace("l", "r")`, `q.format("z")`, `", ".join(["a", "b"])` | ✅ derived |
| `%` formatting | `"x=%s" % q` | N/A — `%` is not a Sharpy string operator (SPY0222) |
| Implicit concatenation | `"a" "b"` | N/A — does not parse (SPY0103); see [String Literals › No Implicit Concatenation](string_literals.md) |
| Any of the forms above over a `str`-typed input | `s.upper()` where `s: str` | ❌ refused (SPY0220) |
| `.join` over a `list[str]` **variable** (not a literal display) | `", ".join(items)` | ❌ refused (SPY0220) |
| Method returning `list`/`tuple` (`split`, `rsplit`, `splitlines`, `partition`, `rpartition`) | `q.split(",")` | N/A — result is `list[str]`/tuple, never a string |

The PEP 675 str-returning method set (each preserves literal-derivedness): `capitalize`,
`casefold`, `center`, `expandtabs`, `format`, `format_map`, `join`, `ljust`, `lower`, `lstrip`,
`replace`, `rjust`, `rstrip`, `strip`, `swapcase`, `title`, `upper`, `zfill`.

```python
def safe_query(query: LiteralString) -> str:
    return query

def main() -> None:
    q: LiteralString = "SELECT"
    print(safe_query(("SELECT * FROM users")))       # literal, redundant parens
    print(safe_query(("SELECT * ") + ("FROM users")))  # + concatenation
    print(safe_query("ab" * 3))                      # ababab
    print(safe_query(f"{q} 1"))                      # SELECT 1  (f-string, derived hole)
    print(safe_query(f"literal"))                    # literal   (holeless f-string)
    print(safe_query(q.upper()))                     # SELECT
    print(safe_query(q.replace("E", "3")))           # S3L3CT  (replace hits every match)
    print(safe_query(", ".join(["a", "b"])))         # a, b      (literal list display)
    print(safe_query(f"{q.lower()}" + " 1"))         # select 1  (nested composition)
```

The N/A and refused forms carry their diagnostic codes. `%` is not a string operator:

<!-- spec-sweep: error SPY0222 -->
```python
def safe_query(query: LiteralString) -> str:
    return query

def main() -> None:
    q: LiteralString = "x"
    print(safe_query("fmt=%s" % q))   # SPY0222 — `%` is not a Sharpy string operator
```

Implicit concatenation (adjacent string literals) is not Sharpy syntax and does not parse
(see [String Literals › No Implicit Concatenation](string_literals.md)):

<!-- spec-sweep: error SPY0103 -->
```python
def main() -> None:
    x: LiteralString = "a" "b"   # SPY0103 — adjacent literals do not concatenate
    print(x)
```

A `str`-typed input is refused even under an otherwise-accepted form — the fact is bottom-up:

<!-- spec-sweep: error SPY0220 -->
```python
def safe_query(query: LiteralString) -> str:
    return query

def main() -> None:
    s: str = "runtime"
    print(safe_query(s.upper()))   # SPY0220 — s is str, so s.upper() is not derived
```

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

def make() -> LiteralString:
    return "SELECT 1"                     # return

def gen() -> LiteralString:
    yield "a"                             # yield

def main() -> None:
    x: LiteralString = "hello"            # declaration
    x = "world"                           # plain store
    print(x)                              # world
    c: Config = Config()
    c.key = "k"                           # attribute store
    print(c.key)                          # k
    xs: list[LiteralString] = ["a", "b"]  # collection-literal elements
    xs[0] = "z"                           # index store
    print(xs[0])                          # z
    d: dict[str, LiteralString] = {}
    d["k"] = "v"                          # dict-value store
    print(d["k"])                         # v
    print(query("SELECT 1"))              # positional argument -> SELECT 1
    print(query(sql="SELECT 1"))          # keyword argument    -> SELECT 1
    print(run())                          # default             -> SELECT 1
    print(make())                         # return              -> SELECT 1
    for s in gen():
        print(s)                          # yield               -> a
    t: tuple[LiteralString, int] = ("a", 1)    # tuple element
    print(t[0])                           # a
    print(query((x := "walrus")))         # walrus              -> walrus
    f: () -> LiteralString = lambda: "b"  # lambda body under a typed target
    print(f())                            # b
    r: LiteralString = "a" if True else "c"    # conditional of literals
    print(r)                              # a
    s2: LiteralString = "a"
    s2 += "b"                             # augmented
    print(s2)                             # ab
```

The expression's type stays `str`; `LiteralString` is the **slot's** declared type.
A `str` variable is always refused — the literal-derived check is a compile-time
fact, not a type.

Every store position takes any **literal-derived** form (§Accepted Forms): f-strings with derived
holes, `"a" * 3`, and PEP 675 str-method calls on derived receivers are all admissible here, per
the #1741 ruling. What stays refused is a `str`-typed expression in any position — literal-derivedness
is a bottom-up compile-time fact, so a `str` variable is refused (SPY0220) even inside an
otherwise-accepted form (`s.upper()`, `", ".join(items)` for a `list[str]` variable `items`). The
non-Sharpy forms `%` formatting (SPY0222) and implicit concatenation `"a" "b"` (does not parse) are
listed as N/A in the §Accepted Forms table.

## Type Relationship

`LiteralString` is a subtype of `str`:

- A `LiteralString` value can be used anywhere a `str` is expected
- A `str` value **cannot** be used where a `LiteralString` is expected

```
LiteralString <: str
```

This ensures that functions accepting `str` work with literal strings, but functions requiring `LiteralString` reject runtime-constructed strings. `LiteralString` can appear as the payload of `T?` and `T | None`:

```python
def main() -> None:
    x: LiteralString? = Some("a")
    if x is not None:
        print(x.upper())  # A
```

### Use Surface

A `LiteralString` value supports every operation a `str` does: operators (`==`, `!=`, `<`,
`+`, `*`), comparison chains, `len`, indexing, slicing, iteration, `in`, truthiness (`if x:`),
method calls (including `split`, which returns `list[str]`), f-strings, `str()`, and `sorted`.

```python
def main() -> None:
    x: LiteralString = "hello"
    print(x.upper())           # HELLO
    print(x.startswith("he"))  # True
    print(x.replace("l", "r", 1))  # herlo
    print(f"{x}!")             # hello!
    print(str(x))              # hello
```

`x + "b"` is still literal-derived (admissible into a `LiteralString` slot), while `x + s`
(where `s: str`) is not — `x += s` is SPY0220.

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
