# http

HTTP modules — status codes, connections, and responses.

```python
import http
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `HTTP_PORT` | `int` |  |
| `HTTPS_PORT` | `int` |  |

## HTTPException

Base exception for http module errors.
Equivalent to Python's `http.client.HTTPException`.

## InvalidURL

Raised when an invalid URL is provided.
Equivalent to Python's `http.client.InvalidURL`.

## NotConnected

Raised when no request has been sent yet.
Equivalent to Python's `http.client.NotConnected`.

## HTTPConnection

Lower-level HTTP connection. Equivalent to Python's `http.client.HTTPConnection`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `host` | `str` |  |
| `port` | `int` |  |

### `request(method: str, url: str, body: str | None = None, headers: dict[str, str] | None = None)`

### `getresponse() -> HTTPResponse`

### `close()`

## HTTPSConnection

HTTPS connection. Equivalent to Python's `http.client.HTTPSConnection`.

## HTTPResponse

HTTP response from a low-level HTTP connection.
Equivalent to Python's `http.client.HTTPResponse`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `status` | `int` |  |
| `reason` | `str` |  |

### `read() -> bytes`

### `read(amt: int) -> bytes`

### `getheader(name: str, default_: str | None = None) -> str | None`

### `getheaders() -> list[tuple[str, str]]`

### `close()`

## HTTPStatus

HTTP status codes with phrase descriptions.
Equivalent to Python's `http.HTTPStatus`.

### Constants

| Name | Type | Description |
|------|------|-------------|
| `CONTINUE` | `HTTPStatus` |  |
| `SWITCHING_PROTOCOLS` | `HTTPStatus` |  |
| `PROCESSING` | `HTTPStatus` |  |
| `EARLY_HINTS` | `HTTPStatus` |  |
| `OK` | `HTTPStatus` |  |
| `CREATED` | `HTTPStatus` |  |
| `ACCEPTED` | `HTTPStatus` |  |
| `NON_AUTHORITATIVE_INFORMATION` | `HTTPStatus` |  |
| `NO_CONTENT` | `HTTPStatus` |  |
| `RESET_CONTENT` | `HTTPStatus` |  |
| `PARTIAL_CONTENT` | `HTTPStatus` |  |
| `MULTI_STATUS` | `HTTPStatus` |  |
| `ALREADY_REPORTED` | `HTTPStatus` |  |
| `IM_USED` | `HTTPStatus` |  |
| `MULTIPLE_CHOICES` | `HTTPStatus` |  |
| `MOVED_PERMANENTLY` | `HTTPStatus` |  |
| `FOUND` | `HTTPStatus` |  |
| `SEE_OTHER` | `HTTPStatus` |  |
| `NOT_MODIFIED` | `HTTPStatus` |  |
| `USE_PROXY` | `HTTPStatus` |  |
| `TEMPORARY_REDIRECT` | `HTTPStatus` |  |
| `PERMANENT_REDIRECT` | `HTTPStatus` |  |
| `BAD_REQUEST` | `HTTPStatus` |  |
| `UNAUTHORIZED` | `HTTPStatus` |  |
| `PAYMENT_REQUIRED` | `HTTPStatus` |  |
| `FORBIDDEN` | `HTTPStatus` |  |
| `NOT_FOUND` | `HTTPStatus` |  |
| `METHOD_NOT_ALLOWED` | `HTTPStatus` |  |
| `NOT_ACCEPTABLE` | `HTTPStatus` |  |
| `PROXY_AUTHENTICATION_REQUIRED` | `HTTPStatus` |  |
| `REQUEST_TIMEOUT` | `HTTPStatus` |  |
| `CONFLICT` | `HTTPStatus` |  |
| `GONE` | `HTTPStatus` |  |
| `LENGTH_REQUIRED` | `HTTPStatus` |  |
| `PRECONDITION_FAILED` | `HTTPStatus` |  |
| `CONTENT_TOO_LARGE` | `HTTPStatus` |  |
| `URI_TOO_LONG` | `HTTPStatus` |  |
| `UNSUPPORTED_MEDIA_TYPE` | `HTTPStatus` |  |
| `RANGE_NOT_SATISFIABLE` | `HTTPStatus` |  |
| `EXPECTATION_FAILED` | `HTTPStatus` |  |
| `IM_A_TEAPOT` | `HTTPStatus` |  |
| `MISDIRECTED_REQUEST` | `HTTPStatus` |  |
| `UNPROCESSABLE_CONTENT` | `HTTPStatus` |  |
| `LOCKED` | `HTTPStatus` |  |
| `FAILED_DEPENDENCY` | `HTTPStatus` |  |
| `TOO_EARLY` | `HTTPStatus` |  |
| `UPGRADE_REQUIRED` | `HTTPStatus` |  |
| `PRECONDITION_REQUIRED` | `HTTPStatus` |  |
| `TOO_MANY_REQUESTS` | `HTTPStatus` |  |
| `REQUEST_HEADER_FIELDS_TOO_LARGE` | `HTTPStatus` |  |
| `UNAVAILABLE_FOR_LEGAL_REASONS` | `HTTPStatus` |  |
| `INTERNAL_SERVER_ERROR` | `HTTPStatus` |  |
| `NOT_IMPLEMENTED` | `HTTPStatus` |  |
| `BAD_GATEWAY` | `HTTPStatus` |  |
| `SERVICE_UNAVAILABLE` | `HTTPStatus` |  |
| `GATEWAY_TIMEOUT` | `HTTPStatus` |  |
| `HTTP_VERSION_NOT_SUPPORTED` | `HTTPStatus` |  |
| `VARIANT_ALSO_NEGOTIATES` | `HTTPStatus` |  |
| `INSUFFICIENT_STORAGE` | `HTTPStatus` |  |
| `LOOP_DETECTED` | `HTTPStatus` |  |
| `NOT_EXTENDED` | `HTTPStatus` |  |
| `NETWORK_AUTHENTICATION_REQUIRED` | `HTTPStatus` |  |

### Properties

| Name | Type | Description |
|------|------|-------------|
| `value` | `int` |  |
| `name` | `str` |  |
| `phrase` | `str` |  |

### `from_value(value: int) -> HTTPStatus`

### `__str__() -> str`

`repr()` uses the same method.
