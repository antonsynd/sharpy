# String Type and UTF-16 Semantics

Sharpy's `str` type maps directly to .NET's `System.String`, which uses UTF-16 encoding internally. This has important implications for string operations.

## UTF-16 Code Units

All string operations in Sharpy work with UTF-16 code units, not Unicode code points or grapheme clusters. This matches C# behavior exactly.

**`len()` returns UTF-16 code units:**

```python
# ASCII characters: 1 code unit each
len("hello")        # 5

# Most common characters: 1 code unit each
len("café")         # 4 (é is a single code unit U+00E9)

# Emoji and rare characters: 2 code units (surrogate pairs)
len("😀")           # 2 (U+1F600 requires surrogate pair)
len("𝄞")            # 2 (musical G clef, U+1D11E)

# Combined
len("Hi 😀!")       # 6 (H=1, i=1, space=1, 😀=2, !=1)
```

**Indexing returns UTF-16 code units:**

```python
s = "hello"
s[0]               # 'h'
s[4]               # 'o'

# With emoji
s = "Hi 😀!"
s[0]               # 'H'
s[3]               # '\uD83D' (high surrogate of 😀)
s[4]               # '\uDE00' (low surrogate of 😀)
s[5]               # '!'
```

**Slicing operates on UTF-16 code units:**

```python
s = "café"
s[0:4]             # "café"
s[0:3]             # "caf"

# Slicing through a surrogate pair can produce invalid strings
s = "A😀B"
s[0:2]             # "A\uD83D" - contains unpaired surrogate (may cause issues)
s[0:3]             # "A😀" - correct
```

## Comparison with Python

| Operation | Python 3 | Sharpy / C# |
|-----------|----------|-------------|
| `len("😀")` | 1 (code point) | 2 (UTF-16 code units) |
| `"😀"[0]` | '😀' (full character) | '\uD83D' (high surrogate) |
| Internal encoding | Flexible (Latin-1/UCS-2/UCS-4) | Always UTF-16 |
| Iteration unit | Code points | UTF-16 code units |

## Iterating Over Strings

Iterating over a string yields individual `char` values (UTF-16 code units):

```python
for c in "Hi😀":
    print(c)
# Output:
# H
# i
# � (high surrogate)
# � (low surrogate)
```

## Working with Unicode Correctly

For applications that need to work with user-perceived characters (grapheme clusters) or Unicode code points, use the appropriate .NET APIs:

```python
from system.globalization import StringInfo

# Get grapheme clusters (user-perceived characters)
text = "café"  # 'e' + combining acute accent (if composed that way)
info = StringInfo(text)
length_in_graphemes = info.length_in_text_elements

# Enumerate code points
from system.text import Rune

for rune in text.enumerate_runes():
    print(rune)
```

**Note:** A dedicated grapheme cluster module for Sharpy is planned for a future version.

## String Literals and Escapes

String literals in source code are UTF-8 encoded (per Sharpy's source file encoding), but are converted to UTF-16 `System.String` values at compile time:

```python
# All produce valid UTF-16 strings
ascii_str = "hello"
unicode_str = "héllo wörld"
emoji_str = "Hello 😀 World"
escape_str = "\u0048\u0065\u006C\u006C\u006F"  # "Hello"
```

## Implications for Sharpy Developers

1. **String length may differ from character count:** `len()` returns UTF-16 code units, which may be more than the number of visible characters for strings containing emoji or rare Unicode characters.

2. **Indexing can split surrogate pairs:** Be cautious when indexing or slicing strings that may contain characters outside the Basic Multilingual Plane (BMP).

3. **Use .NET APIs for Unicode-aware operations:** When correctness with all Unicode text is required, use `StringInfo`, `Rune`, or other .NET globalization APIs.

4. **Most common text works as expected:** ASCII text and most European/Asian scripts (within the BMP) have a 1:1 correspondence between characters and code units.

*Implementation: ✅ Native - Direct use of `System.String` with no additional abstraction.*
