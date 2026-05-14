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

*Implementation: ✅ Native - Maps to `condition ? trueVal : falseVal`.*

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
