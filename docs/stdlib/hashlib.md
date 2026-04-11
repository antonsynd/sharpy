# hashlib

Represents a hash object that accumulates data and computes cryptographic hashes.
Mirrors Python's hashlib hash object API.

```python
import hashlib
```

## Functions

### `hashlib.md5(data: str = "") -> HashObject`

Create an MD5 hash object, optionally initialized with data.

**Parameters:**

- `data` (str) -- Optional initial data to hash (encoded as UTF-8).

**Returns:** A new `HashObject` using the MD5 algorithm.

### `hashlib.sha1(data: str = "") -> HashObject`

Create a SHA-1 hash object, optionally initialized with data.

**Parameters:**

- `data` (str) -- Optional initial data to hash (encoded as UTF-8).

**Returns:** A new `HashObject` using the SHA-1 algorithm.

### `hashlib.sha256(data: str = "") -> HashObject`

Create a SHA-256 hash object, optionally initialized with data.

**Parameters:**

- `data` (str) -- Optional initial data to hash (encoded as UTF-8).

**Returns:** A new `HashObject` using the SHA-256 algorithm.

### `hashlib.sha384(data: str = "") -> HashObject`

Create a SHA-384 hash object, optionally initialized with data.

**Parameters:**

- `data` (str) -- Optional initial data to hash (encoded as UTF-8).

**Returns:** A new `HashObject` using the SHA-384 algorithm.

### `hashlib.sha512(data: str = "") -> HashObject`

Create a SHA-512 hash object, optionally initialized with data.

**Parameters:**

- `data` (str) -- Optional initial data to hash (encoded as UTF-8).

**Returns:** A new `HashObject` using the SHA-512 algorithm.

## HashObject

Represents a hash object that accumulates data and computes cryptographic hashes.
Mirrors Python's hashlib hash object API.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `digest_size` | `int` | The size of the resulting hash in bytes. |
| `name` | `str` | The canonical name of this hashing algorithm (e.g., "sha256"). |

### `update(data: str)`

Append data to the hash object. The data is encoded as UTF-8.

**Parameters:**

- `data` (str) -- The string data to append.

### `hexdigest() -> str`

Return the hex-encoded string of the hash digest.

**Returns:** A lowercase hex string of the computed hash.

### `digest() -> list[int]`

Return the raw hash digest as a list of integers (byte values 0-255).

**Returns:** A `List{T}` of integer byte values.

### `copy() -> HashObject`

Return a copy of the hash object with the same accumulated data.

**Returns:** A new `HashObject` with the same state.
