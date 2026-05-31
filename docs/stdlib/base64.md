# base64

RFC 4648 base16, base32, base64, and base85 data encodings.

```python
import base64
```

## Functions

### `base64.b64encode(s: Bytes, altchars: Bytes | None = None) -> Bytes`

Encode bytes using Base64.

### `base64.b64decode(s: Bytes, altchars: Bytes | None = None, validate: bool = False) -> Bytes`

Decode Base64-encoded bytes.

### `base64.b64decode(s: str, altchars: Bytes | None = None, validate: bool = False) -> Bytes`

Decode a Base64-encoded string.

### `base64.urlsafe_b64encode(s: Bytes) -> Bytes`

Encode bytes using URL-safe Base64.

### `base64.urlsafe_b64decode(s: Bytes) -> Bytes`

Decode URL-safe Base64-encoded bytes.

### `base64.urlsafe_b64decode(s: str) -> Bytes`

Decode a URL-safe Base64-encoded string.

### `base64.b32encode(s: Bytes) -> Bytes`

Encode bytes using Base32.

### `base64.b32decode(s: Bytes, casefold: bool = False) -> Bytes`

Decode Base32-encoded bytes.

### `base64.b16encode(s: Bytes) -> Bytes`

Encode bytes using Base16.

### `base64.b16decode(s: Bytes, casefold: bool = False) -> Bytes`

Decode Base16-encoded bytes.

### `base64.b85encode(s: Bytes) -> Bytes`

Encode bytes using RFC 1924 Base85.

### `base64.b85decode(s: Bytes) -> Bytes`

Decode RFC 1924 Base85-encoded bytes.

### `base64.a85encode(s: Bytes) -> Bytes`

Encode bytes using Adobe-style Ascii85.

### `base64.a85decode(s: Bytes) -> Bytes`

Decode Adobe-style Ascii85-encoded bytes.
