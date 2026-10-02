# difflib

```python
import difflib
```

## Functions

### `difflib.unified_diff(a: IList[str], b: IList[str], from_file: str = "", to_file: str = "", from_file_date: str = "", to_file_date: str = "", n: int = 3, lineterm: str = "\n") -> IEnumerable[str]`

### `difflib.context_diff(a: IList[str], b: IList[str], from_file: str = "", to_file: str = "", from_file_date: str = "", to_file_date: str = "", n: int = 3, lineterm: str = "\n") -> IEnumerable[str]`

### `difflib.ndiff(a: IList[str], b: IList[str], line_junk: ((str) -> bool) | None = None, char_junk: ((str) -> bool) | None = None) -> IEnumerable[str]`

### `difflib.get_close_matches(word: str, possibilities: IList[str], n: int = 3, cutoff: float = 0.6) -> list[str]`

### `difflib.is_line_junk(line: str) -> bool`

### `difflib.is_character_junk(ch: str) -> bool`

### `difflib.restore(delta: IEnumerable[str], which: int) -> IEnumerable[str]`

## Differ

### `compare(a: IList[str], b: IList[str]) -> IEnumerable[str]`

## SequenceMatcher

### `set_seqs(a: IList[T], b: IList[T])`

### `set_seq1(a: IList[T])`

### `set_seq2(b: IList[T])`

### `find_longest_match(a_lo: int, a_hi: int, b_lo: int, b_hi: int) -> tuple[int, int, int]`

### `get_matching_blocks() -> list[tuple[int, int, int]]`

### `get_opcodes() -> list[tuple[str, int, int, int, int]]`

### `get_grouped_opcodes(n: int = 3) -> list[list[tuple[str, int, int, int, int]]]`

### `ratio() -> float`

### `quick_ratio() -> float`

### `real_quick_ratio() -> float`
