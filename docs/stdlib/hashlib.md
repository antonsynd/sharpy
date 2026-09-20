# hashlib

Secure hash and message digest algorithms.

```python
import hashlib
```

## Properties

| Name | Type | Description |
|------|------|-------------|
| `name` | `str` |  |
| `digest_size` | `int` |  |

## Functions

### `hashlib.update(data: str)`

Append data to the hash object. The data is encoded as UTF-8.

### `hashlib.hexdigest() -> str`

Return the hex-encoded string of the hash digest.

### `hashlib.digest() -> list[int]`

Return the raw hash digest as a list of integers (byte values 0-255).

### `hashlib.copy() -> HashObject`

Return a copy of the hash object with the same accumulated data.

### `hashlib.md5(data: str = "") -> HashObject`

Return a new hash object for MD5, optionally initialized with data.

### `hashlib.sha1(data: str = "") -> HashObject`

Return a new hash object for SHA-1, optionally initialized with data.

### `hashlib.sha256(data: str = "") -> HashObject`

Return a new hash object for SHA-256, optionally initialized with data.

### `hashlib.sha384(data: str = "") -> HashObject`

Return a new hash object for SHA-384, optionally initialized with data.

### `hashlib.sha512(data: str = "") -> HashObject`

Return a new hash object for SHA-512, optionally initialized with data.

### `hashlib.sha224(data: str = "") -> HashObject`

Return a new hash object for SHA-224, optionally initialized with data.

### `hashlib.sha3256(data: str = "") -> HashObject`

Return a new hash object for SHA3-256, optionally initialized with data.

### `hashlib.sha3512(data: str = "") -> HashObject`

Return a new hash object for SHA3-512, optionally initialized with data.

### `hashlib.blake2b(data: str = "") -> HashObject`

Return a new hash object for BLAKE2b, optionally initialized with data.

### `hashlib.blake2s(data: str = "") -> HashObject`

Return a new hash object for BLAKE2s, optionally initialized with data.
