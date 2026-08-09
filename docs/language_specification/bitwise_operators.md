# Bitwise Operators

| Operator | Description |
|----------|-------------|
| `&` | Bitwise AND |
| `\|` | Bitwise OR |
| `^` | Bitwise XOR |
| `~` | Bitwise NOT |
| `<<` | Left shift |
| `>>` | Right shift |

## Shift Semantics

### Result type follows the left operand

A shift's result type is the promoted type of its **left operand alone**. The count is not part of
the promotion — it converts to `int` at emission, which is what the .NET shift operators take:

```python
print(1 << 2)      # 4    — int << int is int
print(1L << 2)     # 4    — long << int is long
print(1 << 2L)     # 4    — a long count does not make the result long
```

This differs from `&`, `|` and `^`, which promote both operands, because a shift's operands play
different roles: one is the value, the other is a count.

### Constant shifts are range-checked

A constant shift whose exact value does not fit the expression's own width is a compile error,
**SPY0348** — the same rule constant `+`, `-` and `*` follow:

```python
b: long = 1 << 62      # ERROR (SPY0348): 4611686018427387904 does not fit 'int'
c: int  = 1 << 33      # ERROR (SPY0348): 8589934592 does not fit 'int'
a: long = 1L << 62     # OK: 4611686018427387904 — a long left operand makes the shift long
```

The left operand carries the width, so `1L << 62` is the remedy — annotating the *variable* as
`long` does not help, because the shift is already `int` by then.

A constant **negative** count is a compile error, **SPY0213**. CPython raises `ValueError`; .NET
masks the count, so `1 << -1` would be `1 << 31` = `-2147483648`. Neither is what the author meant,
and a constant is visible at compile time:

```python
print(1 << -1)     # ERROR (SPY0213): shift count -1 is negative
print(256 >> -1)   # ERROR (SPY0213)
```

### A runtime count is masked (.NET semantics)

When the count is not a constant, the .NET operators apply and the count is masked to the left
operand's width — 5 bits for `int`, 6 bits for `long`. This is Axiom 1: the generated code is a
plain C# shift, and no runtime guard is inserted.

```python
n: int = 1
s: int = 40
print(n << s)      # 256 — the count masks to 40 & 31 == 8. CPython prints 1099511627776.
```

Catalogued as a deviation (`docs/deviations.yaml`, `shift-count-masked`). Use `long` when a count
may reach 32, and check the sign yourself when a count may be negative.

*Implementation*
- *✅ Native - Direct mapping.*
