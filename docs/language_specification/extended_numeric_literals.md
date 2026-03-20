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

**Note:** .NET formats scientific notation output with uppercase `E` (e.g., `1E+20`), unlike Python which uses lowercase `e` (e.g., `1e+20`). This is an Axiom 1 (.NET-first) trade-off.

*Implementation:*
- *Binary/Hex: ✅ Native - Direct C# support (C# 7.0+)*
- *Octal: 🔄 Lowered - Converted to decimal at compile time*
- *Scientific: ✅ Native - Direct C# support*
