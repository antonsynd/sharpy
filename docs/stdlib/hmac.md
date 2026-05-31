# hmac

Module exports for the hmac module.

```python
import hmac
```

## Functions

### `hmac.new(key: Bytes, msg: Bytes | None = None, digestmod: str = "sha256") -> HmacObject`

### `hmac.new(key: str, msg: str | None = None, digestmod: str = "sha256") -> HmacObject`

### `hmac.digest(key: Bytes, msg: Bytes, digestmod: str) -> Bytes`

### `hmac.digest(key: str, msg: str, digestmod: str) -> Bytes`

### `hmac.compare_digest(a: str, b: str) -> bool`

### `hmac.compare_digest(a: Bytes, b: Bytes) -> bool`

## HmacObject

### Properties

| Name | Type | Description |
|------|------|-------------|
| `digest_size` | `int` |  |
| `name` | `str` |  |

### `update(data: str)`

### `update(data: Bytes)`

### `hexdigest() -> str`

### `digest() -> list[int]`

### `copy() -> HmacObject`
