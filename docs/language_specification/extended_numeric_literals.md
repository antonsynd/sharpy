# Extended Numeric Literals **[v0.1.5]**

```python
# Binary literals
binary = 0b1010        # 10 in decimal
flags = 0b1111_0000

# Hexadecimal literals
hex_value = 0xFF       # 255 in decimal
color = 0x001122

# Octal literals
permissions = 0o755    # 493 in decimal

# Scientific notation
avogadro = 6.022e23
planck = 6.626e-34
```

*Implementation:*
- *Binary/Hex: ✅ Native - Direct C# support (C# 7.0+)*
- *Octal: 🔄 Lowered - Converted to decimal at compile time*
- *Scientific: ✅ Native - Direct C# support*
