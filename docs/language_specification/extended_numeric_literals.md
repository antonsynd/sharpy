# Extended Numeric Literals

```python
# Binary literals
binary = 0b1010        # 10 in decimal
flags = 0b1111_0000

# Hexadecimal literals
hex_value = 0xFF       # 255 in decimal
color = 0x001122

# Octal literals
permissions = 0o755    # 493 in decimal

# Scientific notation (e or E)
avogadro = 6.022e23
planck = 6.626e-34
large = 1E20
```

**Output format.** Printing a float uses Python's layout, not .NET's: lowercase `e`, an explicit
exponent sign, and at least two exponent digits (`1e+20`, `1e-05`). Writing a value as
`0.d1…dn × 10^decpt`, a `float` prints positionally when `-4 < decpt <= 16` and exponentially
otherwise — CPython's rule, so `1e16` prints as `1e+16` rather than `10000000000000000.0` (#1204).
The digits themselves are .NET's shortest round-trip form, which already matches CPython's.

`float32` uses the same renderer with its own threshold, `-4 < decpt <= 9`: CPython has no
`float32`, so there is no parity answer to copy, and 9 is the point past which positional layout
would print more digits than a single carries.

*Implementation:*
- *Binary/Hex: ✅ Native - Direct C# support (C# 7.0+)*
- *Octal: 🔄 Lowered - Converted to decimal at compile time*
- *Scientific: ✅ Native - Direct C# support*
