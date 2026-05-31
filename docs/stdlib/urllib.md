# urllib

URL parsing, quoting, and query string manipulation utilities.

```python
import urllib
```

## Functions

### `urllib.urlparse(url: str, scheme: str = "", allow_fragments: bool = True) -> ParseResult`

Parse a URL into six components: (scheme, netloc, path, params, query, fragment).

**Parameters:**

- `url` (str) -- URL string to parse.
- `scheme` (str) -- Default scheme if none is present in the URL.
- `allow_fragments` (bool)

**Returns:** A `ParseResult` with the six components.

### `urllib.urlsplit(url: str, scheme: str = "", allow_fragments: bool = True) -> SplitResult`

Parse a URL into five components: (scheme, netloc, path, query, fragment).
Similar to `Urlparse` but does not split params from the path.

**Parameters:**

- `url` (str) -- URL string to parse.
- `scheme` (str) -- Default scheme if none is present in the URL.
- `allow_fragments` (bool)

**Returns:** A `SplitResult` with the five components.

### `urllib.urlunparse(components: ParseResult) -> str`

Combine the six components of a `ParseResult` into a URL string.

### `urllib.urlunsplit(components: SplitResult) -> str`

Combine the five components of a `SplitResult` into a URL string.

### `urllib.urljoin(base_url: str, url: str, allow_fragments: bool = True) -> str`

Construct a full URL by combining a base URL with a relative URL.

**Parameters:**

- `base_url` (str)
- `url` (str) -- The URL to join (may be relative).
- `allow_fragments` (bool)

**Returns:** The combined URL.

### `urllib.parse_qs(qs: str, separator: str = "&") -> dict[str, list[str]]`

Parse a query string and return a dictionary of lists.
Keys that appear multiple times have all values aggregated.

**Parameters:**

- `qs` (str) -- The query string to parse.
- `separator` (str) -- The separator between key-value pairs.

**Returns:** A dictionary mapping keys to lists of values.

### `urllib.parse_qsl(qs: str, separator: str = "&") -> list[tuple[str, str]]`

Parse a query string and return a list of (key, value) tuples.

**Parameters:**

- `qs` (str) -- The query string to parse.
- `separator` (str) -- The separator between key-value pairs.

**Returns:** A list of (key, value) tuples.

### `urllib.urlencode(query: dict[str, object | None], doseq: bool = False) -> str`

Encode a dictionary of query parameters into a query string.

**Parameters:**

- `query` (dict[str, object | None]) -- Dictionary of key-value pairs.
- `doseq` (bool) -- If True, sequence values are encoded as separate key=value pairs.

**Returns:** A URL-encoded query string.

### `urllib.urlencode(query: list[tuple[str, str]]) -> str`

Encode a list of (key, value) tuples into a query string.

**Parameters:**

- `query` (list[tuple[str, str]]) -- List of (key, value) tuples.

**Returns:** A URL-encoded query string.

### `urllib.quote(s: str, safe: str = "/") -> str`

Percent-encode a string. Characters in *safe* are not encoded.
By default, `/` is considered safe.

**Parameters:**

- `s` (str) -- The string to encode.
- `safe` (str) -- Characters that should not be encoded.

**Returns:** The percent-encoded string.

### `urllib.quote_plus(s: str, safe: str = "") -> str`

Like `Quote` but also replaces spaces with `+` signs.
By default, no characters are considered safe.

**Parameters:**

- `s` (str) -- The string to encode.
- `safe` (str) -- Characters that should not be encoded.

**Returns:** The percent-encoded string with spaces as +.

### `urllib.unquote(s: str) -> str`

Decode a percent-encoded string.

**Parameters:**

- `s` (str) -- The string to decode.

**Returns:** The decoded string.

### `urllib.unquote_plus(s: str) -> str`

Like `Unquote` but also replaces `+` signs with spaces.

**Parameters:**

- `s` (str) -- The string to decode.

**Returns:** The decoded string.

## ParseResult

Result of `UrllibModule.Urlparse`. Contains the six components of a
parsed URL: scheme, netloc, path, params, query, and fragment.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `scheme` | `str` | URL scheme specifier (e.g. "https"). |
| `netloc` | `str` | Network location part (e.g. "user:pass@host:8080"). |
| `path` | `str` | Hierarchical path (e.g. "/index.html"). |
| `params` | `str` | Parameters for the last path element (text after semicolon). |
| `query` | `str` | Query component (text after '?'). |
| `fragment` | `str` | Fragment identifier (text after '#'). |

### `geturl() -> str`

Reassemble the URL from its components.

## SplitResult

Result of `UrllibModule.Urlsplit`. Contains five components of a
parsed URL: scheme, netloc, path, query, and fragment (no params).

### Properties

| Name | Type | Description |
|------|------|-------------|
| `scheme` | `str` | URL scheme specifier (e.g. "https"). |
| `netloc` | `str` | Network location part (e.g. "user:pass@host:8080"). |
| `path` | `str` | Hierarchical path (e.g. "/index.html"). |
| `query` | `str` | Query component (text after '?'). |
| `fragment` | `str` | Fragment identifier (text after '#'). |

### `geturl() -> str`

Reassemble the URL from its components.
