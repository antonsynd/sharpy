# String Type and UTF-16 Semantics

Sharpy's `str` type maps directly to `System.String` (C# `string`). Python-compatible string methods (`upper()`, `find()`, `split()`, etc.) are provided as extension methods on `string` via `Sharpy.StringExtensions`. Operations that `System.String` doesn't natively support (repetition, negative indexing, iteration as single-character strings) use static helper methods in `Sharpy.StringHelpers`.

```python
s: str = "hello"       # Type is System.String (C# string)
```

This design follows the Kotlin model — Kotlin's `String` is `java.lang.String` with extension functions — and aligns with all three Sharpy axioms:

- **Axiom 1 (.NET):** `string` is the native .NET type. Zero interop friction.
- **Axiom 2 (Python):** Extension methods provide `s.upper()`, `s.find()`, etc. — same surface as Python.
- **Axiom 3 (Type Safety):** No implicit conversions, no boxing, no overload ambiguity.

> **Historical note:** Sharpy originally used a `Sharpy.Str` readonly struct wrapper. This was removed — see [SRP-0007](../rejected_proposals/SRP-0007-str-wrapper-type.md) for rationale.

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

Iterating over a string yields single-character `str` values (one UTF-16 code unit each), via `StringHelpers.Iterate()`:

```python
for c in "Hi😀":
    print(c)
# Output:
# H
# i
# � (high surrogate)
# � (low surrogate)
```

Each iteration variable `c` is a `str` (not a `char`), matching Python's behavior where iterating a string yields single-character strings.

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

## String Method Availability

Sharpy provides Python-compatible string methods as **extension methods** on `string` in `Sharpy.StringExtensions`. The compiler's `NameMangler` converts snake_case method names to PascalCase (e.g., `upper` → `Upper`), and generated code includes `using global::Sharpy;` to bring these extensions into scope.

### Pythonic String Methods (Extension Methods)

| Sharpy Method | Extension Method | Notes |
|---------------|-----------------|-------|
| `s.upper()` | `s.Upper()` | Uppercase (invariant culture) |
| `s.lower()` | `s.Lower()` | Lowercase (invariant culture) |
| `s.strip()` | `s.Strip()` | Remove leading/trailing whitespace |
| `s.lstrip()` | `s.Lstrip()` | Remove leading whitespace |
| `s.rstrip()` | `s.Rstrip()` | Remove trailing whitespace |
| `s.startswith(prefix)` | `s.Startswith(prefix)` | Check prefix |
| `s.endswith(suffix)` | `s.Endswith(suffix)` | Check suffix |
| `s.find(sub)` | `s.Find(sub)` | Find substring (returns -1 if not found) |
| `s.rfind(sub)` | `s.Rfind(sub)` | Find last occurrence |
| `s.replace(old, new)` | `s.Replace(old, new)` | Replace all occurrences |
| `s.split()` | `s.Split()` | Split on whitespace |
| `s.split(sep)` | `s.Split(sep)` | Split on separator |
| `s.join(items)` | `s.Join(items)` | Join with separator |
| `s.count(sub)` | `s.Count(sub)` | Count occurrences |
| `s.isdigit()` | `s.Isdigit()` | Check if all digits |
| `s.isalpha()` | `s.Isalpha()` | Check if all alphabetic |
| `s.isalnum()` | `s.Isalnum()` | Check if alphanumeric |
| `s.isspace()` | `s.Isspace()` | Check if all whitespace |
| `s.casefold()` | `s.Casefold()` | Full Unicode case folding |

### .NET Methods (Direct Access)

Since `str` is `System.String`, all .NET string methods are directly available:

```python
s = "Hello, World!"

# .NET methods work directly
s.Contains("World")            # True
s.Substring(0, 5)              # "Hello"
s.PadLeft(20)                  # "       Hello, World!"
s.Insert(7, "Beautiful ")      # "Hello, Beautiful World!"
```

### Method Resolution

When both a Sharpy extension method and a .NET method could apply, the Sharpy extension method takes precedence via the compiler's name mangling:

```python
s.upper()    # Mangled to s.Upper() — calls Sharpy extension method
```

### Differences from Python

Some Python string methods have slightly different behavior due to .NET semantics:

| Operation | Python | Sharpy/.NET |
|-----------|--------|-------------|
| `"ab" * 3` | `"ababab"` | `"ababab"` (✅ same) |
| `s.split()` | Splits on any whitespace | Splits on whitespace (✅ same) |
| `s.split("")` | `ValueError` | `ValueError` (✅ same) |
| `s.count(sub)` | Count non-overlapping | Count non-overlapping (✅ same) |
| `s[::2]` | Every other char | Slice syntax supported |

To split a string into individual characters in Sharpy, use:

```python
chars = list("hello")           # ['h', 'e', 'l', 'l', 'o']
# or
chars = [c for c in "hello"]    # ['h', 'e', 'l', 'l', 'o']
```

## Implications for Sharpy Developers

1. **String length may differ from character count:** `len()` returns UTF-16 code units, which may be more than the number of visible characters for strings containing emoji or rare Unicode characters.

2. **Indexing can split surrogate pairs:** Be cautious when indexing or slicing strings that may contain characters outside the Basic Multilingual Plane (BMP).

3. **Use .NET APIs for Unicode-aware operations:** When correctness with all Unicode text is required, use `StringInfo`, `Rune`, or other .NET globalization APIs.

4. **Most common text works as expected:** ASCII text and most European/Asian scripts (within the BMP) have a 1:1 correspondence between characters and code units.

*Implementation*
- *✅ `str` maps to `System.String`; Python methods via `Sharpy.StringExtensions`; operators/indexing/iteration via `Sharpy.StringHelpers`.*
