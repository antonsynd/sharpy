# str

Python-compatible string methods as extension methods on string.

## Methods

### `upper() -> str`

Return a copy of the string converted to uppercase.
Python: `str.upper()`

### `lower() -> str`

Return a copy of the string converted to lowercase.
Python: `str.lower()`

### `capitalize() -> str`

Return a copy of the string with its first character capitalized
and the rest lowercased.
Python: `str.capitalize()`

### `title() -> str`

Return a titlecased version of the string.
Python: `str.title()`

### `swapcase() -> str`

Return a copy of the string with uppercase characters converted to
lowercase and vice versa.
Python: `str.swapcase()`

### `casefold() -> str`

Return a casefolded copy of the string.
Python: `str.casefold()`

### `strip() -> str`

Return a copy with leading and trailing whitespace removed.
Python: `str.strip()`

### `strip(chars: str) -> str`

Return a copy with leading and trailing characters in
*chars* removed.
Python: `str.strip(chars)`

### `lstrip() -> str`

Return a copy with leading whitespace removed.
Python: `str.lstrip()`

### `lstrip(chars: str) -> str`

Return a copy with leading characters in *chars* removed.
Python: `str.lstrip(chars)`

### `rstrip() -> str`

Return a copy with trailing whitespace removed.
Python: `str.rstrip()`

### `rstrip(chars: str) -> str`

Return a copy with trailing characters in *chars* removed.
Python: `str.rstrip(chars)`

### `center(width: int, fillchar: char = ' ') -> str`

Return centered in a string of length *width*.
Python: `str.center(width, fillchar)`

### `ljust(width: int, fillchar: char = ' ') -> str`

Return left-justified in a string of length *width*.
Python: `str.ljust(width, fillchar)`

### `rjust(width: int, fillchar: char = ' ') -> str`

Return right-justified in a string of length *width*.
Python: `str.rjust(width, fillchar)`

### `zfill(width: int) -> str`

Return left filled with ASCII '0' digits to make a string of length
*width*. A leading sign prefix is handled.
Python: `str.zfill(width)`

### `removeprefix(prefix: str) -> str`

If the string starts with the *prefix*, return the
string with the prefix removed. Otherwise, return a copy.
Python: `str.removeprefix(prefix)`

### `removesuffix(suffix: str) -> str`

If the string ends with the *suffix*, return the
string with the suffix removed. Otherwise, return a copy.
Python: `str.removesuffix(suffix)`

### `expandtabs(tabsize: int = 8) -> str`

Return a copy where all tab characters are expanded using spaces.
Python: `str.expandtabs(tabsize=8)`

### `replace(old: str, new_: str) -> str`

Return a copy with all occurrences of *old* replaced
by *new_*.
Python: `str.replace(old, new)`

!!! note
    This extension shadows `replace` by design.
    C# instance methods take precedence over extensions, so generated code calling
    `s.Replace(old, new_)` always invokes the BCL method at runtime. This
    overload exists for two reasons: (1) BuiltinRegistry discovers it via reflection
    to register `str.replace` for type-checking, and (2) the 3-arg overload
    calls it directly (as a static call) when `count < 0` to get Python
    empty-string replacement semantics (BCL throws on empty *old*
    on empty *old*).

### `replace(old: str, new_: str, count: int) -> str`

Return a copy with the first *count* occurrences of
*old* replaced by *new_*.
Python: `str.replace(old, new, count)`

### `join(iterable: IEnumerable[str]) -> str`

Return a string which is the concatenation of the strings in
*iterable*. The separator between elements is this
string.
Python: `str.join(iterable)` — called as `separator.join(list)`.

### `split() -> list[str]`

Split on whitespace. Consecutive whitespace is collapsed,
leading/trailing whitespace is stripped.
Python: `str.split()`

### `split(sep: str) -> list[str]`

Split on a separator string.
Python: `str.split(sep)`

### `split(sep: str, maxsplit: int) -> list[str]`

Split on a separator string, performing at most
*maxsplit* splits (from the left).
Python: `str.split(sep, maxsplit)`

### `rsplit() -> list[str]`

Split on whitespace from the right.
Python: `str.rsplit()`

### `rsplit(sep: str) -> list[str]`

Split on a separator string from the right.
Python: `str.rsplit(sep)`

### `rsplit(sep: str, maxsplit: int) -> list[str]`

Split on a separator string from the right, performing at most
*maxsplit* splits.
Python: `str.rsplit(sep, maxsplit)`

### `splitlines() -> list[str]`

Return a list of the lines in the string, breaking at line boundaries.
Python: `str.splitlines()`

### `splitlines(keepends: bool) -> list[str]`

Return a list of lines, optionally keeping line break characters.
Python: `str.splitlines(keepends)`

### `partition(sep: str) -> tuple[str, str, str]`

Split at the first occurrence of *sep*, returning a
3-tuple.
Python: `str.partition(sep)`

### `rpartition(sep: str) -> tuple[str, str, str]`

Split at the last occurrence of *sep*, returning a
3-tuple.
Python: `str.rpartition(sep)`

### `format(*args: object) -> str`

Return a formatted version of the string, using positional arguments.
Python: `str.format(*args)`

### `format_map(mapping: dict[str, object]) -> str`

Return a formatted version of the string, using a mapping of keyword arguments.
Python: `str.format_map(mapping)`

### `maketrans(x: str, y: str) -> Dictionary[char, str]`

Build a translation table mapping characters in *x*
to corresponding characters in *y*.
Python: `str.maketrans(x, y)`

### `maketrans(x: str, y: str, z: str) -> Dictionary[char, str]`

Build a translation table with a deletion set.
Python: `str.maketrans(x, y, z)`

### `translate(table: Dictionary[char, str]) -> str`

Return a copy of the string in which each character has been mapped
through the given translation table.
Python: `str.translate(table)`

### `encode(encoding: str = "utf-8") -> bytes`

Encode the string using the specified encoding and return as bytes.
Python: `str.encode(encoding='utf-8')`

### `find(sub: str) -> int`

Return the lowest index where substring *sub* is found.
Return -1 if not found.
Python: `str.find(sub)`

### `find(sub: str, start: int) -> int`

Return the lowest index where substring *sub* is found,
starting the search at *start*.
Python: `str.find(sub, start)`

### `find(sub: str, start: int, end: int) -> int`

Return the lowest index where substring *sub* is found
within `s[start:end]`.
Python: `str.find(sub, start, end)`

### `rfind(sub: str) -> int`

Return the highest index where substring *sub* is found.
Return -1 if not found.
Python: `str.rfind(sub)`

### `rfind(sub: str, start: int) -> int`

Return the highest index where substring *sub* is found,
searching within `s[start:]`.
Python: `str.rfind(sub, start)`

### `rfind(sub: str, start: int, end: int) -> int`

Return the highest index where substring *sub* is found
within `s[start:end]`.
Python: `str.rfind(sub, start, end)`

### `index(sub: str) -> int`

Like `find` but raises `ValueError`
when the substring is not found.
Python: `str.index(sub)`

### `index(sub: str, start: int) -> int`

Like `find` but raises `ValueError`.
Python: `str.index(sub, start)`

### `index(sub: str, start: int, end: int) -> int`

Like `find` but raises `ValueError`.
Python: `str.index(sub, start, end)`

### `rindex(sub: str) -> int`

Like `rfind` but raises `ValueError`.
Python: `str.rindex(sub)`

### `rindex(sub: str, start: int) -> int`

Like `rfind` but raises `ValueError`.
Python: `str.rindex(sub, start)`

### `rindex(sub: str, start: int, end: int) -> int`

Like `rfind` but raises `ValueError`.
Python: `str.rindex(sub, start, end)`

### `count(sub: str) -> int`

Return the number of non-overlapping occurrences of substring
*sub*.
Python: `str.count(sub)`

### `startswith(prefix: str) -> bool`

Return True if the string starts with the *prefix*.
Python: `str.startswith(prefix)`

### `startswith(prefix: str, start: int) -> bool`

Return True if `s[start:]` starts with the *prefix*.
Python: `str.startswith(prefix, start)`

### `startswith(prefix: str, start: int, end: int) -> bool`

Return True if `s[start:end]` starts with the *prefix*.
Python: `str.startswith(prefix, start, end)`

### `endswith(suffix: str) -> bool`

Return True if the string ends with the *suffix*.
Python: `str.endswith(suffix)`

### `endswith(suffix: str, start: int) -> bool`

Return True if `s[start:]` ends with the *suffix*.
Python: `str.endswith(suffix, start)`

### `endswith(suffix: str, start: int, end: int) -> bool`

Return True if `s[start:end]` ends with the *suffix*.
Python: `str.endswith(suffix, start, end)`

### `isdigit() -> bool`

Return True if all characters are digits and there is at least one character.
Python: `str.isdigit()`

### `isalpha() -> bool`

Return True if all characters are alphabetic and there is at least one character.
Python: `str.isalpha()`

### `isalnum() -> bool`

Return True if all characters are alphanumeric and there is at least one character.
Python: `str.isalnum()`

### `isspace() -> bool`

Return True if all characters are whitespace and there is at least one character.
Python: `str.isspace()`

### `isupper() -> bool`

Return True if all cased characters are uppercase and there is at least
one cased character.
Python: `str.isupper()`

### `islower() -> bool`

Return True if all cased characters are lowercase and there is at least
one cased character.
Python: `str.islower()`

### `istitle() -> bool`

Return True if the string is titlecased and there is at least one character.
Python: `str.istitle()`

### `isnumeric() -> bool`

Return True if all characters are numeric and there is at least one character.
Python: `str.isnumeric()`

### `isdecimal() -> bool`

Return True if all characters are decimal characters and there is at least one character.
Python: `str.isdecimal()`

### `isidentifier() -> bool`

Return True if the string is a valid Python identifier.
Python: `str.isidentifier()`

### `isprintable() -> bool`

Return True if all characters are printable or the string is empty.
Python: `str.isprintable()`

### `isascii() -> bool`

Return True if all characters are ASCII (U+0000 to U+007F) or the string is empty.
Python: `str.isascii()`

### `contains(substring: str) -> bool`

Return True if *substring* is found within this string.
Used for `"x" in s` codegen.

!!! note
    This extension shadows `contains` by design.
    C# instance methods take precedence over extensions, so generated code calling
    `s.Contains(x)` always invokes the BCL method at runtime. This overload
    exists so that BuiltinRegistry can discover it via reflection and register
    `str.contains` for type-checking. The ordinal semantics here match
    Python's byte-level containment check.
