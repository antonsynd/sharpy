# requests

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

```python
import requests
```

## Functions

### `requests.get(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a GET request.

### `requests.post(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a POST request.

### `requests.put(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a PUT request.

### `requests.delete(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a DELETE request.

### `requests.patch(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a PATCH request.

### `requests.head(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a HEAD request.

### `requests.options(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send an OPTIONS request.

## RequestException

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `response` | `Response?` | The HTTP response associated with this error, if any. |

## ConnectionError

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

## Timeout

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

## HTTPError

Base class for all requests-related errors.
Equivalent to Python's `requests.RequestException`.

## Response

Represents an HTTP response. Wraps `System.Net.Http.HttpResponseMessage`.
Equivalent to Python's `requests.Response`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ok` | `bool` | True if the status code is in the 2xx range, false otherwise. Equivalent to Python's \`response.ok\`. |

### `json() -> object?`

Parse the response body as JSON and return the resulting object.
Equivalent to Python's `response.json()`.

**Returns:** The parsed JSON object (Dict, List, string, int, double, bool, or null).

### `raise_for_status() -> Result[Response, RequestException]`

Returns `Ok(this)` for 2xx status codes, or `Err(HTTPError)` for 4xx/5xx
(and any other non-2xx) status codes.
Equivalent to Python's `response.raise_for_status()`, but uses a tagged
`Result{T, E}` instead of raising.

### `iter_content(chunk_size: int = 1024) -> Iterable[list[byte]]`

Iterate over the response body in chunks of the given size (default 1024 bytes).
The response must have been created with `stream=true` and the body must not
have been fully read (via `Content` or `Text`).
Equivalent to Python's `response.iter_content(chunk_size)`.

### `iter_lines() -> Iterable[str]`

Iterate over the response body line by line, using the response encoding to decode
bytes. The response must have been created with `stream=true` and the body
must not have been fully read.
Equivalent to Python's `response.iter_lines()`.

## Session

A session for sending multiple HTTP requests with shared configuration
(default headers, cookies, authentication). Equivalent to Python's
`requests.Session`.

### `get(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a GET request using this session.

### `post(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a POST request using this session.

### `put(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a PUT request using this session.

### `delete(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a DELETE request using this session.

### `patch(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a PATCH request using this session.

### `head(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send a HEAD request using this session.

### `options(url: str, headers: dict[str, str]? = null, params_: dict[str, str]? = null, json: object? = null, data: dict[str, str]? = null, timeout: float? = null, (string, string) -> Result[Response, RequestException]`

Send an OPTIONS request using this session.
