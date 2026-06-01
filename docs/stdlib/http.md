# http

HTTP modules — status codes, connections, and responses.

```python
import http
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `http_port` | `int` |  |
| `https_port` | `int` |  |

## HTTPException

Base exception for http module errors.
Equivalent to Python's `http.client.HTTPException`.

## InvalidURL

Base exception for http module errors.
Equivalent to Python's `http.client.HTTPException`.

## NotConnected

Base exception for http module errors.
Equivalent to Python's `http.client.HTTPException`.

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

Lower-level HTTP connection. Equivalent to Python's `http.client.HTTPConnection`.

## HTTPResponse

HTTP response from a low-level HTTP connection.
Equivalent to Python's `http.client.HTTPResponse`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `status` | `int` |  |
| `reason` | `str` |  |

### `read() -> Bytes`

### `read(amt: int) -> Bytes`

### `getheader(name: str, default_: str | None = None) -> str | None`

### `close()`

## HTTPStatus

HTTP status codes with phrase descriptions.
Equivalent to Python's `http.HTTPStatus`.

### Constants

| Name | Type | Description |
|------|------|-------------|
| `continue` | `HTTPStatus` |  |
| `switching_protocols` | `HTTPStatus` |  |
| `processing` | `HTTPStatus` |  |
| `early_hints` | `HTTPStatus` |  |
| `ok` | `HTTPStatus` |  |
| `created` | `HTTPStatus` |  |
| `accepted` | `HTTPStatus` |  |
| `non_authoritative_information` | `HTTPStatus` |  |
| `no_content` | `HTTPStatus` |  |
| `reset_content` | `HTTPStatus` |  |
| `partial_content` | `HTTPStatus` |  |
| `multi_status` | `HTTPStatus` |  |
| `already_reported` | `HTTPStatus` |  |
| `im_used` | `HTTPStatus` |  |
| `multiple_choices` | `HTTPStatus` |  |
| `moved_permanently` | `HTTPStatus` |  |
| `found` | `HTTPStatus` |  |
| `see_other` | `HTTPStatus` |  |
| `not_modified` | `HTTPStatus` |  |
| `use_proxy` | `HTTPStatus` |  |
| `temporary_redirect` | `HTTPStatus` |  |
| `permanent_redirect` | `HTTPStatus` |  |
| `bad_request` | `HTTPStatus` |  |
| `unauthorized` | `HTTPStatus` |  |
| `payment_required` | `HTTPStatus` |  |
| `forbidden` | `HTTPStatus` |  |
| `not_found` | `HTTPStatus` |  |
| `method_not_allowed` | `HTTPStatus` |  |
| `not_acceptable` | `HTTPStatus` |  |
| `proxy_authentication_required` | `HTTPStatus` |  |
| `request_timeout` | `HTTPStatus` |  |
| `conflict` | `HTTPStatus` |  |
| `gone` | `HTTPStatus` |  |
| `length_required` | `HTTPStatus` |  |
| `precondition_failed` | `HTTPStatus` |  |
| `content_too_large` | `HTTPStatus` |  |
| `uri_too_long` | `HTTPStatus` |  |
| `unsupported_media_type` | `HTTPStatus` |  |
| `range_not_satisfiable` | `HTTPStatus` |  |
| `expectation_failed` | `HTTPStatus` |  |
| `im_a_teapot` | `HTTPStatus` |  |
| `misdirected_request` | `HTTPStatus` |  |
| `unprocessable_content` | `HTTPStatus` |  |
| `locked` | `HTTPStatus` |  |
| `failed_dependency` | `HTTPStatus` |  |
| `too_early` | `HTTPStatus` |  |
| `upgrade_required` | `HTTPStatus` |  |
| `precondition_required` | `HTTPStatus` |  |
| `too_many_requests` | `HTTPStatus` |  |
| `request_header_fields_too_large` | `HTTPStatus` |  |
| `unavailable_for_legal_reasons` | `HTTPStatus` |  |
| `internal_server_error` | `HTTPStatus` |  |
| `not_implemented` | `HTTPStatus` |  |
| `bad_gateway` | `HTTPStatus` |  |
| `service_unavailable` | `HTTPStatus` |  |
| `gateway_timeout` | `HTTPStatus` |  |
| `http_version_not_supported` | `HTTPStatus` |  |
| `variant_also_negotiates` | `HTTPStatus` |  |
| `insufficient_storage` | `HTTPStatus` |  |
| `loop_detected` | `HTTPStatus` |  |
| `not_extended` | `HTTPStatus` |  |
| `network_authentication_required` | `HTTPStatus` |  |

### Properties

| Name | Type | Description |
|------|------|-------------|
| `value` | `int` |  |
| `name` | `str` |  |
| `phrase` | `str` |  |

### `from_value(value: int) -> HTTPStatus`
