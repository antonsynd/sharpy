# difflib

```python
import difflib
```

## Functions

### `difflib.unified_diff(a: IList[str], b: IList[str], from_file: str = "", to_file: str = "", from_file_date: str = "", to_file_date: str = "", n: int = 3, lineterm: str = "\n") -> Iterable[str]`

### `difflib.context_diff(a: IList[str], b: IList[str], from_file: str = "", to_file: str = "", from_file_date: str = "", to_file_date: str = "", n: int = 3, lineterm: str = "\n") -> Iterable[str]`

### `difflib.ndiff(a: IList[str], b: IList[str], line_junk: (str) -> bool | None = None, char_junk: (str) -> bool | None = None) -> Iterable[str]`

### `difflib.get_close_matches(word: str, possibilities: IList[str], n: int = 3, cutoff: float = 0.6) -> list[str]`

### `difflib.is_line_junk(line: str) -> bool`

### `difflib.is_character_junk(ch: str) -> bool`

### `difflib.restore(delta: Iterable[str], which: int) -> Iterable[str]`

## Differ

### `compare(a: IList[str], b: IList[str]) -> Iterable[str]`

## SequenceMatcher

### `set_seqs(a: IList[T], b: IList[T])`

### `set_seq1(a: IList[T])`

### `set_seq2(b: IList[T])`

### `ratio() -> float`

### `quick_ratio() -> float`

### `real_quick_ratio() -> float`
