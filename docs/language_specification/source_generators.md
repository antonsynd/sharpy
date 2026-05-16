# Source Generators

Source generators let users write compile-time code generation in Sharpy itself. A class, struct, or function decorated with a generator bracket attribute (e.g., `@[GenerateEquals]`) triggers the compiler to invoke a user-defined generator, which produces additional Sharpy source code that is then merged into the compilation.

Source generators provide a pragmatic metaprogramming escape hatch: they cover the same use cases as Python `@decorator` rewriters (serialization, builders, equality, instrumentation) without exposing raw AST manipulation. The generator runs once per decorated declaration, the produced source flows through the normal pipeline (parse → semantic → codegen), and there is no runtime reflection or attribute lookup involved.

## Comparison to Python

| Mechanism | When it runs | Visibility | Sharpy equivalent |
|-----------|--------------|------------|-------------------|
| Python decorator that wraps the function | Runtime, on import | Replaces the binding | Sharpy decorators (e.g., `@dataclass`) |
| Python metaclass `__init_subclass__` | Class creation time | Mutates class object | Source generator on a class |
| Python `__init_subclass__` / `__set_name__` | Runtime | Late-bound | Source generator (compile-time) |
| C# Roslyn source generators | Compile time | Adds new declarations | **Source generators** (this document) |

Source generators sit between Sharpy's built-in `@dataclass` (compiler-recognized macro) and a hypothetical AST macro system. They are open-ended (any user can write one), but the generator API is intentionally narrow — generators see a *read-only* view of the decorated declaration and emit *Sharpy source strings*, not AST nodes.

## Trigger Syntax

A source generator is invoked by applying its name as a bracket attribute (`@[...]`) on a class, struct, or function:

```python
@[generate_equals]
class Point:
    x: int
    y: int

@[serializable("json")]
class Config:
    name: str
    debug: bool = False
```

Generator triggers use the same `@[Name]` / `@[Name(args, key=value)]` syntax described in [decorators.md](decorators.md), with the same name-mangling rules (`@[generate_equals]` resolves to a class named `GenerateEquals`). Because the syntax is shared, the compiler distinguishes a *generator trigger* from a *plain C# attribute* by resolving the name and checking whether it refers to a `SourceGenerator` subclass.

> **Note:** Plain bracket attributes (e.g., `@[serializable]` referring to `System.SerializableAttribute`) and generator triggers (e.g., `@[generate_equals]` referring to a `SourceGenerator` subclass) coexist. Resolution is unambiguous because each name maps to at most one symbol.

### Argument Restrictions

Unlike plain bracket attributes (which require compile-time constant arguments), generator trigger arguments may be **any expression that evaluates to a literal value**. The compiler extracts the value at compile time and forwards it to the generator:

- String, int, float, bool literals
- `None` (passed as `null`)
- Negative numeric literals (e.g., `-42`)
- Expressions composed of literals (e.g., `1 + 2`)

Non-literal arguments (variable references, function calls) are passed as their string representation via `ToString()`.

Arguments are forwarded to the generator as a `list[object]` (positional) and `dict[str, object]` (keyword) on `GeneratorContext`.

## The `SourceGenerator` Protocol

Generators are written in Sharpy by subclassing `SourceGenerator` from the `sharpy.generators` module:

```python
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class GenerateEquals(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None:
            return GeneratorOutput("")

        field_compares = " and ".join(
            f"self.{f.name} == other.{f.name}" for f in cls.fields
        )
        source = f"""
def __eq__(self, other: object) -> bool:
    if not isinstance(other, {cls.name}):
        return False
    return {field_compares}
"""
        return GeneratorOutput(source)
```

### `generate` Method Signature

The `generate` method is **abstract** and must be overridden:

```python
def generate(self, context: GeneratorContext) -> GeneratorOutput
```

- A generator class **must** subclass `SourceGenerator`.
- The class **must not** be `@abstract`.
- The signature **must** match exactly: one `GeneratorContext` parameter and a `GeneratorOutput` return type.
- Generator instances are constructed via the parameterless default constructor; **no constructor parameters are permitted**.

Violations are reported during semantic analysis (see [Diagnostics](#diagnostics)).

## `GeneratorContext` API

The compiler builds a `GeneratorContext` for each trigger and passes it to `generate`. The context contains a read-only description of the decorated declaration plus the arguments supplied to the trigger.

| Property | Type | Description |
|----------|------|-------------|
| `target_class` | `ClassInfo?` | The decorated class or struct (as `ClassInfo`), or `None` if the trigger was on a function |
| `target_function` | `FunctionInfo?` | The decorated function, or `None` if the trigger was on a class or struct |
| `arguments` | `list[object]` | Positional arguments from `@[Name(arg1, arg2)]` |
| `keyword_arguments` | `dict[str, object]` | Keyword arguments from `@[Name(key=value)]` |
| `module_name` | `str` | The fully-qualified module name (root namespace) of the target |

Exactly one of `target_class` / `target_function` is non-`None`. Generators that want to apply to both must check explicitly:

```python
def generate(self, context: GeneratorContext) -> GeneratorOutput:
    if context.target_class is not None:
        return self._generate_for_class(context.target_class)
    if context.target_function is not None:
        return self._generate_for_function(context.target_function)
    return GeneratorOutput("")
```

### `ClassInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | The unmangled (Sharpy) class name |
| `fields` | `list[FieldInfo]` | All declared fields, in source order |
| `methods` | `list[MethodInfo]` | All declared methods, in source order |
| `base_classes` | `list[str]` | Base class / interface names |
| `decorators` | `list[DecoratorInfo]` | All decorators (including the trigger itself) and bracket attributes |
| `is_dataclass` | `bool` | True if `@dataclass` is applied |

### `FunctionInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | The unmangled function name |
| `parameters` | `list[ParameterInfo]` | Parameters, in source order |
| `return_type` | `str?` | The declared return type, or `None` if absent |
| `decorators` | `list[DecoratorInfo]` | All decorators and bracket attributes |
| `is_static` | `bool` | True for `@static` methods |
| `is_async` | `bool` | True for `async def` |

### `FieldInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | The unmangled field name |
| `type_name` | `str?` | The declared type as written, or `None` |
| `has_default` | `bool` | True if the field has a default value |
| `default_value` | `str?` | The default value as a source string |

### `MethodInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | Unmangled method name |
| `parameters` | `list[ParameterInfo]` | Parameters (excluding `self`) |
| `return_type` | `str?` | Return type or `None` |
| `is_static` | `bool` | True for `@static` |
| `is_abstract` | `bool` | True for `@abstract` or ellipsis-bodied methods on `@abstract` classes |
| `is_virtual` | `bool` | True for `@virtual` |
| `is_async` | `bool` | True for `async def` |

### `ParameterInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | Parameter name |
| `type_name` | `str?` | Parameter type as written, or `None` |
| `has_default` | `bool` | True if a default value is present |

### `DecoratorInfo`

| Property | Type | Description |
|----------|------|-------------|
| `name` | `str` | Unmangled decorator name |
| `arguments` | `list[object]` | Positional arguments |
| `keyword_arguments` | `dict[str, object]` | Keyword arguments |
| `is_bracket_attribute` | `bool` | True if applied via `@[name]` syntax (vs. `@name`) |

> **Read-only contract:** All `*Info` types are immutable. Mutating their `list`/`dict` properties has no effect on the compiler — they are snapshots.

## `GeneratorOutput`

The `generate` method must return a `GeneratorOutput`:

```python
class GeneratorOutput:
    source: str                                    # Sharpy source to inject
    diagnostics: list[GeneratorDiagnostic]         # Optional diagnostics
```

`GeneratorOutput.empty` is a convenient sentinel for "no output."

### Emitting Source

The `source` string must contain valid Sharpy source that will be parsed and merged into the target file's compilation unit. Common patterns:

- Emitting **methods** for a class: write the method definitions directly (the compiler injects them into the target class).
- Emitting **standalone declarations**: write top-level `def`, `class`, etc.

> **Body injection model (current):** Method-shaped output emitted by a class-level generator is grafted onto the target class as if the user had written the methods inline. Top-level declarations are added to the same module as the target. The injected source goes through normal type checking and codegen, so all type errors are reported with reference to the generator's output.

### Reporting Diagnostics

Generators can attach diagnostics to their output:

```python
from sharpy.generators import GeneratorDiagnostic, GeneratorDiagnosticSeverity

return GeneratorOutput(
    source="",
    diagnostics=[
        GeneratorDiagnostic(
            "Cannot generate equality for class with no fields",
            GeneratorDiagnosticSeverity.error
        )
    ]
)
```

Severity values are `info`, `warning`, and `error`. Errors prevent compilation; warnings and info are surfaced through the normal diagnostic pipeline.

## Compilation Model

Source generation runs as a distinct phase **after** semantic analysis of the original files and **before** code generation. This allows generators to receive fully type-resolved context (field types, method signatures) while ensuring the generated source is type-checked before codegen.

### Two-Stage Compilation

A project containing source generators is compiled in two stages:

1. **Stage 1 — Generator Compilation.** All Sharpy files that *only* define source generators (subclasses of `SourceGenerator`) are compiled to .NET IL first.
2. **Stage 2 — Target Compilation.** All other files are compiled. When the compiler encounters a generator trigger, it loads the Stage 1 assembly, instantiates the generator type, builds the `GeneratorContext`, and calls `generate`. The returned source string is parsed and merged into the target compilation unit.

This staging is automatic — users do not configure it. During name resolution, any class that inherits from `SourceGenerator` is flagged, and files containing such classes are routed to Stage 1.

### File Separation Requirement

Generator definitions and their use sites **must live in different files**:

```python
# generators.spy — Stage 1
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class GenerateEquals(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput: ...

# point.spy — Stage 2
from generators import GenerateEquals  # imported, but only the trigger is used

@[generate_equals]
class Point:
    x: int
    y: int
```

If a generator and its trigger appear in the same file, the file is treated as a generator file (Stage 1). The trigger is silently not processed because the generator pipeline only runs against application files (Stage 2). For clarity, always keep generators in dedicated files.

### One-Pass Rule (D6)

Source generation is **non-recursive**. Generated source is *not* re-scanned for generator triggers; if the output of generator A produces text that looks like `@[B]`, the `B` trigger is ignored. This guarantees termination, makes incremental builds tractable, and keeps the mental model simple.

If you need a generator's output to be processed by another generator, apply both triggers to the original declaration and let each generator independently produce its slice of code.

## Constraints and Limits

| Constraint | Limit | Diagnostic on violation |
|------------|-------|--------------------------|
| Generator output size | **100 KB** (102,400 bytes UTF-8) | SPY0550 |
| Generator execution time | **30 seconds** wall-clock | SPY0551 |
| Recursion depth | **1 pass** (no recursive generation) | (silent — not re-scanned) |
| Generator file separation | Required | (handled by staging) |
| Generator constructor | Parameterless required | (instantiation failure → SPY0550) |

The 30-second timeout protects against infinite loops in generators; the 100 KB cap protects against runaway output. Both apply per-trigger.

## Diagnostics

Source generator failures are reported via two ranges:

### Validation Errors (SPY0445–SPY0447)

| Code | Severity | Meaning |
|------|----------|---------|
| **SPY0445** | Error | Generator class has an invalid `generate` signature (wrong parameters or return type) |
| **SPY0446** | Error | Generator class is `@abstract` and cannot be instantiated |
| **SPY0447** | Error | Generator trigger applied to an invalid target (e.g., a field or module-level variable) |

These are produced during semantic analysis, before the generator runs.

### Execution Errors (SPY0550–SPY0554)

| Code | Severity | Meaning |
|------|----------|---------|
| **SPY0550** | Error | Generator threw an exception, exceeded the size limit, or could not be instantiated |
| **SPY0551** | Error | Generator exceeded the 30-second timeout |
| **SPY0552** | Error | Generator returned source that failed to parse |
| **SPY0553** | Error | Cycle detected: a generator bracket attribute applied to another source generator class |
| **SPY0554** | Warning | Generator returned an empty (or whitespace-only) `source` |

User-emitted diagnostics from `GeneratorDiagnostic` are wrapped with the generator name (e.g., `[GenerateEquals] Cannot generate ...`) and carry the position of the trigger.

## Caching and Incremental Compilation

When `--incremental` is enabled, generator output is cached based on a SHA-256 hash of:
1. The generator's source text (so editing the generator invalidates).
2. The decorated declaration's source text (so editing the target invalidates).
3. The trigger arguments.

Cached output is stored in `obj/{Config}/.sharpy-symbols` (schema v13) and reused on subsequent builds when none of the inputs changed. See [Multi-File Compilation](README.md) and `IncrementalCompilationCache` for details.

## Examples

### Example 1: Generate `__eq__`

```python
# generators.spy
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class GenerateEquals(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None or len(cls.fields) == 0:
            return GeneratorOutput.empty

        compares = " and ".join(
            f"self.{f.name} == other.{f.name}" for f in cls.fields
        )
        return GeneratorOutput(f"""
def __eq__(self, other: object) -> bool:
    if not isinstance(other, {cls.name}):
        return False
    return {compares}
""")

# point.spy
from generators import GenerateEquals

@[generate_equals]
class Point:
    x: int
    y: int

def main() -> int:
    a = Point(1, 2)
    b = Point(1, 2)
    print(a == b)   # True
    return 0
```

### Example 2: Generator with Arguments

```python
# generators.spy
from sharpy.generators import SourceGenerator, GeneratorContext, GeneratorOutput

class Serializable(SourceGenerator):
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        cls = context.target_class
        if cls is None:
            return GeneratorOutput.empty

        # First positional argument: the format ("json" or "xml")
        format = context.arguments[0] as str if len(context.arguments) > 0 else "json"

        if format == "json":
            entries = ", ".join(
                f'"{f.name}": self.{f.name}' for f in cls.fields
            )
            return GeneratorOutput(f"""
def to_json(self) -> str:
    import json
    return json.dumps({{{entries}}})
""")
        return GeneratorOutput.empty

# config.spy
from generators import Serializable

@[serializable("json")]
class Config:
    name: str
    debug: bool
```

## Cross-References

- [decorators.md](decorators.md) — Bracket attribute syntax (`@[...]`), name mangling, argument restrictions.
- [dataclass.md](dataclass.md) — Compiler-recognized `@dataclass` macro (similar use case, narrower scope).
- [module_system.md](module_system.md) — Multi-file compilation, package layout, import semantics.

*Implementation: ✅ Native (Phases 1–7 complete) — generator IL is produced via Roslyn, executed against the targets in a sandboxed `Task.Run` with cancellation token, and the emitted Sharpy source is parsed back through the standard pipeline.*
