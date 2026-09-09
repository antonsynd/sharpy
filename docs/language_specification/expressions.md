# Expressions

## Primary Expressions

```python
# Literals
42                  # Integer
3.14                # Float
"hello"             # String
True                # Boolean
None                # None

# Identifiers
x
my_variable

# Parenthesized
(x + y)
(2 + 3) * 4
```

## Member Access

```python
# Standard access
obj.field
obj.method()

# Null-conditional
obj?.field
obj?.method()
```

## Index Access

```python
arr[0]              # First element
arr[-1]             # Last element
arr[i]              # Element at index i
matrix[i, j]        # Tuple index (parsed as tuple expression)
```

## Function Calls

```python
print("Hello")
calculate_total(100, 0.08)
obj.method(arg1, arg2)

# Generic instantiation
container = ListContainer[str]()
```

## Conditional Expression (Ternary)

```python
result = x if x > 0 else -x         # Absolute value
status = "even" if n % 2 == 0 else "odd"
```

The type of a conditional expression follows best-common-type (R-W):

1. **Slot-directed (arm 1):** when the conditional is stored into a typed slot (declaration, argument, return, collection element, etc.), each branch is admitted against the slot individually — a type mismatch is reported at the branch that fails:
   ```python
   a: Animal = Dog() if c else Cat()  # both branches admitted to Animal
   a: Animal = Dog() if c else "x"    # error at "x": cannot assign 'str' to 'Animal'
   ```

2. **One-accepts-all (arm 2):** without a slot, if one branch's type accepts the other, that type is the result:
   ```python
   x = Dog() if c else Animal()  # Animal (Dog is assignable to Animal)
   y = 1 if c else 2.5           # float (int literal converts to float)
   ```

3. **Refuse by name (arm 3):** if neither branch accepts the other, the compiler refuses:
   ```python
   z = Dog() if c else Cat()  # error: no best common type ('Dog', 'Cat')
   ```

A `None` branch in a slot-less conditional triggers an arm-3 refusal: `None` is untyped and has no best common type with any value. Use a typed slot instead: `x: str | None = None if c else "hello"`.

**Truthiness position:** when a conditional appears in a truthiness context (`if`, `while`, `not`, `and`, `or`, `assert`, comprehension condition, match guard), the truthiness test distributes per branch rather than requiring a common type:
```python
s = ""
n = 42
if s if flag else n:  # tests s's truthiness or n's truthiness per branch
    print("truthy")
```

*Implementation: ✅ Native - Maps to `condition ? trueVal : falseVal`. Arm-1 casts are inert under C# 9 target-typed conditionals; truthiness distributes via `TruthinessLowering.Distributed`.*

## Expression Evaluation Order

Expressions are evaluated left-to-right:

```python
# Left-to-right evaluation
result = f1() + f2() * f3()
# Order: f1(), f2(), f3(), then operators by precedence

# Short-circuit evaluation
result = cheap() and expensive()
# If cheap() is False, expensive() is never called

# Argument evaluation
func(first(), second(), third())
# Order: first(), second(), third(), then func() called
```

**Rules:**
1. Expressions evaluated left-to-right
2. Operator precedence determines grouping, not evaluation order
3. Short-circuit operators (`and`, `or`, `??`, `?.`) stop early when possible
4. Function arguments evaluated left-to-right before call
