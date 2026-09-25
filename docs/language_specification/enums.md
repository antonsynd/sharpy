# Enumerations

## Simple Enums

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"



def main() -> None:
    favorite = Color.RED
    if favorite == Color.RED:
        print("Red is your favorite")

    # Access underlying value
    value = favorite.value  # 1
    name = favorite.name    # "RED"
    print(value, name)      # 1 RED
```

**Rules:**
- All cases must have explicit constant values
- All values must be of the same type, either an integer type or the `str` type.
- Enums must have at least one case

**Enum Iteration and Methods:**

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3



def main() -> None:
    # Iterate over all enum values
    for color in Color:
        print(f"{color.name} = {color.value}")
    # Output:
    # RED = 1
    # GREEN = 2
    # BLUE = 3

    # Get all values as a list
    all_colors: list[Color] = list(Color)

    # Get all names
    names: list[str] = [c.name for c in Color]  # ["RED", "GREEN", "BLUE"]

    # Get all values
    values: list[int] = [c.value for c in Color]  # [1, 2, 3]
    print(len(all_colors), names, values)         # 3 ['RED', 'GREEN', 'BLUE'] [1, 2, 3]
```

**`str` and `repr` of an integer enum** are Python's: `str(Color.RED)` is `Color.RED`, `repr` is
`<Color.RED: 1>`, and `.name` is the member name exactly as declared — the same spelling on every
route (`print`, f-strings and `format()` pad the `str`, a list or dict element is the `repr`). The
rule covers every .NET enum reached through interop too: `str(DayOfWeek.Monday)` is
`DayOfWeek.Monday` (Sharpy-only; Python has no such type).

```python
enum Color:
    RED = 1
    dark_blue = 2


def main() -> None:
    print(Color.RED)                 # Color.RED
    print(repr(Color.dark_blue))     # <Color.dark_blue: 2>
    print(Color.dark_blue.name)      # dark_blue
    print(f"[{Color.RED:>12}]")      # [   Color.RED]
    print([Color.RED])               # [<Color.RED: 1>]
```

A string-backed enum keeps `StrEnum`'s `str` (the value, below).

**Note:** Simple enums (non-tagged unions) cannot have custom methods. For enums with methods, use tagged unions, see [tagged_unions.md](tagged_unions.md).

## String-Backed Enums

An enum whose members carry string values is a **string-backed enum**, and it behaves like
CPython's `StrEnum`: a member is its own type *and* is usable wherever a `str` is.

```python
enum Color:
    RED = "red"
    GREEN = "green"


def describe(c: str) -> str:
    return "<" + c + ">"


def main() -> None:
    c: Color = Color.RED       # the declared enum type is a real annotation
    print(c.name)              # RED  — the member name
    print(c.value)             # red  — the backing string
    print(str(c))              # red  — str() gives the value
    print(repr(c))             # <Color.RED: 'red'>  — repr() is StrEnum's
    print(c == Color.RED)      # True — member identity
    print(c == "red")          # True — and its backing string
    print(describe(Color.GREEN))   # <green> — passes as a str
    s: str = Color.GREEN       # assigns to a str
    for x in Color:            # iterates its members
        print(x.name)
```

Because a string-backed enum lowers to a class, no name the class declares may equal the enum's own
(C#'s CS0542). A member that compiles to the enum's name, and a string enum named like one of the
members its class synthesizes (`Name`, `Value`, `Values`, `ToString` — so `enum value` too), are
refused with `SPY0525`: rename the member or the enum. When it is the member's *casing* that
collides (`color` compiles to `Color`), backtick-escaping the member's declaration also works: an
escaped member compiles verbatim, and every reference to it — `Color.color`, ``Color.`color` ``,
`case Color.color:` — follows the declaration, escaped or not. An integer-backed enum is a C# `enum`,
whose members may share its name, so `enum Color: Color = 1` compiles.

<!-- spec-sweep: error SPY0525 -->
```python
enum Color:
    Color = "c"    # SPY0525: emitted as 'Color', the same name as its enclosing type
    RED = "r"


def main() -> None:
    print(Color.RED)
```

```python
enum Color:
    `color` = "c"    # escaped: compiles to 'color', no clash with 'Color'
    RED = "r"


def main() -> None:
    print(Color.color.value)      # c — a plain use follows the escaped declaration
    print(Color.`color`.value)    # c
```

*Implementation*
- *Integer enums: ✅ Native - C# `enum`*
- *String enums: 🔄 Lowered - sealed class of singleton instances carrying `Name`/`Value`, with
  `ToString()` returning the value, an explicit `Sharpy.IRepr.Repr()` giving StrEnum's
  `<Color.RED: 'red'>`, an implicit conversion to `string`, and a static `Values` list*
- *`.name` property: 🔄 Lowered - `Sharpy.Builtins.EnumName` (integer enums: the python name
  the field records in `[SharpyFieldName]` when its C# name differs, else the field name — the same
  channel `str`/`repr` read); the instance's `Name` (string enums)*
