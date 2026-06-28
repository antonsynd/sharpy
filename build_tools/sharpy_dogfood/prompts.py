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
    (
        r"SPY0456",
        "When defining __hash__, you MUST also define __eq__(self, other: object). "
        "Note: the parameter type must be 'object', not the class type.",
    ),
    (
        r"SPY0018",
        "Remove ALL backtick characters (`) from your code. "
        "Backticks are only valid as identifier escape syntax in Sharpy, not as code fences.",
    ),
    (
        r"SPY0220.*list\[.*\?\]",
        "Cannot create list[T?] from mixed T and None literals. "
        "Use an empty list and .append() each value individually.",
    ),
    (
        r"SPY0301.*Module '(\w+)' has no exported symbol '(\w+)'",
        "The module does not define the imported symbol. You must add the definition "
        "of the missing symbol to the corresponding .spy file. Check that ALL symbols "
        "imported in main.spy and other files actually exist in their source modules. "
        "Symbol names are case-sensitive and must be defined at the module's top level. "
        "If the symbol is an @abstract class, try simplifying: remove the @abstract decorator "
        "or move the class to the importing file. "
        "IMPORTANT: Do NOT reference symbols that you did not define in the source file.",
    ),
    (
        r"SPY0907",
        "An internal compiler error occurred. Try simplifying your code — "
        "avoid deeply nested generics or complex cross-module patterns.",
    ),
    (
        r"FormatException.*0x",
        "Hex literals are supported in Sharpy. If you see a FormatException, "
        "ensure hex values don't exceed the 64-bit signed integer range (max 0x7FFFFFFFFFFFFFFF).",
    ),
    (
        r"CS0513.*abstract.*non-abstract",
        "The class containing @abstract methods must itself be decorated with @abstract. "
        "Add @abstract before the class definition.",
    ),
    (
        r"CS0506.*not.*virtual",
        "Cannot @override a method that is not @virtual or @abstract in the base class. "
        "Add @virtual to the base class method, or remove @override from the subclass method.",
    ),
    (
        r"SPY0222",
        "Operator not supported on this type. For enum comparisons, ensure both sides "
        "are the same enum type. For class comparisons, implement __eq__.",
    ),
    (
        r"CS0029.*Exception|CS0155",
        "Custom exception classes must extend Exception or a builtin exception type. "
        "Change 'class MyError:' to 'class MyError(Exception):'.",
    ),
    (
        r"SPY0220.*list\[.*\].*list\[",
        "Generic collections are INVARIANT in Sharpy — list[Child] cannot be assigned to "
        "list[Parent], even if Child extends Parent. Fix: declare the variable/parameter as "
        "list[Parent] from the start, and add items individually. "
        "Example: shapes: list[Shape] = [] then shapes.append(Circle(5.0)).",
    ),
    (
        r"SPY0414",
        "Do NOT call dunder methods directly. Use the corresponding builtin function instead: "
        "len(obj) not obj.__len__(), reversed(obj) not obj.__reversed__(), "
        "str(obj) not obj.__str__(), bool(obj) not obj.__bool__().",
    ),
    (
        r"SPY0107.*self\.\w+\s*:",
        "Do NOT put type annotations on self.field assignments in __init__. "
        "Write 'self.name = name', NOT 'self.name: str = name'.",
    ),
    (
        r"does not contain a definition for 'Unwrap'",
        "After `if x is not None:`, the variable is narrowed to its unwrapped type. "
        "Do NOT call `.unwrap()` — use the variable directly. "
        "`.unwrap()` is only available on Optional (T?) types before narrowing.",
    ),
    (
        r"does not contain a definition for '(Lower|Upper|Strip|Split|Replace|Find|Count|Title|Capitalize|Isdigit|Isalpha)'",
        "Iterating over `str` yields `char`, not `str`. "
        "String methods like `.lower()`, `.upper()` are not available on `char`. "
        "Use `str(c)` to convert a char back to a string if you need string methods.",
    ),
    (
        r"Cannot apply indexing.*tuple",
        "Star unpacking (`*mid`) works with `list` but NOT `tuple`. "
        "Tuples are fixed-size value types. "
        "Access tuple elements by index: `t[0]`, `t[1]`, etc. "
        "Or convert to list first if you need star unpacking.",
    ),
    (
        r"return type must be.*to match overridden member",
        "Generator methods (with `yield`) have `IEnumerable<T>` return type in C#. "
        "They cannot override non-generator abstract methods. "
        "Either mark the base method as a generator too, or don't use `yield` in the override.",
    ),
    (
        r"SPY0273",
        "`await` can only be used inside `async def` functions. "
        "Either mark the enclosing function as `async def`, or remove the `await`.",
    ),
    (
        r"SPY0274",
        "`await` cannot be used inside lambda expressions. "
        "Move the async logic into a named `async def` function instead.",
    ),
    (
        r"SPY0360",
        "`async for` requires an IAsyncEnumerable operand and must be inside an `async def` function. "
        "Check that the iterable is an async iterable and the enclosing function is `async def`.",
    ),
    (
        r"SPY0370",
        "This argument must be passed positionally (it's before the `/` separator). "
        "Remove the keyword name from the call: use `foo(1, 2)` not `foo(x=1, y=2)`.",
    ),
    (
        r"SPY0371",
        "This argument must be passed as a keyword (it's after the `*` separator). "
        "Add the keyword name: use `bar(1, b=2)` not `bar(1, 2)`.",
    ),
    (
        r"SPY0416",
        "Non-exhaustive match expression. All possible values must be covered. "
        "Add a `case _:` wildcard arm, or add missing enum/bool/union cases.",
    ),
    (
        r"SPY0417",
        "Variance annotation (`out`/`in`) is only allowed on interface or delegate type parameters. "
        "Remove the variance annotation from class or struct type parameters.",
    ),
    (
        r"SPY0418",
        "Covariant type parameter (`out T`) can only appear in output (return) positions. "
        "Move `T` out of parameter positions, or change to `in T`.",
    ),
    (
        r"SPY0419",
        "Contravariant type parameter (`in T`) can only appear in input (parameter) positions. "
        "Move `T` out of return positions, or change to `out T`.",
    ),
    (
        r"SPY0124",
        "Empty union declarations are not allowed. "
        "Add at least one `case` to the union definition.",
    ),
    (
        r"SPY0365",
        "Duplicate union case name. " "Each case in a union must have a unique name.",
    ),
    (
        r"SPY0322",
        "Built-in decorators (@abstract, @virtual, @override, @static, @final, "
        "@private, @protected, @internal) do NOT accept arguments. "
        "Remove the parentheses and arguments: use `@abstract` not `@abstract(something)`.",
    ),
    (
        r"SPY0425",
        "Custom decorator arguments must be compile-time constants: "
        "strings, ints, floats, bools, None, enum member access, or type(X). "
        "Variable references and function calls are not allowed.",
    ),
    (
        r"SPY0373",
        "Event type must be a delegate type. Use `event name: DelegateType` where "
        "DelegateType is declared with `delegate`. Do not use function types directly.",
    ),
    (
        r"SPY0374",
        "Events can only be raised from inside the declaring class. "
        "Move the `.invoke()` call inside a method of the class that owns the event.",
    ),
    (
        r"SPY0375|SPY0376",
        "Event subscribe (`+=`) and unsubscribe (`-=`) require a compatible handler. "
        "Ensure the handler signature matches the event's delegate type.",
    ),
    (
        r"SPY0420",
        "Function-style events require BOTH `event add` and `event remove` accessors. "
        "Add the missing accessor.",
    ),
    (
        r"SPY0135|SPY0136",
        "Event declaration syntax error. Use `event name: DelegateType` for auto-events, "
        "or `event add name(self, handler: DelegateType):` / "
        "`event remove name(self, handler: DelegateType):` for function-style events.",
    ),
    (
        r"does not contain a constructor that takes",
        "Structs without an explicit `__init__` only have a parameterless constructor. "
        "Add an `__init__` method to the struct with the desired parameters: "
        "`def __init__(self, x: int, y: int): self.x = x; self.y = y`.",
    ),
    (
        r"is not valid for this item",
        "Some decorators are not valid on struct members. "
        "`@virtual` and `@override` cannot be used on struct methods because structs are sealed value types. "
        "Remove these decorators from struct methods.",
    ),
    (
        r"SPY0907.*FunctionCall",
        "An internal compiler error occurred during function call resolution. "
        "Try simplifying the code to avoid complex type inference.",
    ),
    (
        r"SPY0237.*map",
        "The compiler cannot infer map()'s type parameters. Use explicit syntax: "
        "map[InputType, OutputType](lambda x: ..., items). For example: "
        "map[int, str](lambda x: str(x), numbers).",
    ),
    (
        r"SPY0101.*event",
        "'event' is a reserved keyword in Sharpy (used for event declarations). "
        "Use a different identifier name like 'evt', 'e', or 'event_data'.",
    ),
    (
        r"CS1721",
        "Sharpy only supports single inheritance. Use one base class and interfaces "
        "for additional behavior. Change `class C(A, B):` to `class C(A, IB):` "
        "where `IB` is an interface.",
    ),
    (
        r"CS1729.*Exception",
        "Custom exception with `pass` body has no constructor accepting arguments. "
        "Add `def __init__(self, message: str): super().__init__(message)` to accept a message.",
    ),
    (
        r"SPY0203",
        "Type 'X' has no member 'Y' — the member does not exist on the type. "
        "Either: (1) define the missing method/property in the class definition, "
        "(2) check for typos in the member name, or (3) use a different member that exists.",
    ),
    (
        r"CS0246.*Staticmethod",
        "Use `@static` not `@staticmethod`. Sharpy uses `@static` for static methods.",
    ),
]


def _get_remediation_hint(validation_error: str) -> str:
    """Match validation_error against known patterns and return remediation hints."""
    hints = []
    for pattern, hint in RETRY_REMEDIATION:
        if re.search(pattern, validation_error, re.IGNORECASE):
            hints.append(hint)

    # Enrich SPY0203 hints with extracted type/member names
    spy0203_match = re.search(
        r"SPY0203.*?[Tt]ype '([^']+)' has no member '([^']+)'", validation_error
    )
    if spy0203_match:
        type_name = spy0203_match.group(1)
        member_name = spy0203_match.group(2)
        hints.append(
            f"Specifically: type '{type_name}' does not have member '{member_name}'. "
            f"Check that '{member_name}' is defined in the '{type_name}' class, "
            f"or use a member that '{type_name}' actually provides."
        )

    if not hints:
        return ""
    return "\n\n## Remediation Hints\n\n" + "\n".join(f"- {h}" for h in hints) + "\n"


# =============================================================================
# Shared prompt sections — referenced by all 4 generation/regeneration prompts.
# Keep in sync: any update here propagates to all templates automatically.
# =============================================================================

BEHAVIORAL_RULES_SECTION = """\
### ⚠️ CRITICAL BEHAVIORAL RULES — Common pitfalls:
- CRITICAL: **Abstract class decorator**: When using `@abstract` methods, the containing class MUST also be decorated with `@abstract`. Both decorators are required:
  ```
  @abstract
  class Shape:
      @abstract
      def area(self) -> float: ...
  ```
  When overriding abstract methods in subclasses, ALWAYS use `@override`:
  ```
  class Circle(Shape):
      @override
      def area(self) -> float:
          return 3.14159 * self.radius ** 2
  ```
- CRITICAL: **Single class inheritance only**: Sharpy supports single class inheritance only. `class C(A, B):` is INVALID if both A and B are classes. For multiple behaviors, use interfaces: `class C(A, IB):` where `IB` is an interface.
- **Interface vs override**: When implementing interface methods, do NOT use `@override`. `@override` is ONLY for overriding `@virtual` or `@abstract` methods from base classes.
- **Struct constructors**: Structs require an explicit `__init__` to accept constructor arguments. There is no auto-generated positional constructor.
- **String char type**: String indexing `s[i]` and iteration `for c in s` yield C# `char`, not `str`. You MUST wrap with `str()` before comparing or assigning: `str(s[i]) == "a"` (NOT `s[i] == "a"`), `c: str = str(s[i])` (NOT `c: str = s[i]`). Also use `str(c)` in for-each loops over strings.
- **Float division by zero**: Float division by zero produces `Infinity` (not an exception). Only integer division by zero raises `ZeroDivisionError`.
- **Self prefix**: Always use `self.field_name` to access instance fields inside methods.
- **Try-block scoping**: Variables declared inside `try`/`except`/`finally` blocks are block-scoped — they are NOT visible outside those blocks. Declare variables before the `try` if they need to be used in `except`/`else`/`finally` or after the `try`.
- **Float output**: When printing float values, the output ALWAYS includes a decimal point (e.g., `print(100.0)` outputs `100.0`, NOT `100`). In EXPECTED OUTPUT comments, always write `100.0` for float values, never `100`. This matches Python behavior.
- **Set iteration order**: Set iteration order is NOT deterministic. Do NOT rely on set iteration order in expected output. Sort first if deterministic output is needed: `sorted(my_set)`.
- **Properties are not method calls**: Access properties WITHOUT parentheses: `obj.area` (NOT `obj.area()`). Properties look like field access.
- **Property backing fields**: Function-style properties need a separate backing field (e.g., `_name: str`). Auto-properties generate this automatically.
- **Spread type safety**: All elements in a spread must be compatible types: `[*int_list, *str_list]` is an error unless the target type is `list[object]`.
- **Spread requires variadic**: `func(*args)` only works if the function accepts `*args` variadic parameter, or if spreading a tuple that matches the exact parameter count.
- **With statement CLR limitation**: The `with` statement works with `IDisposable` types, but CLR type discovery currently cannot resolve inherited methods. Avoid using `StringWriter.WriteLine()` (inherited from `TextWriter`). Stick to types whose methods are defined directly on the type, not inherited.
- **Named tuple field names**: All fields must be named, or none. Cannot mix named and unnamed fields in a named tuple type definition.
- **Match exhaustiveness**: Match statements on enums, bools, or tagged unions must cover all possible values/cases OR include a `case _:` wildcard. Match expressions ALWAYS require exhaustive coverage.
- **Instance fields not static by default**: Class fields are INSTANCE fields by default. To make a class-level static field, use the `@static` decorator: `@static` then `FIELD_NAME: type = value`. Access via `ClassName.FIELD_NAME`.
- **Struct inheritance forbidden**: Classes CANNOT inherit from structs. Structs are sealed value types.
- **Generic invariance**: Generic collections are INVARIANT. `list[Child]` cannot be assigned to `list[Parent]`. Declare the collection with the base/interface type. Use `out`/`in` variance only on interfaces and delegates.
- **Optional usage**: `Some()` and `None()` can only be used where the target type is `T?` (Optional). Cannot pass `None()` where a non-optional type is expected.
- **Virtual/override required for polymorphism**: Polymorphic dispatch requires `@virtual` on the base class method AND `@override` on each subclass method. Without these decorators, the base class method is called even when the object is a subclass instance.
- **Custom exception constructors**: Custom exception classes with `pass` body have NO message constructor. To accept messages, define `__init__(self, message: str): super().__init__(message)`.
- **Custom exception hierarchy**: Classes used with `raise` or `except` MUST extend `Exception` (or a builtin exception type like `ValueError`, `RuntimeError`). A plain class cannot be raised or caught.
- **`__eq__` parameter type**: `__eq__` parameter must be typed as `object`: `def __eq__(self, other: object) -> bool:` — NOT `def __eq__(self, other: MyClass) -> bool:`.
- **Private field access**: Private fields (prefixed with `_`) cannot be accessed from outside the class — use properties to expose them.
- **No type annotations on self.field**: In `__init__`, write `self.name = name`, NOT `self.name: str = name`. Type annotations on `self.field` assignments are forbidden (SPY0107).
- **Enum integer value**: Use `.value` to get the integer value of an enum member: `e.value`, NOT `int(e)`.
- **Enum name**: Use `.name` to get the PascalCase string name of an enum member: `color.name`.
- **Enum iteration**: `for member in EnumType:` iterates over all members.
- **Only import what you define**: Only import symbols you actually define in your source files. Do NOT import symbols that don't exist in the module.
- **Type narrowing and .unwrap()**: After `if x is not None:`, `x` is automatically narrowed to its unwrapped type. Do NOT call `.unwrap()` after narrowing — just use `x` directly. `.unwrap()` is only valid on `T?` before narrowing.
- **Char vs str in iteration**: `for c in some_string:` yields `char` values, not `str`. `char` has different methods than `str` — no `.lower()`, `.upper()`, etc. Use `str(c)` to convert a char back to a string if needed.
- **Static fields**: Use `@static` decorator on class-level fields that should be shared across instances. Use `const` for compile-time constants (integers, floats, strings, bools). Access static/const fields via `ClassName.FIELD_NAME`.
- **Tuples are fixed-size**: `tuple[int, int, int]` always has exactly 3 elements. Star unpacking (`*mid`) only works with `list`, not `tuple`. Access tuple elements by index: `t[0]`, `t[1]`, etc.
- **Union construction**: Tagged union cases are constructed via `UnionName.CaseName(args)`, NOT `CaseName(args)` directly.
- **Union matching**: When matching union cases, use the full form `case UnionName.CaseName(fields):` or the short form `case CaseName(fields):` when the match subject type is known.
- **Async context**: `await` can only appear inside `async def` functions. Using `await` in a regular function is a compile error (SPY0274).
- **Positional-only enforcement**: Arguments before `/` must be passed by position — using keyword names for them is a compile error (SPY0370).
- **Keyword-only enforcement**: Arguments after `*` must be passed by keyword — passing them positionally is a compile error (SPY0371).
- **Partial application placeholder**: `_` in a function call creates a partial application (lambda). Don't confuse with `case _:` wildcard in match statements.
- **Variance position rules**: `out T` can only appear in return types, `in T` can only appear in parameter types. Violation causes SPY0418/SPY0419.
- **Delegate vs function type**: Named delegates (`delegate Pred(x: int) -> bool`) are interchangeable with function types (`(int) -> bool`). Use delegates for public API readability.
- **Event raise location**: Events can ONLY be raised from inside the declaring class. Use `self.on_click?.invoke()` — the `?.invoke()` pattern is thread-safe. Outside code can only `+=` (subscribe) and `-=` (unsubscribe).
- **Event delegate type required**: Events must use a delegate type: `event on_click: EventHandler`, NOT a function type `event on_click: () -> None`.
- **Function-style event accessors**: Function-style events have `event add name(self, handler: DelegateType):` and `event remove name(self, handler: DelegateType):` — both are required if using function-style.
- **Custom decorator arguments**: Only compile-time constants allowed as decorator arguments: strings, ints, floats, bools, `None`, enum member access (`Color.RED`), and `type(X)`. Variable references or function calls are rejected (SPY0425).
- **Built-in decorator no-args**: `@abstract`, `@virtual`, `@override`, `@static`, `@final`, `@private`, `@protected`, `@internal` never take arguments. Adding parentheses with arguments to these is an error (SPY0322).
- **Decorator name mangling**: Custom decorator names are mangled from snake_case to PascalCase: `@my_custom_attr` → `[MyCustomAttr]`. Dotted names like `@system.serializable` → `[System.Serializable]`.
- **Struct constructors**: Structs without an explicit `__init__` only have a parameterless constructor. To accept arguments, define `__init__` explicitly:
  ```
  struct Point:
      x: float
      y: float
      def __init__(self, x: float, y: float):
          self.x = x
          self.y = y
  ```
- **Method overloading**: Sharpy supports method overloading (multiple methods with the same name, different signatures), including on virtual and override methods.
- **Enum cross-module caution**: When accessing `.name` or `.value` on enum members imported from another module, ensure the enum is properly imported and the member access matches the PascalCase-mangled name. If unsure, prefer `str(member)` over `.name` and avoid `.value` for cross-module enums.
- **Expected output accuracy**: Double-check ALL arithmetic in expected output. Trace each computation step-by-step. Common mistakes: area/perimeter formulas, order of operations, integer vs float division, off-by-one in loops. When in doubt, keep computations simple.
- **`map()` type inference**: The compiler may not infer `map()`'s output type parameter. If you get SPY0237, use explicit type arguments: `map[int, str](lambda x: str(x), items)`."""

ENTRY_POINT_SECTION = """\
## CRITICAL: Program Entry Point Requirement

Every executable Sharpy program MUST have a `main()` function as its entry point:
- All executable statements (print, variable assignments, function calls) must be inside `main()`
- Only declarations (classes, functions, type aliases, static fields with type annotations) can be at module level
- Module-level variables require explicit type annotations: `counter: int = 0`
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime
- Example of WRONG: `def main(): ... \\n main()` - the `main()` call is forbidden at module level"""

ALLOWED_FEATURES_SECTION = """\
## CRITICAL: Allowed Features (Phases 0.1.0-0.2.6)

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
- **`__iter__(self)`** / **`__next__(self)`**: Iterator protocol, enables `for item in obj:`. Prefer `yield` inside `__iter__` over manual `__next__`.
- **`__reversed__(self)`**: Reverse iteration, enables `reversed(obj)`. Use `yield` to produce values in reverse order.
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
- **Set spreading**: `combined: set[int] = {*set1, *set2}` — merge sets
- **Dict spreading**: `merged: dict[str, int] = {**defaults, **overrides}` — merge dicts (later values override)
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
- **Tuple patterns**: `case (0, 0):`, `case (x, y):`
- **Or patterns**: `case "a" | "b":` — match multiple values
- **Match expression**: `result = match value: case 1: "one" case _: "other"` — produces a value
- **Exhaustiveness**: Match on enums must cover all values or have `_` wildcard
- **IMPORTANT**: When matching with `case _:`, this is the catch-all default case

#### Context Managers (With Statement) — RESTRICTED
- **With statement**: `with expr as name:` for automatic resource cleanup
- **⚠️ LIMITATION**: CLR type method resolution currently does not resolve inherited methods. Avoid `StringWriter`, `StreamReader`, and other types whose useful methods come from a base class.
- **Requires IDisposable**: The object must implement `System.IDisposable`
- **IMPORTANT**: Only works with .NET types implementing `IDisposable`. Not a general Python-style context manager.

#### Comparison Chaining
- **Chained comparisons**: `a < b < c` equivalent to `a < b and b < c`
- **Range checks**: `1 <= value <= 100`
- **Mixed operators**: `a < b <= c` — different comparison operators can be mixed
- **Single evaluation**: `a < f() < c` evaluates `f()` only once

#### F-Strings (0.1.11)
- **F-string interpolation**: `f"Hello {name}"`, `f"Result: {x + y}"`
- **Format specifiers**: `f"{value:.2f}"`, `f"{num:05d}"`

#### Collections (0.1.11)
- **List literals**: `nums: list[int] = [1, 2, 3]`
- **Dict literals**: `scores: dict[str, int] = {"alice": 100, "bob": 85}`
- **Set literals**: `unique: set[int] = {1, 2, 3}`
- **List comprehensions**: `[x * 2 for x in range(10)]`
- **Dict comprehensions**: `{k: v * 2 for k, v in items.items()}`
- **Set comprehensions**: `{x for x in items if x > 0}`
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
- **Custom exceptions**: `class MyError(Exception): pass` (no message constructor). To accept messages: `class MyError(Exception):` with `def __init__(self, message: str): super().__init__(message)`

#### Lambda Expressions (0.1.14)
- **Lambdas**: `lambda x: x * 2`, `lambda a, b: a + b`
- **Higher-order functions**: Passing lambdas to functions that have typed parameters
- **Type inference**: Lambda parameter types are inferred from the expected function type context
- **IMPORTANT**: The receiving function MUST declare its parameter with a function type: `def apply(fn: (int) -> int) -> int:`
- **WARNING**: Lambdas CANNOT be assigned to `auto` variables — there is no type context to infer parameter types. Use an explicit function type: `square: (int) -> int = lambda n: n * n`, NOT `square: auto = lambda n: n * n`.

#### Generators & Yield
- **Generator function**: Any function containing `yield` becomes a generator — it returns `IEnumerable<T>` and can be iterated with `for x in gen():`
- **Yield statement**: `yield value` produces a value to the caller, suspending the generator
- **Yield from**: `yield from other_generator()` delegates to another generator, yielding all its values
- **Generator return type**: Annotate with the ELEMENT type, not the collection type: `def count() -> int:` (not `-> IEnumerable<int>`)
- **Early return in generators**: `return` (without a value) terminates the generator early. `return value` in a generator is FORBIDDEN.
- **Generator __iter__**: Use `yield` inside `__iter__(self)` to make a class iterable: `def __iter__(self) -> int: yield 1; yield 2`
- **Generator __reversed__**: Use `yield` inside `__reversed__(self)` for reverse iteration: `def __reversed__(self) -> int: ...`
- **IMPORTANT**: `yield` CANNOT appear inside `__next__()` — it is only allowed in regular functions, `__iter__`, and `__reversed__`
- **IMPORTANT**: A class CANNOT define both generator `__iter__` (with yield) AND `__next__` — these are mutually exclusive approaches

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
- **Usage**: Converts exception-throwing code into Result-based error handling

### ✅ ALLOWED — Phase 0.2.0+ Features (recently implemented)

#### Constructor Chaining (0.2.0)
- **`self.__init__()`**: Chain to another constructor in the same class: `self.__init__(default_value)` inside an overloaded `__init__`
- **`super().__init__()`**: Call parent constructor (already supported, now also chainable with `self.__init__`)
- **IMPORTANT**: Constructor chaining translates to `: this(...)` in C#

#### Enum Enhancements (0.2.0)
- **Enum `.name`**: Get the string name of an enum member: `color.name` → `"Red"`
- **Enum `.value`**: Get the integer value of an enum member: `color.value` → `1`
- **Enum iteration**: `for c in Color:` iterates over all enum members
- **IMPORTANT**: `.name` returns PascalCase (e.g., `"Red"` not `"RED"`)

#### Generic Type Aliases (0.2.0)
- **Generic alias**: `type Callback[T] = (T) -> None` — parameterized type aliases
- **Usage**: `handler: Callback[int] = lambda x: print(x)`

#### Method Overloading (0.2.0)
- **Overloaded methods**: Multiple methods with the same name but different parameter types/counts
- **Resolution**: Compiler selects the best match at compile time based on argument types
- **IMPORTANT**: Overloaded methods must differ in parameter count or types, not just return type

#### Advanced Pattern Matching (0.2.2)
- **Match expression**: `result = match value: case 1: "one" case _: "other"` — produces a value
- **Or-patterns**: `case "a" | "b":` — match multiple values in one arm
- **Type patterns with binding**: `case int() as n:` — match a type and bind the value
- **Relational patterns**: `case > 0:`, `case >= 10:` — compare against values
- **Property patterns**: `case Point(x=0):` — match on property values
- **Positional patterns**: `case Point(0, y):` — match by position with `Deconstruct`
- **Guard clauses**: `case int() as n if n > 0:` — additional conditions on match arms
- **Exhaustiveness**: Match on enums/bools must cover all values or have `case _:`; match expressions on non-finite types require a wildcard arm

#### Tagged Unions (0.2.2)
- **Union declaration**: `union Shape:` with `case Circle(radius: float)`, `case Rectangle(width: float, height: float)`
- **Union construction**: `s: Shape = Shape.Circle(5.0)` or `s = Shape.Rectangle(2.0, 3.0)`
- **Union matching**: `match shape: case Shape.Circle(r): ... case Shape.Rectangle(w, h): ...`
- **Generic unions**: `union Option[T]:` with `case Some(value: T)`, `case None_()`
- **Unit cases**: `case None_()` — cases with no fields
- **Exhaustiveness**: Match on unions must cover all cases or have `case _:`
- **IMPORTANT**: Union cases are constructed via `UnionName.CaseName(args)`

#### Async/Await (0.2.4)
- **Async functions**: `async def fetch_data() -> str:` — returns `Task<T>` under the hood
- **Await expressions**: `result = await fetch_data()` — suspends until task completes
- **Async for**: `async for item in async_iter:` — iterate over `IAsyncEnumerable<T>`
- **Async with**: `async with resource as r:` — async resource management
- **Async generators**: `async def` + `yield` → `IAsyncEnumerable<T>` return type
- **asyncio.gather**: `results = await asyncio.gather(task1, task2)` → `Task.WhenAll`
- **asyncio.sleep**: `await asyncio.sleep(1.0)` → `Task.Delay`
- **IMPORTANT**: `await` can only be used inside `async def` functions
- **IMPORTANT**: Import asyncio with `import asyncio` before using `asyncio.gather` or `asyncio.sleep`

#### Positional-Only & Keyword-Only Parameters (0.2.5)
- **Positional-only (`/`)**: `def foo(x: int, y: int, /, z: int):` — `x` and `y` can only be passed positionally
- **Keyword-only (`*`)**: `def bar(a: int, *, b: int, c: int):` — `b` and `c` must be passed as keywords
- **Combined**: `def baz(a: int, /, b: int, *, c: int):` — `a` positional-only, `c` keyword-only, `b` either

#### Partial Application (0.2.5)
- **Placeholder `_` in calls**: `add_five = add(_, 5)` — creates a lambda `lambda x: add(x, 5)`
- **Operator sections**: `double = (_ * 2)`, `is_positive = (_ > 0)`, `negate = (-_)` — shorthand for lambdas
- **IMPORTANT**: `_` in function arguments is the partial application placeholder, not a discard
- **IMPORTANT**: Operator sections require parentheses: `(_ * 2)` not `_ * 2`

#### Delegate Type Declarations (0.2.6)
- **Delegate definition**: `delegate Predicate(value: int) -> bool` — named function type
- **Usage**: `p: Predicate = lambda x: x > 0` or as parameter type `def filter(pred: Predicate):`
- **Generic delegates**: `delegate Transform[T](value: T) -> T`

#### Generic Variance (0.2.6)
- **Covariant (`out`)**: `interface IProducer[out T]:` — `T` only in output positions
- **Contravariant (`in`)**: `interface IConsumer[in T]:` — `T` only in input positions
- **On delegates**: `delegate Func[in T, out R](value: T) -> R`
- **IMPORTANT**: Variance annotations only valid on interfaces and delegates, not classes/structs

#### Events (0.2.6)
- **Auto-event**: `event on_click: EventHandler` — compiler-generated backing field and accessors
- **Auto-event with generic args**: `event on_data: EventHandler[DataEventArgs]` — typed event args
- **Custom delegate event**: `event on_change: MyDelegate` — use any delegate type
- **Function-style event**: Custom `add`/`remove` accessors for fine-grained control:
  ```
  event add on_click(self, handler: EventHandler):
      self._handlers.append(handler)
  event remove on_click(self, handler: EventHandler):
      self._handlers.remove(handler)
  ```
- **Subscribe**: `obj.on_click += handler` or `obj.on_click += lambda: print("clicked")`
- **Unsubscribe**: `obj.on_click -= handler`
- **Raise (inside class only)**: `self.on_click?.invoke()` or `self.on_click?.invoke(self, args)` — thread-safe null-check
- **Event decorators**: `@virtual`, `@override`, `@abstract`, `@static`, `@private`, `@protected` work on events
- **Interface events**: Interfaces can declare `event on_click: EventHandler` requirements
- **IMPORTANT**: Events can ONLY be raised from inside the declaring class. Outside code can only `+=` and `-=`.
- **IMPORTANT**: Use `?.invoke()` for thread-safe raising — do NOT call the event directly

#### Custom Decorators / .NET Attributes (0.2.6)
- **Simple attribute**: `@obsolete("Use bar() instead")` — maps to C# `[Obsolete("Use bar() instead")]`
- **Dotted name**: `@system.serializable` — maps to C# `[System.Serializable]`
- **Keyword arguments**: `@dll_import("user32.dll", entry_point="MessageBox")` — maps to named attribute arguments
- **Type argument**: `@some_attr(type(MyClass))` — `type(X)` maps to C# `typeof(X)`
- **Decorator names** undergo snake_case → PascalCase mangling (e.g., `@my_custom_attr` → `[MyCustomAttr]`)
- **IMPORTANT**: Arguments must be compile-time constants: string, int, float, bool, None, enum access, `type(X)`. No variables or function calls.
- **IMPORTANT**: Built-in decorators (`@abstract`, `@virtual`, `@override`, `@static`, `@final`, `@private`, `@protected`, `@internal`) must NOT have arguments (SPY0322)"""

FORBIDDEN_FEATURES_SECTION = """\
### ❌ FORBIDDEN - Do NOT use these features (not yet implemented or restricted):
- **NO main() call at module level**: Do NOT write `main()` after defining it - it's auto-invoked by runtime
- **NO multi-argument print**: `print(a, b, c)` - use multiple `print()` calls
- **NO isinstance with tuples**: `isinstance(x, (int, str))` - use `or` instead
- **NO @interface decorator**: `interface` is a keyword, use `interface IName:` syntax
- **NO combining @abstract and @virtual**: abstract methods are inherently virtual in .NET — use only `@abstract`
- **NO bare string indexing in comparisons/assignments**: `s[i] == "a"` or `c: str = s[i]` fails — always wrap with `str()`: `str(s[i]) == "a"`, `c: str = str(s[i])`
- **NO 'in' operator on strings**: `char in "abc"` — not yet supported
- **NO bare char iteration**: `for c in s:` yields `char` — use `str(c)` before comparing or assigning to `str`
- **NO `__repr__()` method**: removed — only `__str__()` exists (maps to `.ToString()`)
- **NO `del` statement**: `del x` — not supported
- **NO `**kwargs` spreading in function calls**: `func(**kwargs)` — not yet supported for keyword argument spreading
- **NO spread in non-variadic function calls**: `func(*args)` only works when the function has `*args` parameter or when spreading a tuple that matches exact parameter count
- **NO `yield` inside `__next__`**: `yield` is only allowed in regular functions, `__iter__`, and `__reversed__`
- **NO `return value` in generators**: Generators cannot return a value — use bare `return` for early termination
- **NO mixing generator `__iter__` with `__next__`**: A class cannot have both `yield`-based `__iter__` AND a `__next__` method
- **NO direct dunder calls**: Use builtin functions instead — `reversed(obj)` not `obj.__reversed__()`, `len(obj)` not `obj.__len__()`, `str(obj)` not `obj.__str__()`
- **NO `raise X from Y`**: Inner exception chaining not supported
- **NO event raise from outside class**: Events can ONLY be raised inside the declaring class via `?.invoke()`. Outside code can only use `+=` (subscribe) and `-=` (unsubscribe).
- **NO direct event assignment**: Do NOT write `obj.on_click = handler`. Use `obj.on_click += handler` to subscribe.
- **NO non-constant decorator arguments**: Custom decorator arguments must be compile-time constants (string, int, float, bool, None, enum access, `type(X)`). Variable expressions are rejected (SPY0425).
- **NO arguments on built-in decorators**: `@abstract(something)` or `@virtual(args)` is forbidden (SPY0322). Built-in decorators like `@abstract`, `@virtual`, `@override`, `@static`, `@final`, `@private`, `@protected`, `@internal` take NO arguments.
- **NO `hex()`, `oct()`, `bin()` builtins**: These conversion functions are not available. Use string formatting instead.
- **NO `sum()` builtin**: `sum()` is not available. Use a loop or `reduce()` to sum values.
- **NO `@virtual` or `@override` on struct methods**: Structs are sealed value types — their methods cannot be virtual or overridden.
- **NEVER define `@abstract` methods in a non-`@abstract` class**: The class MUST have `@abstract` decorator if any method has `@abstract`.
- **NO multiple class inheritance**: `class C(A, B):` where both A and B are classes is invalid. Use single class + interfaces for additional behavior.
- **NO `@staticmethod` decorator**: Use `@static` instead — Sharpy uses `@static` for static methods.
- **NO list comprehensions**: `[x for x in ...]` is not supported. Use a loop with `.append()` instead."""

NAMING_RULES_SECTION = """\
### ⚠️ CRITICAL NAMING RULES - Avoid builtin conflicts:
- **NEVER name functions or variables**: `double`, `int`, `str`, `float`, `bool`, `len`, `print`, `range`, `abs`, `min`, `max`, `sum`, `round`, `input`, `type`, `list`, `dict`, `set`, `tuple`, `map`, `filter`, `zip`, `any`, `all`, `sorted`, `reversed`, `enumerate`, `chr`, `ord`, `hex`, `bin`, `oct`, `hash`, `id`, `open`, `file`, `exit`, `quit`, `long`, `float32`, `pow`, `divmod`, `isinstance`, `repr`, `iter`, `next`
- Use **descriptive names** like `double_value`, `multiply_by_two`, `calculate_double`, `doubled` instead
- Names like `double` conflict with the `double` type (float64) and will cause type errors
- **Reserved interfaces**: Do NOT define interfaces named `ISized` or `IBoolConvertible` — these are compiler-reserved protocol interfaces.
- **Static decorator**: `@static` is the correct decorator for static methods, NOT `@staticmethod`.
- **Module naming**: Do NOT name modules after Python standard library modules (`types`, `sys`, `os`, `math`, `collections`)."""

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
    case 1 | 2 | 3:
        print("small positive")
    case _:
        print("other")

# Named tuple
type Point = tuple[x: float, y: float]
p: Point = (x=1.0, y=2.0)
print(p.x)

# Comparison chaining
if 0 < x < 10:
    print("in range")

# Generator function with yield
def count_up(n: int) -> int:
    i = 0
    while i < n:
        yield i
        i += 1

for x in count_up(3):
    print(x)  # 0, 1, 2

# Yield from delegation
def inner() -> int:
    yield 1
    yield 2

def outer() -> int:
    yield 0
    yield from inner()

# Class with generator __iter__ and __reversed__
class Range:
    start: int
    end: int
    def __init__(self, start: int, end: int):
        self.start = start
        self.end = end
    def __iter__(self) -> int:
        i = self.start
        while i < self.end:
            yield i
            i += 1
    def __reversed__(self) -> int:
        i = self.end - 1
        while i >= self.start:
            yield i
            i -= 1

# Tagged union declaration and matching
union Shape:
    case Circle(radius: float)
    case Rectangle(width: float, height: float)
    case Point()

def describe(s: Shape) -> str:
    return match s:
        case Shape.Circle(r): f"Circle r={{r}}"
        case Shape.Rectangle(w, h): f"Rect {{w}}x{{h}}"
        case Shape.Point(): "Point"

# Or-patterns and relational patterns
match score:
    case 90 | 95 | 100:
        print("excellent")
    case > 80:
        print("good")
    case _:
        print("ok")

# Type pattern with binding
match value:
    case int() as n if n > 0:
        print(f"positive int: {{n}}")
    case str() as s:
        print(f"string: {{s}}")
    case _:
        print("other")

# Async function and await
async def fetch_value() -> int:
    await asyncio.sleep(0.1)
    return 42

async def main_async() -> None:
    result = await fetch_value()
    print(result)

# Positional-only and keyword-only parameters
def create(x: int, y: int, /, *, label: str = "default") -> str:
    return f"{{label}}: ({{x}}, {{y}})"

# Partial application
def add(a: int, b: int) -> int:
    return a + b
add_five = add(_, 5)
print(add_five(3))  # 8

# Operator section
nums: list[int] = [1, -2, 3, -4]
positives = filter((_ > 0), nums)

# Delegate type
delegate Predicate(value: int) -> bool
def count_matching(items: list[int], pred: Predicate) -> int:
    result = 0
    for item in items:
        if pred(item):
            result += 1
    return result

# Generic variance
interface IProducer[out T]:
    def produce(self) -> T: ...

interface IConsumer[in T]:
    def consume(self, item: T) -> None: ...

# Enum iteration and .name/.value
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

for c in Color:
    print(f"{{c.name}} = {{c.value}}")

# Method overloading
class Printer:
    def show(self, x: int) -> None:
        print(f"int: {{x}}")
    def show(self, x: str) -> None:
        print(f"str: {{x}}")

# Events — auto-event with subscribe and raise
delegate EventHandler() -> None

class Button:
    event on_click: EventHandler

    def click(self) -> None:
        self.on_click?.invoke()

def main():
    b = Button()
    b.on_click += lambda: print("clicked!")
    b.click()

# Custom decorators / .NET attributes
@obsolete("Use new_function instead")
def old_function() -> None:
    pass

@system.serializable
class Config:
    name: str
    def __init__(self, name: str):
        self.name = name

# Context manager with dunder protocol
class ManagedResource:
    def __enter__(self) -> ManagedResource:
        print("enter")
        return self

    def __exit__(self):
        print("exit")

with ManagedResource() as r:
    print("using")
```

## Output Format

Wrap your code in `<code>` tags and expected output in `<expected>` tags:

<code>
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
</code>
<expected>
11
</expected>

### Expected Output Verification (CRITICAL)

After writing the code and expected output:
1. **Mentally trace `main()` line by line** — show each variable's value at each line.
2. **For loops, show each iteration's state explicitly** — track the loop variable and any accumulators.
3. **Verify arithmetic** — especially modular arithmetic (%), floating-point (*), and hash computations. Verify sums, products, and averages by hand.
4. **Count carefully** — for set operations, list out all elements to count correctly.
5. **Check method dispatch** — if a method is NOT @virtual, the BASE class version runs regardless of the actual type.
6. **Verify every line of expected output matches your trace.** If it doesn't, fix the expected output.
7. **Keep programs simple enough that you can trace the output with confidence.**

IMPORTANT:
- Use ONLY simple print() calls with ONE argument: print(value)
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in `<expected>` tags
- Float values ALWAYS print with a decimal point: write `100.0` not `100`, `5.0` not `5`
- Keep the code simple and focused on testing the specified feature
- ALWAYS close your `<code>` and `<expected>` tags"""


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

## CRITICAL: Response Format

Your response MUST use this exact XML format for each file:

<code file="module_name.spy">
# code here
</code>

<code file="main.spy">
# main entry point
</code>

<expected>
expected output line 1
expected output line 2
</expected>

Do NOT use markdown code blocks. Do NOT use any other file delimiter format.

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

Wrap each file in `<code file="filename.spy">` tags and expected output in `<expected>` tags:

<code file="module_name.spy">
# Module providing utility functions

def helper_function(x: int) -> int:
    return x * 2

class UtilityClass:
    value: int

    def __init__(self, v: int):
        self.value = v
</code>
<code file="main.spy">
# Main entry point - imports from module_name
from module_name import helper_function, UtilityClass

def main():
    result: int = helper_function(5)
    print(result)

    obj = UtilityClass(10)
    print(obj.value)
</code>
<expected>
10
10
</expected>

### Expected Output Verification (CRITICAL)

After writing the code and expected output:
1. **Mentally trace `main()` line by line** — show each variable's value at each line.
2. **For loops, show each iteration's state explicitly** — track the loop variable and any accumulators.
3. **Verify arithmetic** — especially modular arithmetic (%), floating-point (*), and hash computations. Verify sums, products, and averages by hand.
4. **Count carefully** — for set operations, list out all elements to count correctly.
5. **Check method dispatch** — if a method is NOT @virtual, the BASE class version runs regardless of the actual type.
6. **Verify every line of expected output matches your trace.** If it doesn't, fix the expected output.
7. **Keep programs simple enough that you can trace the output with confidence.**

CRITICAL RULES:
1. Each file is wrapped in `<code file="filename.spy">` ... `</code>` tags
2. One file MUST be named `main.spy` - this is the entry point
3. Expected output goes in `<expected>` tags (NOT in comments)
4. Use ONLY `from module import items` syntax (NOT `import module`)
5. Module names match filenames exactly (without .spy)
6. All print() calls must be in main.spy
7. NO circular imports between modules
8. Float values ALWAYS print with a decimal point: write `100.0` not `100`, `5.0` not `5`
9. ALWAYS close your `<code>` and `<expected>` tags"""


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

<code>
{previous_code}
</code>

## Validation Error

```
{validation_error}
```
{remediation_hint}
## Instructions

1. Analyze the validation error carefully
2. Identify which feature(s) are NOT allowed in phases 0.1.0-0.2.6
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

Wrap your code in `<code>` tags and expected output in `<expected>` tags:

<code>
# Your fixed code here
...
</code>
<expected>
expected output lines
</expected>

IMPORTANT:
- Use ONLY simple print() calls with ONE argument
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in `<expected>` tags
- Float values ALWAYS print with a decimal point: write `100.0` not `100`, `5.0` not `5`
- ALWAYS close your `<code>` and `<expected>` tags"""


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

    # Format previous files with XML-like tags (same format as generation output)
    files_section = ""
    if not previous_files:
        files_section = "(no files from previous attempt)\n"
    else:
        for filename, code in previous_files.items():
            files_section += f'<code file="{filename}">\n{code}\n</code>\n'

    remediation_hint = _get_remediation_hint(validation_error)

    return f"""You are regenerating a MULTI-FILE Sharpy project for compiler testing (dogfooding).

## CRITICAL: Response Format

Your response MUST use this exact XML format for each file:

<code file="module_name.spy">
# code here
</code>

<code file="main.spy">
# main entry point
</code>

<expected>
expected output
</expected>

Do NOT use markdown code blocks. Do NOT use any other file delimiter format.

## REGENERATION ATTEMPT {attempt}/{max_attempts}

Your previous multi-file project FAILED validation. You must fix the issue and regenerate.

## Previous Files That Failed

{files_section}

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

Wrap each file in `<code file="filename.spy">` tags and expected output in `<expected>` tags:

<code file="module_name.spy">
# Fixed module code here
...
</code>
<code file="main.spy">
# Fixed main code here
...
</code>
<expected>
expected output lines
</expected>

CRITICAL RULES:
1. Each file is wrapped in `<code file="filename.spy">` ... `</code>` tags
2. One file MUST be named `main.spy` - this is the entry point
3. Expected output goes in `<expected>` tags (NOT in comments)
4. Use ONLY `from module import items` syntax (NOT `import module`)
5. Module names match filenames exactly (without .spy)
6. All print() calls must be in main.spy
7. NO circular imports between modules
8. Use ONLY simple print() calls with ONE argument
9. For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
10. Float values ALWAYS print with a decimal point: write `100.0` not `100`, `5.0` not `5`
11. ALWAYS close your `<code>` and `<expected>` tags"""


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


_CODE_INDICATORS = ["def ", "class ", "print(", "=", "import "]

_MARKDOWN_LINE_PATTERNS = [
    re.compile(r"^\*\*.*\*\*"),  # Bold markdown: **text**
    re.compile(
        r"^#{1,6}\s+[A-Z][a-z]+\s+[a-z]"
    ),  # Markdown headers: "# Some heading" (requires prose-like text, not Python comments)
    re.compile(r"^\d+\.\s"),  # Numbered lists: 1. item
]


def _strip_markdown_lines(code: str) -> str:
    """Strip lines that look like markdown prose from extracted code.

    Only strips if the remaining content still contains code indicators,
    to avoid accidentally stripping legitimate code.
    """
    lines = code.split("\n")
    cleaned = [
        line
        for line in lines
        if not any(pat.match(line.strip()) for pat in _MARKDOWN_LINE_PATTERNS)
    ]
    cleaned_text = "\n".join(cleaned).strip()
    # Only use cleaned version if it still looks like code
    if any(indicator in cleaned_text for indicator in _CODE_INDICATORS):
        return cleaned_text
    # Fall back to original if stripping removed too much
    return code


def _extract_raw_code_block(response: str) -> Optional[str]:
    """Extract raw code from a response without stripping markdown-like comments.

    This preserves all comment lines (including # EXPECTED OUTPUT blocks)
    exactly as they appear in the code.
    """
    import re

    # Try to find code block with python/sharpy markers
    pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    matches = re.findall(pattern, response, re.DOTALL)

    if matches:
        return max(matches, key=len).strip()

    # If no code block, check if the entire response looks like code
    lines = response.strip().split("\n")
    if lines and not any(line.startswith("```") for line in lines):
        code_indicators = ["def ", "print(", "= ", "if ", "for ", "while ", "#"]
        if any(indicator in response for indicator in code_indicators):
            return response.strip()

    return None


def extract_code_from_xml(response: str) -> Optional[str]:
    """Extract code from XML-style <code>...</code> tags.

    Handles single-file responses with a bare <code> tag (no file attribute).
    Returns the code content, or None if no valid <code> tag is found.
    """
    # Match <code> (no file attr) ... </code>
    pattern = r"<code\s*>\s*\n?(.*?)</code>"
    match = re.search(pattern, response, re.DOTALL)
    if match:
        return match.group(1).strip()
    return None


def extract_expected_from_xml(response: str) -> Optional[str]:
    """Extract expected output from XML-style <expected>...</expected> tags.

    Returns the expected output string (with trailing newline), or None.
    """
    pattern = r"<expected>\s*\n?(.*?)</expected>"
    match = re.search(pattern, response, re.DOTALL)
    if match:
        result = match.group(1).strip()
        return result + "\n" if result else None
    return None


def extract_multifile_from_xml(response: str) -> Optional[dict[str, str]]:
    """Extract multiple files from XML-style <code file="name.spy">...</code> tags.

    Handles common malformations: missing closing tags, extra whitespace,
    case-insensitive tag matching.

    Returns a dictionary mapping filename to code content, or None if
    no valid multi-file XML structure is found.
    """
    # Try strict extraction first: matched <code file="...">...</code> pairs
    pattern = r'<code\s+file="([a-zA-Z_][a-zA-Z0-9_]*\.spy)"\s*>\s*\n?(.*?)</code>'
    matches = re.findall(pattern, response, re.DOTALL | re.IGNORECASE)

    if not matches:
        # Fallback: handle missing closing tags by extracting content between
        # consecutive opening tags or until end of response
        open_pattern = r'<code\s+file="([a-zA-Z_][a-zA-Z0-9_]*\.spy)"\s*>\s*\n?'
        openers = list(re.finditer(open_pattern, response, re.IGNORECASE))
        if openers:
            matches = []
            for i, m in enumerate(openers):
                start = m.end()
                if i + 1 < len(openers):
                    end = openers[i + 1].start()
                else:
                    end = len(response)
                content = response[start:end]
                # Strip any trailing </code> that might be present
                content = re.sub(r"\s*</code>\s*$", "", content, flags=re.IGNORECASE)
                matches.append((m.group(1), content))

    if not matches:
        return None

    files: dict[str, str] = {}
    for filename, code in matches:
        filename = filename.lower()  # Normalize to lowercase
        code = code.strip()
        if code:
            files[filename] = code

    # Validate: must have at least 2 files and one must be main.spy
    if len(files) < 2:
        return None
    if "main.spy" not in files:
        return None

    return files


def has_unclosed_code_tags(response: str) -> bool:
    """Check if the response has unclosed <code> tags.

    Returns True if there are more opening <code> tags than closing </code> tags,
    indicating malformed XML that should trigger a retry.
    """
    open_count = len(re.findall(r"<code(?:\s[^>]*)?>", response))
    close_count = len(re.findall(r"</code>", response))
    return open_count > close_count


def extract_code_block(response: str) -> Optional[str]:
    """Extract code from a response, trying XML tags first, then markdown.

    Attempts XML-style <code>...</code> extraction first. Falls back to
    markdown code block extraction for backward compatibility.
    """
    # Try XML extraction first
    xml_code = extract_code_from_xml(response)
    if xml_code is not None:
        return xml_code

    # Fall back to markdown extraction
    raw = _extract_raw_code_block(response)
    if raw is not None:
        return _strip_markdown_lines(raw)
    return None


def extract_multifile_code(response: str) -> Optional[dict[str, str]]:
    """Extract multiple files from a response, trying multiple strategies.

    Extraction methods tried in order:
    1. XML-style <code file="name.spy">...</code> tags
    2. === FILE: name.spy === marker extraction
    3. Fenced code blocks with filename comments (```python\\n# filename.spy\\n...```)

    Args:
        response: The AI response potentially containing multiple files.

    Returns:
        Dictionary mapping filename to code content, or None if parsing fails.
        Returns None if no valid multi-file structure is found.
    """
    # Method 1: XML extraction
    xml_files = extract_multifile_from_xml(response)
    if xml_files is not None:
        return xml_files

    # Method 2: === FILE: ... === marker extraction
    marker_files = _extract_multifile_from_markers(response)
    if marker_files is not None:
        return marker_files

    # Method 3: Fenced code blocks with filename comments
    fenced_files = _extract_multifile_from_fenced_blocks(response)
    if fenced_files is not None:
        return fenced_files

    return None


def _extract_multifile_from_markers(response: str) -> Optional[dict[str, str]]:
    """Extract files using === FILE: name.spy === markers."""
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


def _extract_multifile_from_fenced_blocks(response: str) -> Optional[dict[str, str]]:
    """Extract files from fenced code blocks with filename comments.

    Matches patterns like:
        ```python
        # filename.spy
        def main():
            ...
        ```
    """
    # Match fenced code blocks
    block_pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    blocks = re.findall(block_pattern, response, re.DOTALL)

    if not blocks:
        return None

    files: dict[str, str] = {}
    # Pattern for filename comment at the start of a block
    filename_comment = re.compile(
        r"^\s*#\s*([a-zA-Z_][a-zA-Z0-9_]*\.spy)\s*\n", re.IGNORECASE
    )

    for block in blocks:
        m = filename_comment.match(block)
        if m:
            filename = m.group(1).lower()
            code = block[m.end() :].strip()
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


def extract_expected_output_from_response(response: str) -> Optional[str]:
    """Extract expected output from the raw AI response.

    Tries XML-style <expected>...</expected> tags first, then falls back to
    comment-based extraction from code blocks.
    """
    # Try XML extraction first
    xml_expected = extract_expected_from_xml(response)
    if xml_expected is not None:
        return xml_expected

    # Fall back to comment-based extraction
    raw = _extract_raw_code_block(response)
    if raw is not None:
        return extract_expected_output(raw)
    return extract_expected_output(response)
