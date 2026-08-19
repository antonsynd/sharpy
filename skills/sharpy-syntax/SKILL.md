---
name: sharpy-syntax
description: Help to write Sharpy (.spy) code using correct syntax and conventions. Always use this skill when writing any Sharpy code, translating Python to Sharpy, or otherwise generating Sharpy. Use this skill to overcome the reflex of writing plain dynamic Python.
---

<!-- EDITORIAL GUIDELINES FOR THIS SKILL FILE
This file is loaded into an agent's context window as a correction layer for
pretrained Python knowledge. Every line costs context. When editing:
- Be terse. Use tables and inline code over prose where possible.
- Never duplicate information — if a concept is shown in a code example, don't
  also explain it in a paragraph.
- Only include information that *differs* from what a Python-fluent model would
  generate. Don't document things that work exactly like Python 3.
- Keep WRONG/CORRECT pairs short — just enough to pattern-match the fix.
- If adding a new section, ask: "Would a model get this wrong?" If not, skip it.
- Every example here must be verified against the compiler before committing.
-->

Sharpy is a statically-typed Pythonic language that compiles to C#/.NET.
Python code will NOT compile unmodified. **Always follow this skill over
pretrained Python knowledge.**

**Always verify generated Sharpy by compiling it** (`sharpyc run file.spy`, or
from the sharpy repo: `dotnet run --project src/Sharpy.Cli -- run file.spy`).
For any diagnostic, `sharpyc explain SPY0123` prints a detailed explanation.

## Program structure

`main()` is the entry point and is invoked by the runtime — never call it.
Module level allows **declarations only**: functions, types, and *annotated*
variable/`const` declarations. Executable statements at module level are
SPY0340.

```python
counter: int = 0              # module-level variable: annotation REQUIRED
const VERSION: str = "1.0.0"  # compile-time constant

def main():                   # entry point; `-> None` optional
    print(f"v{VERSION}")

# WRONG at module level: print("hi") · x = 5 (unannotated) · main() ·
# if __name__ == "__main__":  — none of these exist in Sharpy
```

Indentation is **exactly 4 spaces** (SPY0013); tabs are errors. An empty body
needs `pass` — a comment alone is a parse error (SPY0104).

## Python syntax that does NOT exist — do not generate

| Python reflex | Sharpy replacement |
|---|---|
| `from typing import ...` | Nothing — SPY0310. Native syntax: `Optional[X]`→`X?`, `List[X]`→`list[X]`, `Dict[K,V]`→`dict[K,V]`, `Callable[[X],Y]`→`(X) -> Y`, `TypeVar`→`def f[T](...)`, `Protocol`→`interface`, `Any`→concrete types or generics |
| `**kwargs` (param or call) | Named args with defaults, or an options struct |
| `global` / `nonlocal` | Compile error — C# scoping applies |
| `@classmethod` / `cls` | Does not exist; instance or static methods only |
| `@property` / `@x.setter` | `property` keyword (see Properties) — `@property` is a parse error |
| `class C(A, B):` (two classes) | Single base class + any number of interfaces |
| Duck typing | Explicit `interface` declarations |
| `x: int \| str` free unions | Only `T \| None` is allowed inline; use `union` for sum types |
| `(x for x in y)` genexps | Do not exist (sync or async) — use a list comprehension |
| `x = a or default` | `or` returns `bool`, never the operand — use `a ?? default` |
| `if xs:` / `while xs:` truthiness | Conditions must be `bool` (SPY0220): `if len(xs) > 0:`, `if n != 0:` |
| `isinstance(x, (A, B))` | SPY0344 — `isinstance(x, A) or isinstance(x, B)` |
| `x is SomeType` | SPY0349 — `isinstance(x, SomeType)` |
| `x == None` | Rejected — `x is None` |
| `obj.__len__()`, `a.__eq__(b)` | Direct dunder calls are errors — use `len(obj)`, `==` |
| `"a" "b"` adjacent literals | SPY0103 — use `+` or an f-string |
| `"n = " + 42` | No implicit str conversion — `str(42)` or f-string |
| `raise X from e` | `raise X("msg", e)` — pass the cause to the constructor |
| `del x` | Reserved, not implemented |
| `tuple(iterable)` | SPY0338 — arity is part of the type; use `list(...)` |
| `t[-1]` / `t[i]` on tuples | Tuple indices are non-negative integer literals only |
| `def f(xs: list = [])` | Defaults must be compile-time constants — use `xs: list[int]? = None` |
| `-> Iterator[int]` on a generator | Annotate the **element** type: `-> int` (wraps to `IEnumerable<int>`) |
| `lambda` without type context | Lambda params must be inferable or annotated |
| `cast(T, x)` / bare `x as T` | `x as! T` (throws) / `x as? T` (yields `T?`) |

## Types and declarations

Locals are inferred; everything else is annotated:

```python
def add(x: int, y: int) -> int:   # params always annotated; return type
    total = x + y                 # required unless the function returns
    return total                  # nothing; locals infer
```

- `int` is 32-bit `System.Int32` (no bignum; constant overflow is SPY0348 —
  suffix `42L` for int64). `float` is `System.Double`. Also: `int8/16/64`,
  `uint8/16/32/64`, `float32` (`1.5f`), `decimal` (`1.5m`), `char`, `object`,
  `array[T]` (raw .NET array).
- Re-typing a name needs a fresh annotation (it's shadowing, a new variable):
  `x = 5` then `x = "hi"` is a type error; write `x: str = "hi"`.
- Every block is a scope (C#-style): loop variables, `try` bindings, and
  anything declared inside `if`/`for`/`while`/`try` bodies **do not leak out**.
  Declare before the block: `x: int` then assign in each branch. Comprehension
  variables and walrus targets inside comprehensions never leak either.
- Collection annotations: `list[int]`, `dict[str, int]`, `set[int]`,
  `tuple[int, str]` — or shorthand `[int]`, `{str: int}`, `{int}`, `(int, str)`.
- Named tuples are type aliases: `type Point = tuple[x: float, y: float]`,
  constructed `(x=1.0, y=2.0)`, accessed `pos.x`.
- Type aliases: `type Coordinate = tuple[float, float]`.
- Generics are PEP-695 style, constraints with `&`, defaults allowed:
  `def find_max[T: IComparable[T]](items: list[T]) -> T:` ·
  `class Pair[K, V = str]:`. `identity[int]` must be called immediately;
  it is not a value (SPY0339).

## Optionals and nullables — two different types

`T?` (= `Optional[T]`, strict Sharpy struct union) and `T | None` (loose C#
nullable, for .NET interop) both exist and do **not** convert to each other.

```python
def find(items: list[str], target: str) -> str?:
    for item in items:
        if item == target:
            return Some(item)     # present value
    return None()                 # empty; bare None also works for T?

def main():
    r = find(["a", "b"], "b")
    # print(r.upper())            # WRONG — 'str?' has no member 'upper'
    if r is not None:             # narrowing unlocks the payload
        print(r.upper())
    print(r ?? "missing")         # ?? = fallback; NOT `r or "missing"`
    print(r.unwrap_or("missing")) # unwrap() / unwrap_or() / map() = methods
    print(r.is_some)              # is_some / is_none = properties, no parens
```

- On a `T?` you may only narrow (`is not None`), `match` on
  `Some(v)`/`None()`, use `?.` / `??` / postfix `?`, or call the Optional API.
  Everything else (`len(s)`, `s[0]`, passing `list[int]?` as `list[int]`) is a
  compile error. `T` converts to `T?` implicitly.
- `T | None` is the only inline union and behaves like a C# nullable: members
  are callable and fail at runtime. Convert to strict with `maybe`:
  `safe: str? = maybe dotnet_call()`. `None()` is invalid for `T | None`.
- Discarding an unconsumed `Optional`/`Result` statement warns SPY0480 — bind
  it, propagate it, or write `_ = f()`.

## Result types and error propagation

`T !E` = `Result[T, E]`. Expected failures return Results; exceptions are for
bugs.

```python
def validate(age: int) -> int !str:
    if age < 0:
        return Err("negative")
    return Ok(age)

def parse_both(a: str, b: str) -> tuple[int, int] !ValueError:
    x: int = int.parse(a)?        # postfix ? unwraps or early-returns the Err
    y: int = int.parse(b)?
    return Ok((x, y))

def main():
    match validate(25):
        case Ok(v):
            print(f"ok {v}")
        case Err(e):
            print(f"err {e}")
    r = try int("oops")           # try converts a throwing call to a Result
    s = try[ValueError] int("5")  # constrain the caught type
    print(r.is_err)               # is_ok / is_err are properties
```

`try`/`maybe` are low-precedence prefixes that capture the whole following
expression — parenthesize to combine with `?`: `(try read(path))?`.
`Some`/`Ok`/`Err` need type context (annotation, return type, or parameter) —
`x = Some(42)` alone cannot infer.

## Classes

```python
class Animal:
    name: str                       # ALL fields declared at class level

    def __init__(self, name: str):
        self.name = name            # creating undeclared self.x is an error

    @virtual                        # must opt in to allow overriding
    def speak(self) -> str:
        return "..."

    def describe() -> str:          # no self => automatically static
        return "an animal"

class Dog(Animal):                  # single base class; interfaces after it
    def __init__(self, name: str):
        super().__init__(name)      # super() ONLY as super().method(args)

    @override                       # required on every override
    def speak(self) -> str:
        return f"{self.name} says woof"
```

- `self` is never annotated. A method without `self` is static (`@static` is
  optional decoration).
- Access modifiers are **enforced**: `@public` (default), `@protected` or
  `_name`, `@private` or `__name`, `@internal`. `@abstract` / `@final` as in
  C# (`@final` = sealed). Abstract methods use a `...` body.
- Overloading works (C# semantics) — a second `def` with different parameter
  types is an overload, not a redefinition. Also true for module-level
  functions.
- Dunders: `__div__` not `__truediv__`; no `__floordiv__`, `__pow__`,
  `__call__`, `__del__`, `__radd__`/reflected, `__iadd__`/in-place. `__eq__`
  requires `__hash__` (and vice versa) — compile error, not a warning.
- Constructor chaining: `self.__init__(...)` as the first statement
  (→ C# `: this(...)`).
- `Self` is a built-in type name usable in signatures and fields.
- C# attributes use bracket syntax with snake_case mangling:
  `@[serializable]`, `@[obsolete("msg")]` — never bare `@SomeAttribute`
  (unknown bare decorators are SPY0444).
- `@dataclass` is built in (no import): auto constructor/`__eq__`/`__hash__`/
  `__repr__`.

### Properties — a keyword, not a decorator

```python
class Temperature:
    __celsius: float

    def __init__(self, celsius: float):
        self.__celsius = celsius

    property get fahrenheit(self) -> float:      # computed getter
        return self.__celsius * 9.0 / 5.0 + 32.0

    property set celsius(self, value: float):    # validating setter
        if value < -273.15:
            raise ValueError("below absolute zero")
        self.__celsius = value

class Config:
    property name: str = "default"    # auto-property: get + set
    property get id: int = 0          # get-only
    property init created: str        # settable only in __init__
```

### Other type declarations

```python
interface IShape:                     # explicit interfaces replace duck typing
    def area(self) -> float: ...      # `...` = abstract; `pass` = empty body
    def describe(self) -> str:
        return f"area={self.area()}"  # default implementation allowed

struct Vec2:                          # value type: copied on assignment
    x: float
    y: float

enum Color:                           # values REQUIRED, all same type
    RED = 1                           # (int type or str); no methods
    GREEN = 2

union Shape:                          # tagged union — the real sum type
    case Circle(radius: float)
    case Rectangle(width: float, height: float)
    case Empty                        # unit case: no parens

delegate Handler[in T](value: T) -> None   # named delegate (events/variance)

class Button:
    event on_click: EventHandler      # events are first-class; += / -= to
                                      # subscribe; raise via
                                      # self.on_click?.invoke(self, args)
```

## Pattern matching

```python
def area(shape: Shape) -> float:
    match shape:                      # statement form: bodies MUST be
        case Circle(r):               # indented blocks, never inline
            return 3.14159 * r * r
        case Rectangle(w, h):
            return w * h
        case Shape.Empty:             # unit cases: QUALIFY the pattern —
            return 0.0                # bare `case Empty:` is a capture
                                      # pattern that matches everything

def describe(n: int) -> str:
    return match n:                   # EXPRESSION form: inline bodies,
        case 0: "zero"                # must be exhaustive (SPY0416)
        case _ if n > 0: "positive"
        case _: "negative"
```

- Optionals/Results match through constructors: `case Some(v):` /
  `case None():` and `case Ok(v):` / `case Err(e):`. A payload *type* pattern
  over an Optional (`case str():` on a `str?`) is refused — SPY0498.
- Constructing a unit variant as a value needs call syntax: `Shape.Empty()`,
  not `Shape.Empty` (SPY0337).
- Relational patterns exist: `case > 100:`. Or-patterns `case 1 | 2:` cannot
  bind names. No list patterns (`case [a, b]:` is deferred).

## Operators

- Casts: `dog = animal as! Dog` (throws) · `dog = animal as? Dog` (→ `Dog?`).
  Target type must be non-nullable (SPY0334). `isinstance(x, T)` narrows `x`
  in the branch. Reified generics: `isinstance(x, Box[int])` is valid,
  `isinstance(obj, Box)` on an `object` is not — the reverse of Python.
- `//` and `%` are floored like Python (`-7 // 2 == -4`); `/` is always
  float; int division by zero raises `ZeroDivisionError`; there is no bignum,
  so overflow wraps at runtime (constant overflow is a compile error).
- `and`/`or` take and return `bool` only. `not x` on a non-bool is an error.
  Fallbacks use `??`; `x ??= default` assigns if absent; `a?.b?.c` chains
  safely and flattens (result is `T?`, never `T??`).
- Pipe: `x |> f(a)` ≡ `f(x, a)` — piped value becomes the FIRST argument.
- Partial application: `add_five = add(5, _)` · sections `(_ * 2)`, `(_ + _)`.
- `in` compiles to `.Contains` — user types implement `__contains__`.
- `ref`/`out`/`in` parameter modifiers exist:
  `def swap(a: ref int, b: ref int):` called as `swap(ref x, ref y)`;
  `if Int32.try_parse("42", out value: int):` declares inline.
- `/` and `*` parameter markers (positional-only / keyword-only) are enforced
  at compile time.

## Collections

- `list`, `dict`, `set` are Sharpy types with the Python API (`append`,
  negative indexing, slicing). `d[k]` throws `KeyError`; `d.get(k)` returns
  `V?` (an Optional — narrow or `unwrap_or`, don't compare to a plain value).
- Empty literals need an annotation: `xs: list[int] = []` or `list[int]()`.
- Comprehensions: multiple `for` clauses OK (names must be distinct),
  `if` filters, spread inside (`[*it for it in its]`, `{**d for d in ds}`).
- Spread: `[*a, *b]`, `{**d1, **d2}`, and tuple-into-fixed-params `f(*t)`
  work; `f(**d)` does not exist, and spreading a list into a `*args`
  parameter does not currently compile.
- `*args: int` is homogeneous (a .NET `params int[]`); only one per function.

## Strings

- UTF-16 code units: `len("😀") == 2`; indexing/slicing can split surrogate
  pairs. Use the `grapheme` stdlib module or `System.Text.Rune` for code
  points.
- f-strings work (nested, format specs). A dict literal inside one needs
  parens: `f"{({'k': v})}"`.
- Extra prefixes: `d"""..."""` dedents by the closing delimiter's indentation;
  `t"..."` builds a `Template` object (not a `str`); backticks escape
  identifiers that collide with keywords: `` def `class`(): ``.

## Generators and async

```python
def count_up(n: int) -> int:      # annotation = ELEMENT type
    i = 0                         # (emits IEnumerable<int>)
    while i < n:
        yield i
        i += 1
```

- `yield from` works. `return value` inside a generator is SPY0267 (bare
  `return` = stop). `yield` inside `try`/`except`/`finally` is SPY0270.
- `async def`/`await`/`async for`/`async with` are fully supported (map to
  `Task`, `await foreach`, …). `async def main()` is a valid entry point.
  There is no `asyncio.run`; `await asyncio.gather(t1, t2)` runs tasks
  concurrently as a statement (its results are not currently collectible).
  Async comprehensions run sequentially. No async generator expressions.

## Exceptions

```python
try:
    x = int("abc")                 # int() throws; int.parse() returns Result
except ValueError as e:            # Python exception names, Sharpy types
    print("bad number")
except OSError as e when e.message != "":   # `when` filters (soft keyword)
    raise RuntimeError("context", e)        # no `raise ... from`
finally:
    pass
```

`with` uses `__enter__`/`__exit__` or .NET `IDisposable` directly. `assert`
is a real runtime check (`AssertionError`), never stripped.

## .NET interop

snake_case ↔ PascalCase mangling is automatic in both directions:

```python
from system import Console
from system.io import File

def main():
    Console.write_line("hi")               # System.Console.WriteLine
    text = File.read_all_text("x.txt")     # File.ReadAllText
    evens = [1, 2, 3, 4].where(lambda x: x % 2 == 0)   # LINQ: always
    print(list(evens))                     # available, no import
```

- Module paths are lowercase (`system.collections.generic` →
  `System.Collections.Generic`).
- Instance members beat LINQ extensions: on a Sharpy list, `xs.reverse()` is
  in-place and `xs.count(2)` counts occurrences.
- A .NET sequence assigned into a Sharpy `list[T]` slot must be copied
  explicitly: `ys: list[int] = list(clr_list)` (direct assignment is refused).
- Backticks force exact CLR names: `` obj.`ExactName`() ``.

## Experimental features

Everything above is always-on. Exactly three syntax features are gated
(ungated use is SPY0331); enable with `--enable-feature <name>` or
`<Features>` in a `.spyproj` (NOT importable via `from __future__`):

| Flag | Unlocks |
|---|---|
| `matmul` | `@` / `@=` matrix-multiply operators (`__matmul__`) |
| `defer` | `defer` scope-exit statements |
| `property_observers` | `before_set:` / `after_set:` suites on auto-properties |

## Testing

`@test` marks a test function; plain `assert` inside rewrites to xUnit
assertions. Also `@test.fixture`, `@test.parametrize`, `@test.skip`.
`assert_raises(ValueError, "substring")` uses a positional match argument
(`match=` does not parse — `match` is a keyword).
