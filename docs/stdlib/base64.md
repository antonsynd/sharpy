# base64

RFC 4648 base16, base32, base64, and base85 data encodings.

```python
import base64
```

## Functions

### `base64.b64encode(s: bytes, altchars: bytes | None = None) -> bytes`

Encode bytes using Base64.

### `base64.b64decode(s: bytes, altchars: bytes | None = None, validate: bool = False) -> bytes`

Decode Base64-encoded bytes.

### `base64.b64decode(s: str, altchars: bytes | None = None, validate: bool = False) -> bytes`

Decode a Base64-encoded string.

### `base64.urlsafe_b64encode(s: bytes) -> bytes`

Encode bytes using URL-safe Base64.

### `base64.urlsafe_b64decode(s: bytes) -> bytes`

Decode URL-safe Base64-encoded bytes.

### `base64.urlsafe_b64decode(s: str) -> bytes`

Decode a URL-safe Base64-encoded string.

### `base64.b32encode(s: bytes) -> bytes`

Encode bytes using Base32.

### `base64.b32decode(s: bytes, casefold: bool = False) -> bytes`

Decode Base32-encoded bytes.

### `base64.b16encode(s: bytes) -> bytes`

Encode bytes using Base16.

### `base64.b16decode(s: bytes, casefold: bool = False) -> bytes`

Decode Base16-encoded bytes.

### `base64.b85encode(s: bytes) -> bytes`

Encode bytes using RFC 1924 Base85.

### `base64.b85decode(s: bytes) -> bytes`

Decode RFC 1924 Base85-encoded bytes.

### `base64.a85encode(s: bytes) -> bytes`

Encode bytes using Adobe-style Ascii85.

### `base64.a85decode(s: bytes) -> bytes`

Decode Adobe-style Ascii85-encoded bytes.
