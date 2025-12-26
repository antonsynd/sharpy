# Lambda Expressions

```python
# Single expression lambda
square = lambda x: x ** 2
add = lambda x, y: x + y

# As function argument
result = apply(10, lambda x: x ** 2)
```

## Lambda Rules

- Single expression only (no statements)
- Parameter types inferred from context
- Expression result is automatically returned

## Type Inference Requirements

Lambda parameters have no type annotations; their types must be inferable from context. Sharpy requires sufficient context to determine all lambda parameter types.

**Valid contexts (types inferable):**

```python
# 1. Assignment with explicit function type
f: (int, int) -> int = lambda x, y: x + y  # ✅ x: int, y: int

# 2. Function argument where parameter type is known
def apply(value: int, transform: (int) -> int) -> int:
    return transform(value)

apply(5, lambda x: x * 2)  # ✅ x: int from transform's type

# 3. Collection methods with known element types
items: list[int] = [1, 2, 3]
items.map(lambda x: x * 2)      # ✅ x: int from list[int]
items.filter(lambda x: x > 0)   # ✅ x: int from list[int]

# 4. Generic function with type inference from other args
def transform[T, U](items: list[T], f: (T) -> U) -> list[U]:
    return [f(item) for item in items]

transform([1, 2, 3], lambda x: str(x))  # ✅ T=int inferred from list, x: int
```

**Invalid contexts (insufficient type information):**

```python
# ❌ No type context - ERROR
g = lambda x, y: x + y
# ERROR: Cannot infer types for lambda parameters 'x' and 'y'

# ❌ Generic with no type hints - ERROR
def process[T](f: (T) -> T): ...
process(lambda x: x * 2)
# ERROR: Cannot infer type parameter T from lambda alone

# ❌ Heterogeneous operations - ERROR
h = lambda x: x.upper()  # What is x? str? bytes? custom type?
# ERROR: Cannot infer type for lambda parameter 'x'
```

**Fix by providing context:**

```python
# Add explicit type annotation
g: (int, int) -> int = lambda x, y: x + y  # ✅

# Or use where inference can succeed
numbers: list[int] = [1, 2, 3]
doubled = numbers.map(lambda x: x * 2)     # ✅ x inferred as int
```

**Rationale:** Sharpy is statically typed; all types must be known at compile time. Unlike Python where lambdas are dynamically typed, Sharpy lambdas must have determinable types for the generated C# code.

## Lambda Expression Scope

Lambdas can contain any expression, including:
- Conditional expressions: `lambda x: x if x > 0 else -x`
- Walrus operator: `lambda x: (y := x * 2, y + 1)[-1]`
- Function calls, arithmetic, member access, etc.

What lambdas cannot contain (these are statements, not expressions):
- Assignments without walrus (`x = 5`)
- Control flow blocks (`if`/`for`/`while` blocks)
- Multiple statements

```python
# Valid lambda expressions
absolute = lambda x: x if x >= 0 else -x
complex_calc = lambda a, b: (temp := a * b) + temp ** 2
method_call = lambda obj: obj.process().result

# Invalid - these require statements, not expressions
# lambda x: x = 5          # ERROR: assignment is a statement
# lambda x: if x > 0: x    # ERROR: if block is a statement
```

## Closure Semantics

Lambdas can capture variables from enclosing scopes. Following C# semantics, variables are captured **by reference**, not by value:

```python
# Captured variables are by reference
counter = 0
increment = lambda: counter + 1
counter = 10
print(increment())  # 11, not 1

# Classic loop capture gotcha (same as C#)
funcs: list[() -> int] = []
for i in range(3):
    funcs.append(lambda: i)  # All capture the same 'i'

# After loop, i is 2 (last value)
print([f() for f in funcs])  # [2, 2, 2], not [0, 1, 2]

# To capture current value, use default parameter
funcs_fixed: list[() -> int] = []
for i in range(3):
    funcs_fixed.append(lambda captured=i: captured)  # Each captures different value
print([f() for f in funcs_fixed])  # [0, 1, 2]
```

*Implementation*
- *✅ Native - Maps to `(x, y) => expr`.*
