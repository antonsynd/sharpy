"""
Prompt templates for code generation, validation, and verification.
"""

import re
from pathlib import Path
from typing import Optional

# =============================================================================
# Error-pattern-specific remediation hints for retry prompts.
# Each entry is (regex_pattern, human-readable hint).  Patterns are matched
# case-insensitively against the full validation error text.
# =============================================================================

RETRY_REMEDIATION: list[tuple[str, str]] = [
    (r"SPY0456", "When defining __hash__, you MUST also define __eq__(self, other: object). "
     "Note: the parameter type must be 'object', not the class type."),
    (r"SPY0018", "Remove ALL backtick characters (`) from your code. "
     "Backticks are only valid as identifier escape syntax in Sharpy, not as code fences."),
    (r"SPY0220.*list\[.*\?\]", "Cannot create list[T?] from mixed T and None literals. "
     "Use an empty list and .append() each value individually."),
    (r"SPY0301.*no exported symbol", "Check that the imported symbol name matches exactly "
     "(case-sensitive) and that the symbol is defined at the module's top level."),
    (r"SPY0907", "An internal compiler error occurred. Try simplifying your code — "
     "avoid deeply nested generics or complex cross-module patterns."),
    (r"FormatException.*0x", "Hex literals are supported in Sharpy. If you see a FormatException, "
     "ensure hex values don't exceed the 64-bit signed integer range (max 0x7FFFFFFFFFFFFFFF)."),
]


def _get_remediation_hint(validation_error: str) -> str:
    """Match validation_error against known patterns and return remediation hints."""
    hints = []
    for pattern, hint in RETRY_REMEDIATION:
        if re.search(pattern, validation_error, re.IGNORECASE):
            hints.append(hint)
    if not hints:
        return ""
    return "\n\n## Remediation Hints\n\n" + "\n".join(f"- {h}" for h in hints) + "\n"


# =============================================================================
# Shared prompt sections — referenced by all 4 generation/regeneration prompts.
# Keep in sync: any update here propagates to all templates automatically.
# =============================================================================

BEHAVIORAL_RULES_SECTION = """\
### ⚠️ CRITICAL BEHAVIORAL RULES — Common pitfalls:
- **Interface vs override**: When implementing interface methods, do NOT use `@override`. `@override` is ONLY for overriding `@virtual` or `@abstract` methods from base classes.
- **Struct constructors**: Structs require an explicit `__init__` to accept constructor arguments. There is no auto-generated positional constructor.
- **String char type**: String indexing `s[i]` and iteration `for c in s` yield C# `char`, not `str`. You MUST wrap with `str()` before comparing or assigning: `str(s[i]) == "a"` (NOT `s[i] == "a"`), `c: str = str(s[i])` (NOT `c: str = s[i]`). Also use `str(c)` in for-each loops over strings.
- **Float division by zero**: Float division by zero produces `Infinity` (not an exception). Only integer division by zero raises `ZeroDivisionError`.
- **Self prefix**: Always use `self.field_name` to access instance fields inside methods.
- **Try-block scoping**: Variables declared inside `try`/`except`/`finally` blocks are block-scoped — they are NOT visible outside those blocks. Declare variables before the `try` if they need to be used in `except`/`else`/`finally` or after the `try`.
- **Float output**: Floats always print with at least one decimal place (e.g., `print(5.0)` outputs `5.0`). This matches Python behavior.
- **Set iteration order**: Set iteration order is NOT deterministic. Do NOT rely on set iteration order in expected output. Sort first if deterministic output is needed: `sorted(my_set)`.
- **Properties are not method calls**: Access properties WITHOUT parentheses: `obj.area` (NOT `obj.area()`). Properties look like field access.
- **Property backing fields**: Function-style properties need a separate backing field (e.g., `_name: str`). Auto-properties generate this automatically.
- **Spread type safety**: All elements in a spread must be compatible types: `[*int_list, *str_list]` is an error unless the target type is `list[object]`.
- **Spread requires variadic**: `func(*args)` only works if the function accepts `*args` variadic parameter, or if spreading a tuple that matches the exact parameter count.
- **With statement requires IDisposable**: The `with` statement only works with .NET types that implement `IDisposable` (e.g., `StringWriter`, `StreamReader`). Requires importing from `System.IO` or similar.
- **Named tuple field names**: All fields must be named, or none. Cannot mix named and unnamed fields in a named tuple type definition.
- **Match exhaustiveness**: Match statements on enums or bools must cover all possible values OR include a `case _:` wildcard."""

ENTRY_POINT_SECTION = """\
## CRITICAL: Program Entry Point Requirement

Every executable Sharpy program MUST have a `main()` function as its entry point:
- All executable statements (print, variable assignments, function calls) must be inside `main()`
- Only declarations (classes, functions, type aliases, static fields with type annotations) can be at module level
- Module-level variables require explicit type annotations: `counter: int = 0`
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime
- Example of WRONG: `def main(): ... \\n main()` - the `main()` call is forbidden at module level"""

ALLOWED_FEATURES_SECTION = """\
## CRITICAL: Allowed Features (Phases 0.1.0-0.1.18)

### ✅ ALLOWED - Use these features:

#### Program Structure
- **Entry point**: `def main():` is REQUIRED for all executable code
- **Module-level declarations**: classes, functions, constants, static fields (with type annotation)

#### Variables & Types (0.1.3)
- **Variables**: `x: int = 42` or `x = 42` (type inference)
- **Types**: `int`, `str`, `bool`, `float` (primitive types)
- **Additional numeric types**: `long` (64-bit int), `double` (explicit 64-bit float), `float32` (32-bit float)
- **Float output**: `float` maps to C# `double`. Floats always print with at least one decimal place (e.g., `print(5.0)` outputs `5.0`), matching Python behavior.
- **Nullable types**: `int?`, `str?` with `None` assignment
- **Operators**: `+`, `-`, `*`, `/`, `//`, `%`, `**`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `and`, `or`, `not`
- **Division**: `/` is ALWAYS float division (e.g., `5 / 2` → `2.5`). `//` is floor division (e.g., `5 // 2` → `2`).
- **Augmented assignment**: `+=`, `-=`, `*=`, `/=`
- **Null coalescing**: `??` (e.g., `name ?? "default"`)
- **Null conditional**: `?.` (e.g., `name?.upper()`)
- **Constants**: `const NAME: int = 42`
- **`in`/`not in` operators**: `x in collection`, `x not in collection` for lists, sets, dicts

#### Control Flow (0.1.4)
- **If statements**: `if`, `elif`, `else` with conditions
- **While loops**: `while condition:`
- **For loops**: `for i in range(n):`, `for i in range(start, end):`, `for i in range(start, end, step):`
- **Break/Continue**: inside loops only
- **Pass statement**: `pass` (no-op placeholder)

#### Functions (0.1.5)
- **Function definition**: `def name(param: type) -> return_type:`
- **Default parameters**: `def foo(x: int, y: int = 5) -> int:`
- **Keyword arguments**: `foo(x=10, y=20)`
- **Return**: `return value`
- **Function type parameters**: Use `(ParamType) -> ReturnType` syntax for callable parameters
  - `() -> int` (no params, returns int)
  - `(int) -> str` (one param)
  - `(int, str) -> bool` (two params)
  - Example: `def apply(func: (int) -> int, x: int) -> int:`

#### Classes (0.1.6)
- **Class definition**: `class ClassName:`
- **Fields**: `x: int` inside class body
- **Constructor**: `def __init__(self, params):`
- **Instance methods**: `def method(self) -> type:`
- **Static methods**: methods without `self` parameter (auto-detected)
- **Field access**: `obj.field`, `self.field`

#### Properties
- **Auto-properties (read-write)**: `property name: str` — generates getter and setter
- **Auto-properties with default**: `property name: str = "default"` — with initial value
- **Read-only auto-property**: `property get id: int = 0` — getter only, can be set in `__init__`
- **Init-only auto-property**: `property init token: str` — readable, but settable only in `__init__`
- **Function-style computed property**: `property get area(self) -> float:` with a body that computes a value
- **Function-style getter + setter**: Separate `property get name(self) -> str:` and `property set name(self, value: str):` blocks
- **Static properties**: `@static` + `property get name() -> str:` (no `self` parameter)
- **Access modifiers on properties**: `@private`, `@protected` before `property`
- **Virtual/abstract/override**: `@virtual property get ...`, `@abstract property get ...`, `@override property get ...`
- **Interface properties**: Interfaces can declare `property name: T` requirements
- **IMPORTANT**: Auto-properties and function-style properties CANNOT be mixed for the same property name in a class
- **IMPORTANT**: Properties are accessed like fields: `obj.name` (NOT `obj.name()` — no parentheses)
- **IMPORTANT**: Function-style properties require a user-provided backing field (e.g., `_name: str`)

#### Dunder Methods (Classes)
- **`__str__(self) -> str`**: String conversion, used by `print()` (maps to `.ToString()`)
- **`__eq__(self, other) -> bool`** / **`__hash__(self) -> int`**: Equality and hashing (must define both or neither)
- **`__bool__(self) -> bool`**: Truthiness in `if` statements (synthesizes `IBoolConvertible`)
- **`__len__(self) -> int`**: Enables `len(obj)` (synthesizes `ISized`)
- **`__iter__(self)`** / **`__next__(self)`**: Iterator protocol, enables `for item in obj:`
- **Arithmetic operators**: `__add__`, `__sub__`, `__mul__`, `__div__`, `__mod__` → `+`, `-`, `*`, `/`, `%`
- **Bitwise operators**: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`
- **Comparison operators**: `__lt__`, `__le__`, `__gt__`, `__ge__`, `__ne__`
- **Unary operators**: `__neg__` (unary `-`), `__pos__` (unary `+`), `__invert__` (`~`)
- **Container protocol**: `__getitem__(key)`, `__setitem__(key, value)`, `__contains__(item)`

#### Inheritance & Interfaces (0.1.7)
- **Single inheritance**: `class Child(Parent):`
- **Super calls**: `super().__init__(args)` in `__init__`, `super().method()` in `@override` methods
- **Abstract classes**: `@abstract` decorator on class
- **Abstract methods**: Use `@abstract` decorator. Two equivalent syntaxes:
  - Inline ellipsis: `@abstract` + `def area(self) -> float: ...`
  - Body-less: `@abstract` + `def area(self) -> float` (no colon, no body)
- **Virtual methods**: `@virtual` decorator — **REQUIRED** on any method that will be overridden
- **Override methods**: `@override` decorator — MUST match a `@virtual` or `@abstract` method in base class
- **IMPORTANT**: Abstract method implementations ARE overrides. When implementing an `@abstract` method in a subclass, you MUST use `@override`.
- **Final classes/methods**: `@final` decorator
- **Interfaces**: `interface IName:` with method signatures using `...` (NOTE: `interface` is a keyword, NOT a decorator — do NOT write `@interface`)
- **IMPORTANT**: Interfaces CANNOT declare fields (e.g., `name: str` inside an interface is invalid). Interfaces may only declare method signatures and `property` declarations.
- **Multiple interfaces**: `class Foo(IBar, IBaz):`
- **Access modifiers**: `@private`, `@protected`, `@internal` (default is public)
- **IMPORTANT**: Interface types have NO concrete members - you can only call methods declared in the interface. Do NOT access fields like `.value` through interface types.
- **IMPORTANT**: Unlike Python, `@virtual` is REQUIRED on base class methods that subclasses override. Without `@virtual`, using `@override` in a subclass will cause a compile error.
- **IMPORTANT**: When a parent class has required constructor parameters, subclass `__init__` MUST call `super().__init__(...)` with the required arguments.

#### Structs & Enums (0.1.8)
- **Structs**: `struct Name:` (value types, copied on assignment)
- **Enums**: `enum Name:` with explicit values (e.g., `RED = 1`, `PENDING = 0`)
- **Enum values**: `EnumName.VALUE` (e.g., `Color.RED`, `Status.PENDING`)
- **Enum output**: When printed, enums display in PascalCase (e.g., `print(Status.PENDING)` outputs `Pending`)

#### Type System (0.1.9)
- **Nullable types**: `T?` syntax
- **Type narrowing**: `if x is not None:` narrows type
- **Type aliases**: `type UserId = int`
- **Basic generics**: `class Box[T]:`, `def identity[T](x: T) -> T:`
- **Generic constraints**: `[T: IComparable]` - single constraint only
- **Multiple constraints**: NOT SUPPORTED - use single constraint only, do NOT write `[T: A, B]`

#### Module System (0.1.10)
- **Import**: `import module_name`, `import module as alias`
- **From import**: `from module import item1, item2`
- **Import alias**: `from module import Item as Alias`

#### Built-ins
- **Print**: `print(value)` - SINGLE argument only
- **Range**: `range()` in for loops
- **Boolean/None literals**: `True`, `False`, `None`
- **String literals**: `"hello"`, `'world'`
- **Math**: `abs(x)`, `pow(x, y)`, `round(x)`, `round(x, n)`, `divmod(x, y)`
- **Aggregation**: `min(iterable)`, `max(iterable)`, `sum(iterable)`, `all(iterable)`, `any(iterable)`
- **Ordering**: `sorted(iterable)`, `reversed(sequence)`
- **Iteration**: `enumerate(iterable)`, `zip(iter1, iter2)`, `iter(iterable)`, `next(iterator)`
- **Higher-order**: `filter(fn, iterable)`, `map(fn, iterable)`
- **Type conversions**: `int(x)`, `float(x)`, `bool(x)`, `str(x)`
- **Inspection**: `isinstance(obj, Type)`, `type(obj)`, `hash(obj)`, `repr(obj)`
- **I/O**: `input()`, `input(prompt)`

#### Tuple Types & Unpacking
- **Tuple type annotations**: `tuple[T1, T2]`, `tuple[T1, T2, T3]`
- **Tuple unpacking in assignments**: `x, y = point` where `point: tuple[int, int]`
- **Nested tuple unpacking**: `(a, b), c = nested_tuple`
- **Tuple unpacking in for loops**: `for i, val in enumerate(items):`
- **Tuple unpacking in comprehensions**: `[a + b for a, b in pairs]`
- **Rest patterns (star unpacking)**: `first, *rest = items`, `*rest, last = items`, `first, *mid, last = items`
- **IMPORTANT**: Only ONE starred expression per unpacking. The starred variable is always `list[T]`.

#### Named Tuples
- **Named tuple type**: `type Point = tuple[x: float, y: float]`
- **Named construction**: `p: Point = (x=1.0, y=2.0)` or positional `p: Point = (1.0, 2.0)`
- **Field access**: `p.x`, `p.y` (by name) or `p[0]`, `p[1]` (by index)
- **Named tuple return types**: `def get_bounds() -> tuple[min: int, max: int]:`
- **Unpacking**: `x, y = point` works with named tuples
- **IMPORTANT**: All fields must be named, or none. Cannot partially name fields.

#### Spread Operators
- **List spreading**: `combined: list[int] = [*first, 3, *second]` — merge lists
- **Set spreading**: `combined: set[int] = {{*set1, *set2}}` — merge sets
- **Dict spreading**: `merged: dict[str, int] = {{**defaults, **overrides}}` — merge dicts (later values override)
- **Function call spreading**: `result = func(*args)` — unpack list/tuple into positional arguments
- **Spread range into list**: `nums: list[int] = [*range(5)]`
- **IMPORTANT**: Spread in function calls only works with variadic functions (`*args` parameter) or when the spread matches exact parameter count. Non-variadic functions with `*args` spread will fail (SPY0357).
- **IMPORTANT**: Type safety is enforced — cannot mix incompatible types in spread

#### Walrus Operator (Assignment Expressions)
- **Walrus operator**: `:=` assigns a value within an expression
- **In conditionals**: `if (n := len(items)) > 0:` — assigns `n` and tests it
- **In while loops**: `while (line := read_line()) is not None:` — assign and test
- **Type inference only**: Type is always inferred from the right-hand side, no annotations allowed
- **IMPORTANT**: Walrus variables inside comprehensions are LOCAL to the comprehension — they do NOT leak to outer scope

#### Pattern Matching
- **Match statement**: `match value:` with `case` clauses
- **Literal patterns**: `case 42:`, `case "hello":`, `case True:`
- **Wildcard pattern**: `case _:` (default/catch-all)
- **Type patterns with binding**: `case int() as n:` — type check and bind
- **Guard clauses**: `case int() as n if n > 0:` — additional condition
- **Tuple patterns**: `case (0, 0):`, `case (x, y):`
- **Or patterns**: `case "a" | "b":` — match multiple values
- **Match expression**: `result = match value: case 1: "one" case _: "other"` — produces a value
- **Exhaustiveness**: Match on enums must cover all values or have `_` wildcard
- **IMPORTANT**: When matching with `case _:`, this is the catch-all default case

#### Context Managers (With Statement)
- **With statement**: `with expr as name:` for automatic resource cleanup
- **Without binding**: `with expr:` (no `as` clause)
- **Multiple resources**: `with expr1 as a, expr2 as b:`
- **Requires IDisposable**: The object must implement `System.IDisposable`
- **Common usage**: `from System.IO import StringWriter` then `with StringWriter() as writer:`
- **IMPORTANT**: Only works with .NET types implementing `IDisposable`. Not a general Python-style context manager.

#### Comparison Chaining
- **Chained comparisons**: `a < b < c` equivalent to `a < b and b < c`
- **Range checks**: `1 <= value <= 100`
- **Mixed operators**: `a < b <= c` — different comparison operators can be mixed
- **Single evaluation**: `a < f() < c` evaluates `f()` only once

#### F-Strings (0.1.11)
- **F-string interpolation**: `f"Hello {{name}}"`, `f"Result: {{x + y}}"`
- **Format specifiers**: `f"{{value:.2f}}"`, `f"{{num:05d}}"`

#### Collections (0.1.11)
- **List literals**: `nums: list[int] = [1, 2, 3]`
- **Dict literals**: `scores: dict[str, int] = {{"alice": 100, "bob": 85}}`
- **Set literals**: `unique: set[int] = {{1, 2, 3}}`
- **List comprehensions**: `[x * 2 for x in range(10)]`
- **Dict comprehensions**: `{{k: v * 2 for k, v in items.items()}}`
- **Set comprehensions**: `{{x for x in items if x > 0}}`
- **Collection iteration**: `for item in collection:`
- **WARNING**: Set iteration order is NOT deterministic. Do NOT rely on set iteration order in expected output. Sort first if deterministic output is needed: `sorted(my_set)`.
- **str.split()**: `"a,b,c".split(",")` returns `list[str]`.
- **len()**: `len(collection)` for lists, dicts, sets
- **Indexing**: `collection[index]`, `dict[key]`

#### Collection Methods
- **list**: `.append()`, `.pop()`, `.insert()`, `.remove()`, `.clear()`, `.reverse()`, `.sort()`, `.copy()`, `.extend()`, `.index()`, `.count()`
- **dict**: `.keys()`, `.values()`, `.items()`, `.get()`, `.pop()`, `.update()`, `.clear()`, `.copy()`, `.setdefault()`
- **set**: `.add()`, `.remove()`, `.discard()`, `.pop()`, `.clear()`
- **str**: `.upper()`, `.lower()`, `.strip()`, `.split()`, `.join()`, `.find()`, `.rfind()`, `.capitalize()`, `.title()`, `.count()`, `.isdigit()`, `.isalpha()`, `.isalnum()`, `.removeprefix()`, `.removesuffix()`

#### .NET Interop (0.1.12)
- **Import .NET namespaces**: `from system import Console`
- **Use .NET types**: After importing, use types normally

#### Exception Handling (0.1.13)
- **Try/except**: `try: ... except ExceptionType as e: ...`
- **Try/finally**: `try: ... finally: ...`
- **Try/except/else/finally**: Full exception handling pattern
- **Raise**: `raise ValueError("message")`
- **Available exception types**: `ValueError`, `TypeError`, `KeyError`, `IndexError`, `RuntimeError`, `NotImplementedError`, `AttributeError`, `ZeroDivisionError`, `OverflowError`, `Exception`

#### Lambda Expressions (0.1.14)
- **Lambdas**: `lambda x: x * 2`, `lambda a, b: a + b`
- **Higher-order functions**: Passing lambdas to functions that have typed parameters
- **Type inference**: Lambda parameter types are inferred from the expected function type context
- **IMPORTANT**: The receiving function MUST declare its parameter with a function type: `def apply(fn: (int) -> int) -> int:`
- **WARNING**: Lambdas CANNOT be assigned to `auto` variables — there is no type context to infer parameter types. Use an explicit function type: `square: (int) -> int = lambda n: n * n`, NOT `square: auto = lambda n: n * n`.

#### Optional Types (0.1.15)
- **Optional type**: `x: int? = Some(42)`, `y: int? = None()`
- **Optional constructors**: `Some(value)` wraps a value, `None()` represents absence
- **Optional methods**: `.unwrap()`, `.unwrap_or(default)`, `.map(lambda v: v * 2)`
- **Type annotation**: `T?` is shorthand for `Optional[T]`
- **IMPORTANT**: After `if x is not None:` narrowing, `x` is already the unwrapped type. Do NOT call `.unwrap()` after narrowing — it will fail because the type is no longer Optional.

#### Result Types (0.1.16)
- **Result type**: `x: int !str = Ok(42)`, `y: int !str = Err("failed")`
- **Result constructors**: `Ok(value)` for success, `Err(error)` for failure
- **Result methods**: `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`, `.map_err(fn)`
- **Type annotation**: `T !E` is shorthand for `Result[T, E]`

#### Maybe Expression (0.1.17)
- **Maybe**: `maybe nullable_value` converts `T | None` to `T?` (Optional)
- **Usage**: Useful for converting .NET nullable values to Sharpy optionals

#### Try Expression (0.1.18)
- **Try**: `try risky_call()` wraps a call in `Result[T, Exception]`
- **Try with type**: `try[ValueError] int("abc")` catches specific exception type
- **Usage**: Converts exception-throwing code into Result-based error handling"""

FORBIDDEN_FEATURES_SECTION = """\
### ❌ FORBIDDEN - Do NOT use these features (not yet implemented or restricted):
- **NO main() call at module level**: Do NOT write `main()` after defining it - it's auto-invoked by runtime
- **NO multi-argument print**: `print(a, b, c)` - use multiple `print()` calls
- **NO async/await**: Async programming not implemented
- **NO isinstance with tuples**: `isinstance(x, (int, str))` - use `or` instead
- **NO @interface decorator**: `interface` is a keyword, use `interface IName:` syntax
- **NO combining @abstract and @virtual**: abstract methods are inherently virtual in .NET — use only `@abstract`
- **NO union types (T | U)**: union types are not supported — use a common base class or interface instead
- **NO bare string indexing in comparisons/assignments**: `s[i] == "a"` or `c: str = s[i]` fails — always wrap with `str()`: `str(s[i]) == "a"`, `c: str = str(s[i])`
- **NO 'in' operator on strings**: `char in "abc"` — not yet supported
- **NO bare char iteration**: `for c in s:` yields `char` — use `str(c)` before comparing or assigning to `str`
- **NO `__repr__()` method**: removed — only `__str__()` exists (maps to `.ToString()`)
- **NO `del` statement**: `del x` — not supported
- **NO `**kwargs` spreading in function calls**: `func(**kwargs)` — not yet supported for keyword argument spreading
- **NO spread in non-variadic function calls**: `func(*args)` only works when the function has `*args` parameter or when spreading a tuple that matches exact parameter count"""

NAMING_RULES_SECTION = """\
### ⚠️ CRITICAL NAMING RULES - Avoid builtin conflicts:
- **NEVER name functions or variables**: `double`, `int`, `str`, `float`, `bool`, `len`, `print`, `range`, `abs`, `min`, `max`, `sum`, `round`, `input`, `type`, `list`, `dict`, `set`, `tuple`, `map`, `filter`, `zip`, `any`, `all`, `sorted`, `reversed`, `enumerate`, `chr`, `ord`, `hex`, `bin`, `oct`, `hash`, `id`, `open`, `file`, `exit`, `quit`, `long`, `float32`, `pow`, `divmod`, `isinstance`, `repr`, `iter`, `next`
- Use **descriptive names** like `double_value`, `multiply_by_two`, `calculate_double`, `doubled` instead
- Names like `double` conflict with the `double` type (float64) and will cause type errors"""

MULTIFILE_MODULE_RULES_SECTION = """\
## CRITICAL: Module System Rules (Phase 0.1.10)

### Import Syntax
- **Import entire module**: `import module_name` (then use `module_name.function()`)
- **Import with alias**: `import module_name as alias`
- **From import**: `from module_name import function1, function2`
- **From import with alias**: `from module_name import Item as Alias`

### Module File Structure
- Each `.spy` file is a module
- Module name = filename without `.spy` extension
- No `__init__.py` needed (not Python!)
- Modules in same directory can import each other
- The entry point file (`main.spy`) MUST have a `main()` function"""

MULTIFILE_FORBIDDEN_SECTION = """\
- **NO relative imports**: `from .module import x` - NOT SUPPORTED
- **NO package imports**: `from package.module import x` - NOT SUPPORTED
- **NO star imports**: `from module import *` - NOT SUPPORTED
- **NO circular imports between modules**"""


def load_test_fixtures(fixtures_dir: Path) -> dict[str, list[tuple[str, str]]]:
    """Load existing test fixtures organized by category.

    Returns:
        Dict mapping category name to list of (filename, content) tuples.
    """
    fixtures: dict[str, list[tuple[str, str]]] = {}

    if not fixtures_dir.exists():
        return fixtures

    for category_dir in fixtures_dir.iterdir():
        if not category_dir.is_dir() or category_dir.name.startswith("."):
            continue

        category_name = category_dir.name
        fixtures[category_name] = []

        for spy_file in sorted(category_dir.glob("*.spy")):
            try:
                content = spy_file.read_text()
                fixtures[category_name].append((spy_file.stem, content))
            except Exception:
                continue

    return fixtures


def format_fixtures_for_prompt(
    fixtures: dict[str, list[tuple[str, str]]],
    max_examples_per_category: int = 2,
    max_total_chars: int = 3000,
    max_categories: int = 8,
    shuffle: bool = True,
) -> str:
    """Format test fixtures as a prompt section showing what already exists.

    Args:
        fixtures: Dict from load_test_fixtures
        max_examples_per_category: Max number of example files to show per category
        max_total_chars: Maximum total characters for the section
        max_categories: Maximum number of categories to include. When shuffling
            is enabled, a random subset is selected each call for variety.
        shuffle: If True, randomize category order and example selection so
            successive calls produce different prompt content.

    Returns:
        Formatted string describing existing tests.
    """
    import random

    if not fixtures:
        return ""

    parts = []
    parts.append("## Existing Test Coverage (DO NOT duplicate these)")
    parts.append("")
    parts.append("The following tests already exist in the compiler test suite.")
    parts.append(
        "Generate something DIFFERENT from these - explore untested combinations!"
    )
    parts.append("")

    total_chars = 0

    # Determine category ordering
    categories = list(fixtures.items())
    if shuffle:
        random.shuffle(categories)
    else:
        categories.sort(key=lambda x: x[0])

    # Cap number of categories
    categories = categories[:max_categories]

    for category, test_files in categories:
        if total_chars > max_total_chars:
            parts.append(f"\n... and more categories (truncated for brevity)")
            break

        # List all test names in this category
        test_names = [name for name, _ in test_files]
        parts.append(
            f"### {category.replace('_', ' ').title()} ({len(test_files)} tests)"
        )
        parts.append(f"Existing tests: {', '.join(test_names)}")

        # Pick example files to show
        if shuffle:
            examples = random.sample(
                test_files, min(max_examples_per_category, len(test_files))
            )
        else:
            examples = test_files[:max_examples_per_category]

        for name, content in examples:
            # Truncate long examples
            if len(content) > 400:
                content = content[:400] + "\n# ... (truncated)"

            example_text = f"\n**{name}.spy:**\n```python\n{content}\n```"
            if total_chars + len(example_text) > max_total_chars:
                break
            parts.append(example_text)
            total_chars += len(example_text)

        parts.append("")

    return "\n".join(parts)


def get_code_generation_prompt(
    feature_focus: str = "general",
    complexity: str = "simple",
    example_snippets: list[str] | None = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for creating Sharpy code.

    Args:
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.
    """

    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:3], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    complexity_guide = {
        "simple": """
Generate SIMPLE code:
- 5-20 lines total
- 0-1 functions OR 1 simple class
- Basic arithmetic with int variables
- 1-3 print statements showing results
""",
        "medium": """
Generate MEDIUM complexity code:
- 20-40 lines total
- 1-2 functions OR 1-2 classes with methods
- Can use structs, enums, or simple inheritance
- Use ONE control flow type (if/elif/else OR for OR while)
- 3-5 print statements showing intermediate steps
""",
        "complex": """
Generate COMPLEX code:
- 40-70 lines total
- 2-3 classes with inheritance OR interfaces
- Can include: abstract classes, virtual/override methods, structs, enums
- Mix of control flow (if + for, or if + while)
- Can use nullable types, type aliases, generics, or module imports
- 5-8 print statements showing the flow
""",
    }

    return f"""You are generating Sharpy code for compiler testing (dogfooding).

{ENTRY_POINT_SECTION}

{ALLOWED_FEATURES_SECTION}

{FORBIDDEN_FEATURES_SECTION}

{BEHAVIORAL_RULES_SECTION}

{NAMING_RULES_SECTION}

{existing_fixtures_section}

## Task

Generate a **NOVEL** and **UNIQUE** Sharpy program testing: **{feature_focus}**
Complexity level: **{complexity}**

**IMPORTANT**: Your code should explore DIFFERENT patterns, algorithms, or feature combinations
than the existing tests shown above. Be creative! Some ideas:
- Use different variable names and scenarios
- Combine features in new ways
- Test edge cases not covered by existing tests
- Use different numeric values and control flow patterns

{complexity_guide.get(complexity, complexity_guide["simple"])}

{examples_section}

## Correct Sharpy Pattern Examples

```python
# Higher-order function with typed parameter
def apply_twice(fn: (int) -> int, x: int) -> int:
    return fn(fn(x))

# Class with virtual/override pattern
class Shape:
    @virtual
    def area(self) -> float:
        return 0.0

class Circle(Shape):
    radius: float

    def __init__(self, r: float):
        self.radius = r

    @override
    def area(self) -> float:
        return 3.14159 * self.radius ** 2

# Optional types
def find_first(items: list[int]) -> int?:
    if len(items) > 0:
        return Some(items[0])
    return None()

# Properties (auto-property and function-style)
class Rectangle:
    property width: float
    property height: float

    def __init__(self, w: float, h: float):
        self.width = w
        self.height = h

    property get area(self) -> float:
        return self.width * self.height

# Tuple unpacking and star patterns
point: tuple[int, int] = (3, 7)
x, y = point
items: list[int] = [1, 2, 3, 4, 5]
first, *rest = items

# Spread in collection literals
a: list[int] = [1, 2]
b: list[int] = [4, 5]
combined: list[int] = [*a, 3, *b]

# Walrus operator in conditional
if (n := len(items)) > 3:
    print(f"Long list: {{n}} items")

# Pattern matching
match value:
    case 0:
        print("zero")
    case int() as n if n > 0:
        print(f"positive: {{n}}")
    case _:
        print("other")

# Named tuple
type Point = tuple[x: float, y: float]
p: Point = (x=1.0, y=2.0)
print(p.x)

# Comparison chaining
if 0 < x < 10:
    print("in range")
```

## Output Format

Return ONLY valid Sharpy code with expected output in comments:

```python
# Example: Simple class with method
class Counter:
    value: int

    def __init__(self, start: int):
        self.value = start

    def increment(self) -> None:
        self.value += 1

    def get(self) -> int:
        return self.value

def main():
    c = Counter(10)
    c.increment()
    print(c.get())

# EXPECTED OUTPUT:
# 11
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument: print(value)
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in EXPECTED OUTPUT
- Keep the code simple and focused on testing the specified feature
- Output ONLY raw Sharpy code — NEVER include markdown code fences (```) in your output"""


def get_multifile_generation_prompt(
    feature_focus: str = "module_imports",
    complexity: str = "medium",
    example_snippets: list[str] | None = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for creating multi-file Sharpy code with imports.

    Args:
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.
    """

    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:3], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    complexity_guide = {
        "simple": """
Generate a SIMPLE multi-file project:
- 2 files total: main.spy + one module
- Module has 1-2 simple functions
- main.spy imports and uses those functions
- 2-3 print statements showing results
""",
        "medium": """
Generate a MEDIUM multi-file project:
- 2-3 files total: main.spy + 1-2 modules
- Modules can have classes, functions, or both
- Can use inheritance or interfaces across files
- One module can import from another
- 3-5 print statements showing intermediate steps
""",
        "complex": """
Generate a COMPLEX multi-file project:
- 3-4 files total: main.spy + 2-3 modules
- Modules with classes, interfaces, structs, enums
- Cross-module inheritance or interface implementation
- Complex imports (from module import item1, item2)
- 5-8 print statements showing the flow
""",
    }

    return f"""You are generating a MULTI-FILE Sharpy project for compiler testing (dogfooding).

## CRITICAL: Program Entry Point Requirement

The `main.spy` file MUST have a `main()` function as its entry point:
- All executable statements (print, variable assignments, function calls) must be inside `main()`
- Library modules (non-main.spy files) do NOT need a `main()` function
- Only declarations (classes, functions, type aliases, static fields) can be at module level
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime

{MULTIFILE_MODULE_RULES_SECTION}

{ALLOWED_FEATURES_SECTION}

### ⚠️ Key Patterns for Multi-File Projects

When using inheritance across modules, `@virtual` is REQUIRED on base class methods.
When a parent class has required constructor parameters, subclass `__init__` MUST call `super().__init__(...)` with the required arguments:

```python
# === In base_module.spy ===
class Animal:
    name: str

    def __init__(self, name: str):
        self.name = name

    @virtual
    def speak(self) -> str:
        return "..."

# === In derived_module.spy ===
from base_module import Animal

class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)

    @override
    def speak(self) -> str:
        return "Woof!"
```

When using higher-order functions, declare function type parameters explicitly:
`def apply(fn: (int) -> int, x: int) -> int:`

{FORBIDDEN_FEATURES_SECTION}
{MULTIFILE_FORBIDDEN_SECTION}

{BEHAVIORAL_RULES_SECTION}

{existing_fixtures_section}

## Task

Generate a **MULTI-FILE** Sharpy project testing: **{feature_focus}**
Complexity level: **{complexity}**

{complexity_guide.get(complexity, complexity_guide["medium"])}

{examples_section}

## Output Format

Return multiple files, each clearly marked with its filename. Use this EXACT format:

```
=== FILE: module_name.spy ===
# Module providing utility functions

def helper_function(x: int) -> int:
    return x * 2

class UtilityClass:
    value: int

    def __init__(self, v: int):
        self.value = v

=== FILE: main.spy ===
# Main entry point - imports from module_name
from module_name import helper_function, UtilityClass

def main():
    result: int = helper_function(5)
    print(result)

    obj = UtilityClass(10)
    print(obj.value)

# EXPECTED OUTPUT:
# 10
# 10
```

CRITICAL RULES:
1. Each file starts with `=== FILE: filename.spy ===`
2. One file MUST be named `main.spy` - this is the entry point
3. EXPECTED OUTPUT comment goes in main.spy ONLY
4. Use ONLY `from module import items` syntax (NOT `import module`)
5. Module names match filenames exactly (without .spy)
6. All print() calls must be in main.spy
7. NO circular imports between modules
8. Output ONLY raw Sharpy code — NEVER include markdown code fences (```) in your output"""


def get_regeneration_prompt(
    feature_focus: str,
    complexity: str,
    previous_code: str,
    validation_error: str,
    attempt: int,
    max_attempts: int,
    example_snippets: Optional[list[str]] = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for retrying code generation with validation feedback.

    Used when previous code generation failed validation. Provides the AI with
    the previous code and the specific validation error so it can fix the issue.

    Args:
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        previous_code: The previously generated code that failed validation.
        validation_error: The error message from validation.
        attempt: Current attempt number (1-indexed).
        max_attempts: Maximum number of attempts allowed.
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.

    Returns:
        Prompt string for regenerating code with feedback.
    """
    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:2], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    remediation_hint = _get_remediation_hint(validation_error)

    return f"""You are regenerating Sharpy code for compiler testing (dogfooding).

## REGENERATION ATTEMPT {attempt}/{max_attempts}

Your previous code FAILED validation. You must fix the issue and regenerate.

## Previous Code That Failed

```python
{previous_code}
```

## Validation Error

```
{validation_error}
```
{remediation_hint}
## Instructions

1. Analyze the validation error carefully
2. Identify which feature(s) are NOT allowed in phases 0.1.0-0.1.18
3. REMOVE or REPLACE the forbidden features
4. Keep the same general logic/intent but use only allowed features

{ENTRY_POINT_SECTION}

{ALLOWED_FEATURES_SECTION}

{FORBIDDEN_FEATURES_SECTION}

{BEHAVIORAL_RULES_SECTION}

{examples_section}

## Task

Regenerate the code for: **{feature_focus}** (complexity: **{complexity}**)

Fix the validation error above. Generate VALID code that does NOT use any forbidden features.

## Output Format

Return ONLY valid Sharpy code with expected output in comments:

```python
# Your fixed code here
...
# EXPECTED OUTPUT:
# <expected output lines>
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in EXPECTED OUTPUT
- Output ONLY raw Sharpy code — NEVER include markdown code fences (```) in your output"""


def get_multifile_regeneration_prompt(
    feature_focus: str,
    complexity: str,
    previous_files: dict[str, str],
    validation_error: str,
    attempt: int,
    max_attempts: int,
    example_snippets: Optional[list[str]] = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for retrying multi-file code generation with validation feedback.

    Used when previous multi-file code generation failed validation. Provides the AI
    with the previous files and the specific validation error so it can fix the issue.

    Args:
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        previous_files: The previously generated files that failed validation.
        validation_error: The error message from validation.
        attempt: Current attempt number (1-indexed).
        max_attempts: Maximum number of attempts allowed.
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.

    Returns:
        Prompt string for regenerating multi-file code with feedback.
    """
    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:2], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    # Format previous files with delimiters
    files_section = ""
    if not previous_files:
        files_section = "(no files from previous attempt)\n"
    else:
        for filename, code in previous_files.items():
            files_section += f"\n=== FILE: {filename} ===\n{code}\n"

    remediation_hint = _get_remediation_hint(validation_error)

    return f"""You are regenerating a MULTI-FILE Sharpy project for compiler testing (dogfooding).

## REGENERATION ATTEMPT {attempt}/{max_attempts}

Your previous multi-file project FAILED validation. You must fix the issue and regenerate.

## Previous Files That Failed

```
{files_section}
```

## Validation Error

```
{validation_error}
```
{remediation_hint}
## Instructions

1. Analyze the validation error carefully
2. Identify which file(s) and feature(s) are causing the issue
3. REMOVE or REPLACE the forbidden/invalid features
4. Keep the same general logic/intent but use only allowed features
5. Maintain correct import relationships between files

{ENTRY_POINT_SECTION}

{MULTIFILE_MODULE_RULES_SECTION}

{ALLOWED_FEATURES_SECTION}

{FORBIDDEN_FEATURES_SECTION}
{MULTIFILE_FORBIDDEN_SECTION}

{BEHAVIORAL_RULES_SECTION}

{examples_section}

## Task

Regenerate the multi-file project for: **{feature_focus}** (complexity: **{complexity}**)

Fix the validation error above. Generate VALID code that does NOT use any forbidden features.

## Output Format

Return multiple files, each clearly marked with its filename. Use this EXACT format:

```
=== FILE: module_name.spy ===
# Fixed module code here
...

=== FILE: main.spy ===
# Fixed main code here
...

# EXPECTED OUTPUT:
# <expected output lines>
```

CRITICAL RULES:
1. Each file starts with `=== FILE: filename.spy ===`
2. One file MUST be named `main.spy` - this is the entry point
3. EXPECTED OUTPUT comment goes in main.spy ONLY
4. Use ONLY `from module import items` syntax (NOT `import module`)
5. Module names match filenames exactly (without .spy)
6. All print() calls must be in main.spy
7. NO circular imports between modules
8. Use ONLY simple print() calls with ONE argument
9. For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
10. Output ONLY raw Sharpy code — NEVER include markdown code fences (```) in your output"""


def get_output_verification_prompt(
    code: str,
    expected_output: str,
    actual_output: str,
) -> str:
    """Generate a prompt for verifying output correctness."""

    return f"""Compare the expected and actual outputs from a Sharpy program.

## Code

```python
{code}
```

## Expected Output

```
{expected_output}
```

## Actual Output

```
{actual_output}
```

## Task

Determine if the actual output matches the expected output.

Consider:
- Whitespace differences (trailing spaces, newlines) should be IGNORED
- **Floating-point precision**: IEEE 754 trailing-digit differences are ACCEPTABLE
  - Example: "5.14" and "5.140000000000001" are EQUIVALENT (trailing precision noise)
  - Example: "7.85" and "7.8500000000000005" are EQUIVALENT
  - Compare floating-point numbers to ~10 significant figures
- **CRITICAL — Decimal point presence is significant**: The PRESENCE or ABSENCE of a decimal point changes the type/meaning:
  - "22.0" vs "22" → MISMATCH (float vs integer formatting)
  - "5.0" vs "5" → MISMATCH (float vs integer formatting)
  - "1.0" vs "1" → MISMATCH (float vs integer formatting)
  - These are NOT precision differences — they indicate a type mismatch in the program output
- String content should be exact
- Integer values should match exactly

## Response Format

Respond with EXACTLY one of:

```
MATCH
Outputs are equivalent.
```

OR

```
MISMATCH
Difference: [describe the specific difference]
Expected: [what was expected]
Got: [what was received]
```
"""


def get_test_uniqueness_prompt(
    code: str,
    existing_tests: list[tuple[str, str]],
) -> str:
    """Generate a prompt for checking if a test provides unique value.

    Args:
        code: The generated Sharpy code to evaluate.
        existing_tests: List of (test_name, test_code) tuples from the same category.

    Returns:
        Prompt string for uniqueness evaluation.
    """
    existing_section = ""
    if existing_tests:
        existing_section = "## Existing Tests in This Category\n\n"
        for name, content in existing_tests[:10]:  # Limit to 10 examples
            # Truncate long tests
            if len(content) > 300:
                content = content[:300] + "\n# ... (truncated)"
            existing_section += f"### {name}\n```python\n{content}\n```\n\n"
    else:
        existing_section = (
            "## Existing Tests\n\nNo existing tests in this category yet.\n"
        )

    return f"""Evaluate whether this Sharpy test provides unique value compared to existing tests.

## New Test Code

```python
{code}
```

{existing_section}

## Evaluation Criteria

A test is UNIQUE and worth adding if it:
- Tests a different feature combination than existing tests
- Uses a meaningfully different algorithm or pattern
- Covers an edge case not tested by existing tests
- Tests a feature at a different complexity level

A test is a DUPLICATE and should be skipped if it:
- Tests the same feature combination with only trivial differences (variable names, numeric values)
- Is structurally identical to an existing test
- Doesn't add meaningful coverage beyond what already exists

## Response Format

Respond with EXACTLY one of:

```
UNIQUE
Reason: [brief explanation of what new coverage this provides]
```

OR

```
DUPLICATE
Reason: [which existing test(s) already cover this]
```
"""


def extract_expected_output(code: str) -> Optional[str]:
    """Extract expected output from code comments."""
    lines = code.split("\n")
    in_expected_block = False
    expected_lines = []

    for line in lines:
        stripped = line.strip()

        # Check for start of expected output block
        if "EXPECTED OUTPUT" in stripped.upper() or "EXPECTED:" in stripped.upper():
            in_expected_block = True
            continue

        # Check for end of expected output block
        if in_expected_block:
            if stripped.startswith("#"):
                # Remove the comment marker
                output_line = (
                    stripped[1:].strip() if stripped[1:2] == " " else stripped[1:]
                )
                # Empty comment lines might be intentional empty output
                expected_lines.append(output_line)
            elif stripped and not stripped.startswith("#"):
                # Non-comment, non-empty line ends the block
                break

    if expected_lines:
        # Join lines and strip trailing whitespace from each line,
        # but preserve the structure. Add trailing newline since print() adds one.
        result = "\n".join(expected_lines).rstrip()
        return result + "\n" if result else None
    return None


def extract_code_block(response: str) -> Optional[str]:
    """Extract code from a response that might have markdown formatting."""
    import re

    # Try to find code block with python/sharpy markers
    pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    matches = re.findall(pattern, response, re.DOTALL)

    if matches:
        # Return the longest match (most likely the main code)
        return max(matches, key=len).strip()

    # If no code block, check if the entire response looks like code
    lines = response.strip().split("\n")
    if lines and not any(line.startswith("```") for line in lines):
        # Check if it looks like code (has def, print, assignments, etc.)
        code_indicators = ["def ", "print(", "= ", "if ", "for ", "while ", "#"]
        if any(indicator in response for indicator in code_indicators):
            return response.strip()

    return None


def extract_multifile_code(response: str) -> Optional[dict[str, str]]:
    """Extract multiple files from a response with file markers.

    Parses responses in the format:
    ```
    === FILE: module_name.spy ===
    <code>

    === FILE: main.spy ===
    <code>
    ```

    Args:
        response: The AI response potentially containing multiple files.

    Returns:
        Dictionary mapping filename to code content, or None if parsing fails.
        Returns None if no valid multi-file structure is found.
    """
    import re

    # First, try to extract from code blocks
    code_block_pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    code_blocks = re.findall(code_block_pattern, response, re.DOTALL)

    # Use the code blocks if found, otherwise use the whole response
    content = "\n".join(code_blocks) if code_blocks else response

    # Pattern to match file markers: === FILE: filename.spy ===
    file_pattern = r"===\s*FILE:\s*([a-zA-Z_][a-zA-Z0-9_]*\.spy)\s*===\s*\n"

    # Find all file markers and their positions
    markers = list(re.finditer(file_pattern, content, re.IGNORECASE))

    if not markers:
        return None

    files: dict[str, str] = {}

    for i, match in enumerate(markers):
        filename = match.group(1).lower()  # Normalize filename to lowercase
        start_pos = match.end()

        # Find the end of this file's content (start of next file or end of content)
        if i + 1 < len(markers):
            end_pos = markers[i + 1].start()
        else:
            end_pos = len(content)

        # Extract and clean the code
        code = content[start_pos:end_pos].strip()

        # Remove any trailing file markers that might have been included
        code = re.sub(r"\n===\s*FILE:.*$", "", code, flags=re.IGNORECASE)

        # Remove stray markdown fences (``` lines) that the AI sometimes appends
        code = re.sub(r"^```\w*\s*$", "", code, flags=re.MULTILINE).strip()

        if code:
            files[filename] = code

    # Validate: must have at least 2 files and one must be main.spy
    if len(files) < 2:
        return None

    if "main.spy" not in files:
        return None

    return files


def extract_expected_output_from_multifile(files: dict[str, str]) -> Optional[str]:
    """Extract expected output from the main.spy file in a multi-file project.

    Args:
        files: Dictionary mapping filename to code content.

    Returns:
        The expected output string, or None if not found.
    """
    if "main.spy" not in files:
        return None

    return extract_expected_output(files["main.spy"])
