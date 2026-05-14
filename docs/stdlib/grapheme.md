# grapheme

Grapheme cluster (user-perceived character) operations. Wraps `System.Globalization.StringInfo` for working with text at the level of what users perceive as a single character — including combining marks, emoji sequences, and ZWJ (zero-width joiner) sequences.

```python
import grapheme
```

## Functions

### `grapheme.graphemes(text: str) -> list[str]`

Split a string into a list of grapheme clusters.

**Parameters:**

- `text` (str) -- The string to split.

**Returns:** A list where each element is a single grapheme cluster.

```python
grapheme.graphemes("héllo")    # ["h", "é", "l", "l", "o"]
grapheme.graphemes("👨‍👩‍👧")  # ["👨‍👩‍👧"]
```

### `grapheme.length(text: str) -> int`

Return the number of grapheme clusters in a string.

**Parameters:**

- `text` (str) -- The string to measure.

**Returns:** The count of grapheme clusters.

```python
grapheme.length("hello")    # 5
grapheme.length("héllo")    # 5 (é is a single grapheme)
grapheme.length("👨‍👩‍👧")  # 1 (ZWJ sequence)
```

### `grapheme.slice(text: str, start: int) -> str`

Return a substring containing all graphemes from *start* to the end. Negative values count from the end.

**Parameters:**

- `text` (str) -- The string to slice.
- `start` (int) -- The starting grapheme index (inclusive).

**Returns:** The substring from *start* to the end of the string.

```python
grapheme.slice("héllo", 2)  # "llo"
```

### `grapheme.slice(text: str, start: int, end: int) -> str`

Return a substring by grapheme cluster index range. Indices are clamped to the valid range to match Python's slice semantics. Negative indices count from the end.

**Parameters:**

- `text` (str) -- The string to slice.
- `start` (int) -- The starting grapheme index (inclusive).
- `end` (int) -- The ending grapheme index (exclusive).

**Returns:** The substring containing graphemes from *start* up to (but not including) *end*.

```python
grapheme.slice("héllo", 0, 3)  # "hél"
```

### `grapheme.at(text: str, index: int) -> str`

Return a single grapheme cluster at the given index. Negative values count from the end.

**Parameters:**

- `text` (str) -- The string to index.
- `index` (int) -- The grapheme index.

**Returns:** The grapheme cluster at *index*.

**Raises:** `IndexError` if *index* is out of range.

```python
grapheme.at("héllo", 1)   # "é"
grapheme.at("héllo", -1)  # "o"
```
