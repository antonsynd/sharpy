# str

Extension methods on `string` that provide Python string method
equivalents under PascalCase names.  The emitter's NameMangler converts
`upper` to `Upper`, `lower` to `Lower`, etc.
Generated code includes `using global::Sharpy;` which brings these
extensions into scope so that `name.Upper()` compiles against C#
`string`.

## Methods

### `upper() -> str`

Return a copy of the string converted to uppercase.
Python: `str.upper()`

```python
"hello".upper()    # "HELLO"
```

!!! note
    Uses invariant culture to match Python's culture-independent behavior.

### `lower() -> str`

Return a copy of the string converted to lowercase.
Python: `str.lower()`

```python
"HELLO".lower()    # "hello"
```

!!! note
    Uses invariant culture to match Python's culture-independent behavior.

### `strip() -> str`

Return a copy of the string with leading and trailing whitespace removed.
Python: `str.strip()`

```python
"  hello  ".strip()    # "hello"
```

### `strip(chars: str) -> str`

Return a copy of the string with leading and trailing characters in
*chars* removed.
Python: `str.strip(chars)`

```python
"xxhelloxx".strip("x")    # "hello"
```

### `lstrip() -> str`

Return a copy of the string with leading whitespace removed.
Python: `str.lstrip()`

```python
"  hello".lstrip()    # "hello"
```

### `lstrip(chars: str) -> str`

Return a copy of the string with leading characters in
*chars* removed.
Python: `str.lstrip(chars)`

### `rstrip() -> str`

Return a copy of the string with trailing whitespace removed.
Python: `str.rstrip()`

```python
"hello  ".rstrip()    # "hello"
```

### `rstrip(chars: str) -> str`

Return a copy of the string with trailing characters in
*chars* removed.
Python: `str.rstrip(chars)`

### `capitalize() -> str`

Return a copy of the string with its first character capitalized
and the rest lowercased.
Python: `str.capitalize()`

```python
"hello world".capitalize()    # "Hello world"
```

### `join(iterable: Iterable[str]) -> str`

Return a string which is the concatenation of the strings in
*iterable*. The separator between elements is the
string providing this method.
Python: `str.join(iterable)`

```python
", ".join(["a", "b", "c"])    # "a, b, c"
```

### `title() -> str`

Return a titlecased version of the string where words start with
an upper case character and the remaining characters are lower case.
Python: `str.title()`

```python
"hello world".title()    # "Hello World"
```

### `swapcase() -> str`

Return a copy of the string with uppercase characters converted to
lowercase and vice versa.
Python: `str.swapcase()`

```python
"Hello World".swapcase()    # "hELLO wORLD"
```

### `center(width: int, fillchar: char = ' ') -> str`

Return centered in a string of length *width*.
Padding is done using the specified *fillchar*
(default is a space).
Python: `str.center(width, fillchar)`

```python
"hi".center(10)         # "    hi    "
"hi".center(10, "-")    # "----hi----"
```

### `ljust(width: int, fillchar: char = ' ') -> str`

Return the string left-justified in a string of length
*width*. Padding is done using the specified
*fillchar* (default is a space).
Python: `str.ljust(width, fillchar)`

### `rjust(width: int, fillchar: char = ' ') -> str`

Return the string right-justified in a string of length
*width*. Padding is done using the specified
*fillchar* (default is a space).
Python: `str.rjust(width, fillchar)`

### `zfill(width: int) -> str`

Return a copy of the string left filled with ASCII '0' digits to
make a string of length *width*. A leading sign
prefix (+/-) is handled by inserting the padding after the sign
character rather than before.
Python: `str.zfill(width)`

```python
"42".zfill(5)     # "00042"
"-42".zfill(5)    # "-0042"
```

### `removeprefix(prefix: str) -> str`

If the string starts with the *prefix* string,
return `string[len(prefix):]`. Otherwise, return a copy of
the original string.
Python: `str.removeprefix(prefix)`

```python
"HelloWorld".removeprefix("Hello")    # "World"
"HelloWorld".removeprefix("Bye")      # "HelloWorld"
```

### `removesuffix(suffix: str) -> str`

If the string ends with the *suffix* string,
return `string[:-len(suffix)]`. Otherwise, return a copy of
the original string.
Python: `str.removesuffix(suffix)`

```python
"HelloWorld".removesuffix("World")    # "Hello"
"HelloWorld".removesuffix("Bye")      # "HelloWorld"
```

### `replace(old: str, new_: str) -> str`

Return a copy with all occurrences of *old* replaced
by *new_*.
Python: `str.replace(old, new)`

```python
"hello world".replace("world", "there")    # "hello there"
```

### `replace(old: str, new_: str, count: int) -> str`

Return a copy with the first *count* occurrences of
*old* replaced by *new_*.
Python: `str.replace(old, new, count)`

### `splitlines() -> list[str]`

Return a list of the lines in the string, breaking at line
boundaries. Line breaks are not included in the resulting list.
Python: `str.splitlines()`

```python
"a\nb\nc".splitlines()    # ["a", "b", "c"]
```

!!! note
    Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
    \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).

### `splitlines(keepends: bool) -> list[str]`

Return a list of the lines in the string, breaking at line
boundaries. When *keepends* is `true`, line
break characters are included in the resulting strings.
Python: `str.splitlines(keepends)`

!!! note
    Recognizes all Python line boundaries: \n, \r\n, \r, \v (0x0B),
    \f (0x0C), \x1C, \x1D, \x1E, \x85 (NEL), \u2028 (LS), \u2029 (PS).

### `split() -> list[str]`

Split the string on whitespace. Consecutive whitespace is collapsed,
and leading/trailing whitespace is stripped.
Python: `str.split()`

```python
"a b  c".split()    # ["a", "b", "c"]
```

### `split(sep: str) -> list[str]`

Split the string on a separator string.
Python: `str.split(sep)`

```python
"a,b,c".split(",")    # ["a", "b", "c"]
```

### `split(sep: str, maxsplit: int) -> list[str]`

Split the string on a separator string, performing at most
*maxsplit* splits (from the left).
Python: `str.split(sep, maxsplit)`

**Raises:**

- `TypeError` -- Thrown if *sep* is `null`.
- `ValueError` -- Thrown if *sep* is empty.

### `rsplit() -> list[str]`

Split the string on whitespace from the right. Consecutive whitespace
is collapsed, and leading/trailing whitespace is stripped.
Python: `str.rsplit()`

### `rsplit(sep: str) -> list[str]`

Split the string on a separator string from the right.
Python: `str.rsplit(sep)`

### `rsplit(sep: str, maxsplit: int) -> list[str]`

Split the string on a separator string from the right, performing at
most *maxsplit* splits.
Python: `str.rsplit(sep, maxsplit)`

**Raises:**

- `TypeError` -- Thrown if *sep* is `null`.
- `ValueError` -- Thrown if *sep* is empty.

### `expandtabs(tabsize: int = 8) -> str`

Return a copy where all tab characters are expanded using spaces.
The column position is tracked; tab stops are at every
*tabsize* characters.
Python: `str.expandtabs(tabsize=8)`

```python
"a\tb".expandtabs(4)    # "a   b"
```

### `istitle() -> bool`

Return `true` if the string is a titlecased string and there is
at least one character. Uppercase characters may only follow uncased
characters and lowercase characters only cased characters.
Python: `str.istitle()`

```python
"Hello World".istitle()    # True
"hello world".istitle()    # False
```

### `encode(encoding: str = "utf-8") -> list[byte]`

Encode the string using the specified encoding and return as a byte array.
Python: `str.encode(encoding='utf-8')`

```python
"hello".encode()           # b'hello'  (UTF-8)
"hello".encode("ascii")    # b'hello'  (ASCII)
```

**Raises:**

- `LookupError` -- Thrown if *encoding* is not recognized.

### `maketrans(x: str, y: str) -> Dictionary[char, str]`

Build a translation table mapping characters in *x*
to corresponding characters in *y*.
Python: `str.maketrans(x, y)`

```python
t = str.maketrans("aeiou", "12345")
"apple".translate(t)    # "1ppl2"
```

**Raises:**

- `ValueError` -- Thrown if *x* and *y* have different lengths.

### `maketrans(x: str, y: str, z: str) -> Dictionary[char, str]`

Build a translation table mapping characters in *x*
to corresponding characters in *y*, and mapping
each character in *z* to deletion (empty string).
Python: `str.maketrans(x, y, z)`

### `translate(table: Dictionary[char, str]) -> str`

Return a copy of the string in which each character has been mapped
through the given translation table. Characters mapped to an empty
string are deleted.
Python: `str.translate(table)`

### `find(sub: str) -> int`

Return the lowest index in the string where substring *sub*
is found. Return -1 if *sub* is not found.
Python: `str.find(sub)`

```python
"hello".find("ll")    # 2
"hello".find("xy")    # -1
```

### `find(sub: str, start: int) -> int`

Return the lowest index in the string where substring *sub*
is found, starting the search at position *start*.
Return -1 if *sub* is not found.
Python: `str.find(sub, start)`

### `find(sub: str, start: int, end: int) -> int`

Return the lowest index in the string where substring *sub*
is found within `s[start:end]`.
Return -1 if *sub* is not found.
Python: `str.find(sub, start, end)`

### `rfind(sub: str) -> int`

Return the highest index in the string where substring *sub*
is found. Return -1 if *sub* is not found.
Python: `str.rfind(sub)`

```python
"hello hello".rfind("hello")    # 6
```

### `rfind(sub: str, start: int) -> int`

Return the highest index in the string where substring *sub*
is found, searching within `s[start:]`.
Return -1 if *sub* is not found.
Python: `str.rfind(sub, start)`

### `rfind(sub: str, start: int, end: int) -> int`

Return the highest index in the string where substring *sub*
is found within `s[start:end]`.
Return -1 if *sub* is not found.
Python: `str.rfind(sub, start, end)`

### `isdigit() -> bool`

Return `true` if all characters in the string are digits and
there is at least one character, `false` otherwise.
Python: `str.isdigit()`

```python
"123".isdigit()     # True
"12.3".isdigit()    # False
```

### `isalpha() -> bool`

Return `true` if all characters in the string are alphabetic
and there is at least one character, `false` otherwise.
Python: `str.isalpha()`

```python
"hello".isalpha()     # True
"hello1".isalpha()    # False
```

### `isalnum() -> bool`

Return `true` if all characters in the string are alphanumeric
and there is at least one character, `false` otherwise.
Python: `str.isalnum()`

```python
"abc123".isalnum()    # True
"abc 123".isalnum()   # False
```

### `isspace() -> bool`

Return `true` if all characters in the string are whitespace
and there is at least one character, `false` otherwise.
Python: `str.isspace()`

### `isupper() -> bool`

Return `true` if all cased characters in the string are
uppercase and there is at least one cased character, `false`
otherwise.
Python: `str.isupper()`

### `islower() -> bool`

Return `true` if all cased characters in the string are
lowercase and there is at least one cased character, `false`
otherwise.
Python: `str.islower()`

### `count(sub: str) -> int`

Return the number of non-overlapping occurrences of substring
*sub* in the string.
Python: `str.count(sub)`

```python
"banana".count("an")    # 2
"hello".count("x")      # 0
```

### `startswith(prefix: str) -> bool`

Return `true` if string starts with the *prefix*.
Python: `str.startswith(prefix)`

```python
"hello".startswith("he")    # True
"hello".startswith("lo")    # False
```

### `startswith(prefix: str, start: int) -> bool`

Return `true` if `s[start:]` starts with the *prefix*.
Python: `str.startswith(prefix, start)`

### `startswith(prefix: str, start: int, end: int) -> bool`

Return `true` if `s[start:end]` starts with the *prefix*.
Python: `str.startswith(prefix, start, end)`

### `endswith(suffix: str) -> bool`

Return `true` if string ends with the *suffix*.
Python: `str.endswith(suffix)`

```python
"hello".endswith("lo")    # True
"hello".endswith("he")    # False
```

### `endswith(suffix: str, start: int) -> bool`

Return `true` if `s[start:]` ends with the *suffix*.
Python: `str.endswith(suffix, start)`

### `endswith(suffix: str, start: int, end: int) -> bool`

Return `true` if `s[start:end]` ends with the *suffix*.
Python: `str.endswith(suffix, start, end)`

### `index(sub: str) -> int`

Like `Find(string, string)` but raises `ValueError`
when the substring is not found.
Python: `str.index(sub)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `index(sub: str, start: int) -> int`

Like `Find(string, string, int)` but raises `ValueError`
when the substring is not found.
Python: `str.index(sub, start)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `index(sub: str, start: int, end: int) -> int`

Like `Find(string, string, int, int)` but raises `ValueError`
when the substring is not found.
Python: `str.index(sub, start, end)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `rindex(sub: str) -> int`

Like `Rfind(string, string)` but raises `ValueError`
when the substring is not found.
Python: `str.rindex(sub)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `rindex(sub: str, start: int) -> int`

Like `Rfind(string, string, int)` but raises `ValueError`
when the substring is not found.
Python: `str.rindex(sub, start)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `rindex(sub: str, start: int, end: int) -> int`

Like `Rfind(string, string, int, int)` but raises `ValueError`
when the substring is not found.
Python: `str.rindex(sub, start, end)`

**Raises:**

- `ValueError` -- Thrown if the substring is not found.

### `casefold() -> str`

Return a casefolded copy of the string. Casefolded strings may be
used for caseless matching.
Python: `str.casefold()`

```python
"Straße".casefold()    # "strasse"
```

!!! note
    Performs full Unicode case folding matching Python behavior
    (e.g., ß → ss, ﬁ → fi).
