# Bitwise Operators

| Operator | Description |
|----------|-------------|
| `&` | Bitwise AND |
| `\|` | Bitwise OR |
| `^` | Bitwise XOR |
| `~` | Bitwise NOT |
| `<<` | Left shift |
| `>>` | Right shift |

## Operand types for `&`, `|` and `^`

The binary logical operators promote **both** operands through the same table the arithmetic
operators use — C# §12.4.7, tabulated in
[Numeric Type Promotion](arithmetic_operators.md#numeric-type-promotion). The result type is the
promoted type, not the left operand's type, and a pair the table refuses is refused here:

```python
a: uint32 = 5
b: int16 = 2
r: int64 = a | b         # 7    — both promote to int64
# w: uint32 = a | b      # ERROR (SPY0220): Cannot assign type 'int64'
#                        #   to variable of type 'uint32'

c: uint32 = 5
d: uint8 = 3
s: uint32 = c ^ d        # 6    — uint32 with a small unsigned stays uint32

e: uint64 = 5
f: int32 = 3
# t: uint64 = e & f      # ERROR (SPY0222): Type 'uint64' does not support
#                        #   operator '&' with operand of type 'int32'
u: uint64 = e & uint64(f)   # 1 — cast one operand to a common type
```

A constant operand converts to the other operand's type before promotion (§10.2.11), so a literal
mask needs no suffix:

```python
g: uint64 = 5
v: uint64 = g & 1        # 1 — the constant 1 converts to uint64
```

Shifts are the exception and are covered next.

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

Because the count never joins the promotion, a shift accepts operand pairs `&` refuses. A signed
count against a `uint64` value is fine:

```python
a: uint64 = 5
n: int32 = 1
r: uint64 = a << n       # 10 — only the left operand carries the type
# t: uint64 = a & n      # ERROR (SPY0222) — `&` promotes both, and this pair has no common type
```

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
