# secrets

```python
import secrets
```

## Functions

### `secrets.token_bytes(nbytes: int = 32) -> Bytes`

### `secrets.token_hex(nbytes: int = 32) -> str`

### `secrets.token_urlsafe(nbytes: int = 32) -> str`

### `secrets.randbelow(exclusive_upper_bound: int) -> int`

### `secrets.choice(sequence: list[T]) -> T`

### `secrets.compare_digest(a: str, b: str) -> bool`

### `secrets.compare_digest(a: Bytes, b: Bytes) -> bool`
