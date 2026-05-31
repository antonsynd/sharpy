# secrets

Generate cryptographically strong random numbers suitable for managing secrets.

```python
import secrets
```

## Functions

### `secrets.token_bytes(nbytes: int = 32) -> Bytes`

Return a random byte string containing nbytes bytes.

### `secrets.token_hex(nbytes: int = 32) -> str`

Return a random text string with nbytes random bytes encoded as hex.

### `secrets.token_urlsafe(nbytes: int = 32) -> str`

Return a random URL-safe text string containing nbytes random bytes.

### `secrets.randbelow(exclusive_upper_bound: int) -> int`

Return a random int in the range [0, exclusiveUpperBound).

### `secrets.choice(sequence: list[T]) -> T`

Return a random element from a non-empty sequence.

### `secrets.compare_digest(a: str, b: str) -> bool`

Compare two strings in constant time.

### `secrets.compare_digest(a: Bytes, b: Bytes) -> bool`

Compare two byte sequences in constant time.
