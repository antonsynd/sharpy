# Source Generators — Practical Guide

This guide walks through four end-to-end examples of writing source generators in Sharpy. For the formal specification (trigger syntax, full API surface, diagnostic codes, constraints), see [language_specification/source_generators.md](../language_specification/source_generators.md).

## Project Layout

Source generators must live in **separate files** from the code that uses them, because the compiler builds generators to IL in Stage 1 before applying them in Stage 2. A typical layout:

```
myproject/
├── myproject.spyproj
├── generators/
│   ├── equals.spy        # GenerateEquals
│   ├── repr.spy          # GenerateRepr
│   ├── serializable.spy  # Serializable
│   └── builder.spy       # Builder
└── src/
    ├── point.spy         # uses @[generate_equals], @[generate_repr]
    ├── config.spy        # uses @[serializable("json")]
    └── main.spy
```

Build with the standard project compilation:

```bash
sharpyc project myproject.spyproj
```

The compiler automatically detects which files define generators (any file containing a `SourceGenerator` subclass) and stages compilation accordingly.

---

## Example 1: `@[GenerateEquals]`

**Goal:** Synthesize `__eq__` and `__hash__` for any class, derived from its declared fields.

### Generator

```python
# generators/equals.spy
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class GenerateEquals(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None:
            return GeneratorOutput.empty

        if len(cls.fields) == 0:
            # Trivial case: all instances are equal
            return GeneratorOutput("""
def __eq__(self, other: object) -> bool:
    return isinstance(other, type(self))

def __hash__(self) -> int:
    return 0
""")

        compares = " and ".join(
            f"self.{f.name} == other.{f.name}" for f in cls.fields
        )
        hash_terms = ", ".join(f"self.{f.name}" for f in cls.fields)

        return GeneratorOutput(f"""
def __eq__(self, other: object) -> bool:
    if not isinstance(other, {cls.name}):
        return False
    return {compares}

def __hash__(self) -> int:
    return hash(({hash_terms},))
""")
```

### Target

```python
# src/point.spy
from generators.equals import GenerateEquals

@[generate_equals]
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

### Generated Code (conceptual)

```python
# What the compiler effectively merges into Point's body:
def __eq__(self, other: object) -> bool:
    if not isinstance(other, Point):
        return False
    return self.x == other.x and self.y == other.y

def __hash__(self) -> int:
    return hash((self.x, self.y,))
```

### Usage

```python
# src/main.spy
from src.point import Point

def main() -> int:
    a = Point(1, 2)
    b = Point(1, 2)
    c = Point(3, 4)

    print(a == b)       # True
    print(a == c)       # False
    print(hash(a) == hash(b))  # True
    return 0
```

---

## Example 2: `@[GenerateRepr]`

**Goal:** Generate a `__repr__` that prints the class name and its fields, similar to Python's `dataclass(repr=True)`.

### Generator

```python
# generators/repr.spy
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class GenerateRepr(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None:
            return GeneratorOutput.empty

        # Build "x={self.x}, y={self.y}" inside an f-string.
        parts = ", ".join(
            f"{f.name}={{self.{f.name}}}" for f in cls.fields
        )

        return GeneratorOutput(f"""
def __repr__(self) -> str:
    return f"{cls.name}({parts})"
""")
```

### Target

```python
# src/point.spy
from generators.equals import GenerateEquals
from generators.repr import GenerateRepr

@[generate_equals]
@[generate_repr]
class Point:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y
```

> **Two generators on one class.** Each `@[...]` triggers an independent invocation. Because of the one-pass rule, neither generator sees the other's output — both see the *original* `Point` declaration.

### Generated Code (conceptual)

```python
def __repr__(self) -> str:
    return f"Point(x={self.x}, y={self.y})"
```

### Usage

```python
# src/main.spy
from src.point import Point

def main() -> int:
    p = Point(3, 4)
    print(repr(p))   # Point(x=3, y=4)
    return 0
```

---

## Example 3: `@[Serializable("json")]`

**Goal:** Use generator arguments to pick a serialization format. Generates `to_json` (and pluggably `to_xml`) plus a static `from_json` factory.

### Generator

```python
# generators/serializable.spy
from sharpy.generators import (
    SourceGenerator,
    GeneratorContext,
    GeneratorOutput,
    GeneratorDiagnostic,
    GeneratorDiagnosticSeverity,
)

class Serializable(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None:
            return GeneratorOutput.empty

        # Default to JSON if no format argument given.
        format: str = "json"
        if len(context.arguments) > 0:
            format = context.arguments[0] as str

        if format == "json":
            return self._emit_json(cls)

        return GeneratorOutput(
            source="",
            diagnostics=[
                GeneratorDiagnostic(
                    f"Unsupported serialization format: '{format}' (expected 'json')",
                    GeneratorDiagnosticSeverity.error,
                )
            ],
        )

    def _emit_json(self, cls):
        # Build a JSON object literal: {"name": self.name, "debug": self.debug}
        entries = ", ".join(
            f'"{f.name}": self.{f.name}' for f in cls.fields
        )

        # from_json: positional args matching field declaration order
        ctor_args = ", ".join(
            f'data["{f.name}"]' for f in cls.fields
        )

        return GeneratorOutput(f"""
def to_json(self) -> str:
    import json
    return json.dumps({{{entries}}})

@static
def from_json(payload: str) -> {cls.name}:
    import json
    data = json.loads(payload)
    return {cls.name}({ctor_args})
""")
```

### Target

```python
# src/config.spy
from generators.serializable import Serializable

@[serializable("json")]
class Config:
    name: str
    debug: bool
    retries: int

    def __init__(self, name: str, debug: bool, retries: int):
        self.name = name
        self.debug = debug
        self.retries = retries
```

### Generated Code (conceptual)

```python
def to_json(self) -> str:
    import json
    return json.dumps({"name": self.name, "debug": self.debug, "retries": self.retries})

@static
def from_json(payload: str) -> Config:
    import json
    data = json.loads(payload)
    return Config(data["name"], data["debug"], data["retries"])
```

### Usage

```python
# src/main.spy
from src.config import Config

def main() -> int:
    cfg = Config("prod", False, 3)
    payload = cfg.to_json()
    print(payload)                # {"name": "prod", "debug": false, "retries": 3}

    restored = Config.from_json(payload)
    print(restored.name)          # prod
    return 0
```

### Reporting Errors from a Generator

Note how `Serializable` emits an `error`-severity `GeneratorDiagnostic` for unknown formats:

```python
@[serializable("yaml")]   # → SPY0550: [Serializable] Unsupported serialization format: 'yaml' (expected 'json')
class BadConfig:
    name: str
```

Generator diagnostics are surfaced through the normal compiler diagnostic pipeline, prefixed with the generator name in brackets.

---

## Example 4: `@[Builder]`

**Goal:** Generate a builder pattern: a nested `Builder` class with `with_<field>(value)` methods that return the builder, plus a `build()` method that constructs the target.

### Generator

```python
# generators/builder.spy
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class Builder(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None or len(cls.fields) == 0:
            return GeneratorOutput.empty

        # Build init lines: each field starts as None or its default.
        init_lines = "\n        ".join(
            f"self._{f.name}: {f.type_name}? = None" for f in cls.fields
        )

        # Build with_<field> methods.
        with_methods = "\n\n    ".join(
            f"""def with_{f.name}(self, value: {f.type_name}) -> "Builder":
        self._{f.name} = value
        return self"""
            for f in cls.fields
        )

        # Build the final constructor call.
        ctor_args = ", ".join(f"self._{f.name}!" for f in cls.fields)

        # Static factory on the target.
        target_factory = f"""
@static
def builder() -> "{cls.name}.Builder":
    return {cls.name}.Builder()

class Builder:
    def __init__(self):
        {init_lines}

    {with_methods}

    def build(self) -> "{cls.name}":
        return {cls.name}({ctor_args})
"""
        return GeneratorOutput(target_factory)
```

### Target

```python
# src/user.spy
from generators.builder import Builder

@[builder]
class User:
    name: str
    email: str
    age: int

    def __init__(self, name: str, email: str, age: int):
        self.name = name
        self.email = email
        self.age = age
```

### Generated Code (conceptual)

```python
@static
def builder() -> "User.Builder":
    return User.Builder()

class Builder:
    def __init__(self):
        self._name: str? = None
        self._email: str? = None
        self._age: int? = None

    def with_name(self, value: str) -> "Builder":
        self._name = value
        return self

    def with_email(self, value: str) -> "Builder":
        self._email = value
        return self

    def with_age(self, value: int) -> "Builder":
        self._age = value
        return self

    def build(self) -> "User":
        return User(self._name!, self._email!, self._age!)
```

### Usage

```python
# src/main.spy
from src.user import User

def main() -> int:
    user = (User.builder()
        .with_name("Ada")
        .with_email("ada@example.com")
        .with_age(36)
        .build())

    print(user.name)    # Ada
    print(user.email)   # ada@example.com
    print(user.age)     # 36
    return 0
```

---

## Tips for Writing Generators

### Always Handle the `None` Case

A generator may be invoked on either a class or a function. Always check `target_class` / `target_function` before dereferencing:

```python
def generate(self, context: GeneratorContext) -> GeneratorOutput:
    cls = context.target_class
    if cls is None:
        return GeneratorOutput.empty
    # ... safe to use cls
```

### Validate Inputs Defensively

Raise `error`-severity diagnostics rather than producing broken source. Broken output triggers SPY0552 (unparseable generated source) with confusing error locations; explicit diagnostics are far more user-friendly.

### Keep Output Small

The 100 KB output cap (SPY0550) is generous, but emitting a 50 KB method block is a sign that you should generate a small dispatch that calls into a hand-written helper instead.

### Don't Rely on Other Generators

Source generation is **one-pass**. A generator never sees code emitted by another generator on the same target. If two generators must share state, redesign so one generator emits the union, or pre-compute shared state outside the generator system.

### Use `@dataclass` First

Sharpy's built-in `@dataclass` already handles `__init__`, `__eq__`, `__hash__`, and `__repr__`. Reach for source generators only when you need patterns that `@dataclass` doesn't cover (e.g., serialization, builders, instrumentation, custom equality semantics).

---

## See Also

- [language_specification/source_generators.md](../language_specification/source_generators.md) — Full specification with API tables, diagnostic codes, and compilation model.
- [language_specification/dataclass.md](../language_specification/dataclass.md) — Compiler-recognized macro for the common boilerplate case.
- [language_specification/decorators.md](../language_specification/decorators.md) — Bracket attribute syntax and name mangling rules.
