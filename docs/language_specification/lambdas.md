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
