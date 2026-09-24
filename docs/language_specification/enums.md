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

# Usage
favorite = Color.RED
if favorite == Color.RED:
    print("Red is your favorite")

# Access underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"
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
```

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
refused with `SPY0525`: rename the member or the enum (an enum member's declaration cannot be
backtick-escaped). An integer-backed enum is a C# `enum`, whose members may share its name, so
`enum Color: Color = 1` compiles.

<!-- spec-sweep: error SPY0525 -->
```python
enum Color:
    Color = "c"    # SPY0525: emitted as 'Color', the same name as its enclosing type
    RED = "r"


def main() -> None:
    print(Color.RED)
```

*Implementation*
- *Integer enums: ✅ Native - C# `enum`*
- *String enums: 🔄 Lowered - sealed class of singleton instances carrying `Name`/`Value`, with
  `ToString()` returning the value, an implicit conversion to `string`, and a static `Values` list*
- *`.name` property: 🔄 Lowered - `Enum.GetName()` or lookup (integer enums); the instance's
  `Name` (string enums)*
