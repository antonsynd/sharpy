# Name Shadowing

How Sharpy treats a user declaration or import that reuses the name of a builtin.

Sharpy inherits Python's syntax but resolves names under **Axiom 1 (.NET first)**, so where Python
and C# disagree, C# decides. That single rule explains every case below.

## The governing question: is there a way back?

Shadowing is only safe when the shadowed thing remains reachable. Every language that permits it
provides a qualified path to the original — `java.lang.String`, `kotlin.String`, `Swift.Int`,
`core::primitive::u8`, `global::System.Int32`. Python provides `builtins.int`.

Sharpy provides **`builtins.<name>`**, in both value and type position:

```python
import builtins

def len(x: int) -> int:      # shadows the builtin in this module
    return x * 100

def main() -> None:
    xs: list[int] = [1, 2, 3]
    print(builtins.len(xs))  # 3   — the builtin
    print(len(7))            # 700 — yours

    a: builtins.int = 5      # qualified in type position too
```

Where no such path exists, shadowing is refused rather than permitted.

## 1. Keywords — never shadowable, backticks escape

Keywords (`if`, `class`, `def`, `return`, `None`, …) cannot be used as identifiers. To name a
symbol after one — usually for .NET interop — backtick-escape it:

```python
def `class`(self) -> str: ...
```

This is the same mechanism as C#'s `@` verbatim identifier (`@class`), and it is Sharpy's escape
throughout this document.

## 2. Builtin *type* names — refused in a type declaration (SPY0212)

A `class`/`struct`/`interface`/`enum`/`union`/`delegate` whose bare name is a builtin type is
**refused**:

```python
class int:      # SPY0212
    value: int
```

The reason is C#'s: `int`, `str`, `bool`, `double` are the spellings of predefined types, and in
C# those are keywords — `class int` is not expressible there either. Backticks are intended as the
escape, so the spelling remains available when it is genuinely wanted:

```python
class `int`:    # intended: this is your type, and `int` refers to it
    value: builtins.int
```

> **Known gap (#1325).** The declaration above is accepted, but the type cannot currently be
> *constructed* — `` `int`("hi") `` resolves to the builtin conversion rather than to the user's
> constructor, producing the self-contradictory `SPY0220: Cannot assign type 'int' to variable of
> type 'int'`. Until that is fixed, a builtin type name is effectively not usable under any
> spelling. This is tracked as a defect, not as intended behavior: a rule may not demand an escape
> that does not work.

Builtin *type* names include the primitives (`int`, `long`, `float`, `double`, `float32`, `bool`,
`str`, `decimal`, `char`, `void`, `object`, and the .NET spellings `int32`, `uint`, `sbyte`, …) and
the registry types (`list`, `dict`, `set`, `frozenset`, `tuple`, `bytes`, `Exception`, `Optional`,
`Result`).

> Note that the last three are PascalCase. Reserved-ness is decided by the registry, not by
> capitalization — though as a rule of thumb a lowercase type name in Sharpy is a builtin, because
> user types are PascalCase by convention (SPY0453).

## 3. Builtin names in *value* position — permitted, warned (SPY0483)

A variable, parameter, for-target, `with ... as`, `except ... as`, or **function declaration** may
spell a builtin name. This is legal and honored — the binding shadows the builtin exactly as any
inner binding shadows an outer one:

```python
def double(x: int) -> int:   # SPY0483 warning — compiles and does what it says
    return x * 2
```

It is warned rather than refused because something real happens: for the rest of that scope the
builtin is not reachable by its bare spelling. It is not refused because a value-position name
creates no annotation ambiguity, and because `builtins.double` still reaches the builtin.

## 4. Star-imports — ambiguous at the point of use (SPY0492)

A `from M import *` that binds a name displacing a builtin does **not** fail at the import. The
name becomes **ambiguous where it is used**:

```python
from numpy import *          # fine on its own

def main() -> None:
    xs: list[int] = [1, 2, 3]
    print(len(xs))           # fine — no collision on this name
    print(sum(xs))           # SPY0492 — ambiguous: numpy.sum or the builtin?
```

This is C# `CS0104`: two `using` directives supplying the same name are legal, and only an
unqualified reference that resolves two ways is an error. Reporting at the import instead would
refuse a file that never touches the colliding name — louder and less precise.

Three ways to say which you meant:

```python
import numpy                 # qualified
print(numpy.sum(xs))

from numpy import sum        # explicit — naming it IS the statement of intent
print(sum(xs))

import builtins              # keep the star-import, reach the builtin
print(builtins.sum(xs))
```

An explicit `from M import sum` is never ambiguous, mirroring how a C# `using Foo = A.B;` alias
resolves what two bare `using` directives could not.

### Why not simply refuse the rebinding?

Because the hostile case and the intended case are the same mechanism. `from numpy import *`
deliberately replacing `sum`/`min`/`max` with array-aware versions is idiomatic, and Sharpy's own
`math` exports `pow`. Nothing structural distinguishes that from a dependency quietly redefining
`len`. Refusing would break the legitimate use to punish the other; the ambiguity error refuses
only the case where the reader genuinely cannot tell which one runs.

## 5. Shadowing does not cross module boundaries

A shadow declared in a module affects that module only. A plain `import` never carries it to a
consumer:

```python
# lib.spy
def len(x: int) -> int:
    return 999

# main.spy
import lib
print(len([1, 2, 3]))   # 3 — the builtin. lib's shadow stays in lib.
```

Only `from lib import *` and `from lib import len` bring the name across, and those are governed by
§4.

## Summary

| Form | Result | Escape |
|---|---|---|
| keyword as identifier | refused (lexer) | backticks: `` `class` `` |
| `class int:` | **SPY0212 error** | backticks: `` class `int`: `` — declared but not constructible, #1325 |
| `def double(...)`, `int = 5` | **SPY0483 warning**, honored | backticks, or `builtins.double` |
| `from M import *` + unqualified use of a colliding name | **SPY0492 error at the use** | `M.name`, `builtins.name`, or `from M import name` |
| `from M import name` | permitted, no diagnostic | — |
| `import M` | never shadows anything | — |

## Related

- [Builtin Functions](builtin_functions.md)
- [Primitive Types](primitive_types.md)
- Diagnostics: SPY0212, SPY0453, SPY0483, SPY0492
