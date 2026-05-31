# hmac

Keyed-hashing for message authentication (HMAC).

```python
import hmac
```

## Functions

### `hmac.new(key: Bytes, msg: Bytes | None = None, digestmod: str = "sha256") -> HmacObject`

Create a new HMAC object from a byte key.

### `hmac.new(key: str, msg: str | None = None, digestmod: str = "sha256") -> HmacObject`

Create a new HMAC object from a string key.

### `hmac.digest(key: Bytes, msg: Bytes, digestmod: str) -> Bytes`

Compute an HMAC digest for a byte message.

### `hmac.digest(key: str, msg: str, digestmod: str) -> Bytes`

Compute an HMAC digest for a string message.

### `hmac.compare_digest(a: str, b: str) -> bool`

Compare two strings in constant time.

### `hmac.compare_digest(a: Bytes, b: Bytes) -> bool`

Compare two byte sequences in constant time.

## HmacObject

Represents an incremental HMAC computation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `digest_size` | `int` | Get the digest size in bytes. |
| `name` | `str` | Get the canonical algorithm name for this HMAC. |

### `update(data: str)`

Update the HMAC with string data.

### `update(data: Bytes)`

Update the HMAC with byte data.

### `hexdigest() -> str`

Return the current digest as lowercase hexadecimal text.

### `digest() -> list[int]`

Return the current digest as a list of byte values.

### `copy() -> HmacObject`

Return a copy of this HMAC object.
