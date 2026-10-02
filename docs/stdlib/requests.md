# requests

HTTP library for making requests (GET, POST, PUT, DELETE, etc.).

```python
import requests
```

## Functions

### `requests.get(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a GET request.

### `requests.post(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a POST request.

### `requests.put(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a PUT request.

### `requests.delete(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a DELETE request.

### `requests.patch(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a PATCH request.

### `requests.head(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send a HEAD request.

### `requests.options(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False, allow_redirects: bool | None = None, verify: bool = True) -> Result[Response, RequestException]`

Send an OPTIONS request.

## RequestException

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `response` | `Response \| None` | The HTTP response associated with this error, if any. |

## ConnectionError

Raised when a network connectivity failure occurs (DNS failure, refused connection, etc.).
Equivalent to Python's `requests.ConnectionError`.

## Timeout

Raised when a request times out.
Equivalent to Python's `requests.Timeout`.

!!! note
    Note: this name collides with `System.Threading.Timeout`. Use the fully-qualified
    `System.Threading.Timeout` reference when both are in scope.

## HTTPError

Raised when an HTTP error status code is returned and `raise_for_status()` is called.
Equivalent to Python's `requests.HTTPError`.

## Response

Represents an HTTP response. Wraps `System.Net.Http.HttpResponseMessage`.
Equivalent to Python's `requests.Response`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `status_code` | `int` | The HTTP status code returned by the server (e.g., 200, 404). |
| `ok` | `bool` | True if the status code is in the 2xx range, false otherwise. Equivalent to Python's \`response.ok\`. |

### `json() -> object | None`

Parse the response body as JSON and return the resulting object.
Equivalent to Python's `response.json()`.

**Returns:** The parsed JSON object (Dict, List, string, int, double, bool, or None).

### `raise_for_status() -> Result[Response, RequestException]`

Returns `Ok(this)` for 2xx status codes, or `Err(HTTPError)` for 4xx/5xx
(and any other non-2xx) status codes.
Equivalent to Python's `response.raise_for_status()`, but uses a tagged
`Result[T, E]` instead of raising.

### `__str__() -> str`

`repr()` uses the same method. Returns a string representation of this response (e.g., `<Response [200]>`).

### `iter_content(chunk_size: int = 1024) -> IEnumerable[array[uint8]]`

Iterate over the response body in chunks of the given size (default 1024 bytes).
The response must have been created with `stream=True` and the body must not
have been fully read (via `content` or `text`).
Equivalent to Python's `response.iter_content(chunk_size)`.

### `iter_lines() -> IEnumerable[str]`

Iterate over the response body line by line, using the response encoding to decode
bytes. The response must have been created with `stream=True` and the body
must not have been fully read.
Equivalent to Python's `response.iter_lines()`.

## Session

A session for sending multiple HTTP requests with shared configuration
(default headers, cookies, authentication). Equivalent to Python's
`requests.Session`.

!!! note
    Wraps a single `HttpClientHandler`/`HttpClient` pair
    so connections and a `CookieContainer` are reused across requests.
    Always dispose with `using` (or call `dispose`) to release the
    underlying client.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `auth` | `tuple[str, str] \| None` | Default Basic auth credentials (username, password) sent with every request from this session. Per-request \`auth\` takes precedence, and an explicit \`Authorization\` header (per-request or session-level) also suppresses this default. |

### `get(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a GET request using this session.

### `post(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a POST request using this session.

### `put(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a PUT request using this session.

### `delete(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a DELETE request using this session.

### `patch(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a PATCH request using this session.

### `head(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send a HEAD request using this session.

### `options(url: str, headers: dict[str, str] | None = None, params_: dict[str, str] | None = None, json: object | None = None, data: dict[str, str] | None = None, timeout: float | None = None, auth: tuple[str, str] | None = None, files: dict[str, str] | None = None, stream: bool = False) -> Result[Response, RequestException]`

Send an OPTIONS request using this session.
