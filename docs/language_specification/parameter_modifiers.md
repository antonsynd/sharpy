# Parameter Modifiers (`ref`, `out`, `in`)

> **Implementation status:** ✅ Implemented — `ParameterModifier` enum with `Ref`, `Out`, `In` values; parser, semantic, and codegen support including call-site argument modifiers.

Sharpy supports pass-by-reference semantics for function parameters using space-separated modifier syntax. These modifiers enable direct mutation of caller variables and efficient passing of large value types.

## Syntax

<!-- spec-sweep: fragment -->
```python
def function_name(param: ref T, param: out T, param: in T) -> ReturnType:
    ...
```

## Modifier Types

| Modifier | Description | Caller Must Initialize | Callee Can Read | Callee Can Write | Callee Must Assign |
|----------|-------------|------------------------|-----------------|------------------|---------------------|
| `ref T` | Read/write reference | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| `out T` | Output parameter | ❌ No | ❌ No (until assigned) | ✅ Yes | ✅ Yes |
| `in T` | Readonly reference | ✅ Yes | ✅ Yes | ❌ No | ❌ No |

## `ref T` — Read/Write Reference

The `ref` modifier passes a variable by reference, allowing the callee to both read and modify the caller's variable.

```python
def swap(a: ref int, b: ref int):
    """Swap two values in place."""
    temp = a
    a = b
    b = temp

# Usage - caller must use `ref` keyword
x = 10
y = 20
swap(ref x, ref y)
print(x, y)  # 20 10
```

**Rules:**
- Caller must pass a variable (not a literal or expression result)
- Caller must prefix the argument with `ref`
- Variable must be initialized before the call

## `out T` — Output Parameter

The `out` modifier designates an output-only parameter. The callee must assign a value before returning.

```python
def try_parse(s: str, result: out int) -> bool:
    """Try to parse a string as an integer."""
    if s.isdigit():
        result = int(s)
        return True
    result = 0  # Must assign even on failure path
    return False

# Usage - caller must use `out` keyword
value: int
if try_parse("42", out value):
    print(f"Parsed: {value}")  # Parsed: 42
```

**Rules:**
- Caller uses `out` keyword at call site
- Variable does not need to be initialized before the call
- Callee must assign before any return path (compiler enforced)
- Callee cannot read the parameter until after assignment

### Inline Declaration with `out`

Variables can be declared inline at the call site:

```python
def try_parse(s: str, result: out int) -> bool:
    result = int(s) if s.isdigit() else 0
    return s.isdigit()

# Declare and assign in one statement
if try_parse("42", out value: int):
    print(value)

# Type can be inferred
if try_parse("42", out value: auto):
    print(value)
```

## `in T` — Readonly Reference

The `in` modifier passes by reference for efficiency but prevents modification. Ideal for large structs.

```python
struct LargeData:
    matrix: list[list[float]]
    metadata: dict[str, object]

def compute_result(matrix: list[list[float]]) -> float:
    return sum(sum(row) for row in matrix)

def analyze(data: in LargeData) -> float:
    """Analyze data without copying the large struct."""
    # data.matrix = []  # ERROR: Cannot modify `in` parameter
    return compute_result(data.matrix)

def main() -> None:
    # Usage - `in` keyword optional at call site
    metadata: dict[str, object] = {}
    large = LargeData([[1.0, 2.0], [3.0]], metadata)
    result = analyze(large)       # OK: in keyword implied
    result = analyze(in large)    # OK: explicit in keyword
```

**Rules:**
- Caller may optionally use `in` keyword (for clarity)
- Callee cannot modify the parameter or its members
- No defensive copy is made (unlike regular pass-by-value for structs)

## Combining with Nullable Types

Parameter modifiers can be combined with nullable types:

```python
_cache: dict[str, int?] = {}

def try_get_value(key: str, value: out int?) -> bool:
    """Get a value that might be None."""
    if key in _cache:
        value = _cache[key]  # May be None
        return True
    value = None()
    return False
```

## Overloading with Modifiers

Functions can be overloaded based on parameter modifiers:

```python
def process(value: int) -> int:
    """Process by value."""
    return value * 2

def process(value: ref int):
    """Process in place."""
    value = value * 2
```

**Note:** `ref T` and `out T` are distinct for overloading purposes. `in T` and plain `T` are **not** distinct for overloading.

## Method Signatures in Types

When declaring function types, parameter modifiers are part of the signature:

<!-- spec-sweep: error SPY0104 -->
```python
# Function type with ref parameter
SwapFunc = (ref int, ref int) -> None

# Function type with out parameter
TryParseFunc = (str, out int) -> bool
```

## Restrictions

- Cannot use modifiers with `*args`
- Cannot use modifiers with default parameter values
- `in` parameters cannot be reassigned (even to same value)
- Lambda parameters cannot have modifiers (use regular functions)

<!-- spec-sweep: error SPY0103 -->
```python
# ❌ Invalid combinations
def foo(values: ref int = 5): ...       # ERROR: ref with default
def bar(*args: ref int): ...            # ERROR: ref with *args
func = lambda x: ref int: x * 2         # ERROR: ref in lambda
```

## C# Interop

Sharpy parameter modifiers map directly to C# parameter modifiers:

**Calling C# methods with ref/out/in:**

```python
from system import Int32

def main() -> None:
    # C# signature: bool Int32.TryParse(string s, out int result)
    if Int32.try_parse("42", out value: int):
        print(value)
```

**Sharpy methods callable from C#:**

```csharp
// C# calling Sharpy function: def swap(a: ref int, b: ref int)
int x = 10, y = 20;
SharryModule.Swap(ref x, ref y);
```

## C# Emission

<!-- spec-sweep: prelude -->
```python
# Sharpy
struct LargeStruct:
    value: float

def swap(a: ref int, b: ref int):
    temp = a
    a = b
    b = temp

def try_parse(s: str, result: out int) -> bool:
    result = 0
    return False

def calculate(data: in LargeStruct) -> float:
    return data.value
```

```csharp
// C# 9.0
public static void Swap(ref int a, ref int b)
{
    var temp = a;
    a = b;
    b = temp;
}

public static bool TryParse(string s, out int result)
{
    result = 0;
    return false;
}

public static float Calculate(in LargeStruct data)
{
    return data.Value;
}
```

**Call site emission:**

```python
# Sharpy
x = 10
y = 20
large_data = LargeStruct(1.5)
swap(ref x, ref y)
try_parse("42", out value: int)
calculate(in large_data)
```

```csharp
// C# 9.0
Swap(ref x, ref y);
TryParse("42", out int value);
Calculate(in largeData);  // or just Calculate(largeData)
```

*Implementation: ✅ Implemented*
- *`ref T` → `ref T` parameter — parsed, type-checked, emitted*
- *`out T` → `out T` parameter — parsed, type-checked, emitted*
- *`in T` → `in T` parameter — parsed, type-checked, emitted*
- *Call site `ref`/`out`/`in` keywords map directly to C#*
- *Inline `out` declaration (`out value: int`, `out value: auto`) — parsed, type-checked, emitted*

## See Also

- [Function Parameters](function_parameters.md) — Overview of all parameter types
- [Function Definition](function_definition.md) — Basic function syntax
- [Structs](structs.md) — Value types that benefit from `in` parameters
