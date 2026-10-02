# email

Email message creation and parsing.

```python
import email
```

## Functions

### `email.message_from_string(text: str) -> EmailMessage`

### `email.message_from_bytes(data: bytes) -> EmailMessage`

## EmailMessage

Email message with headers and body.
Equivalent to Python's `email.message.EmailMessage`.

### `get_item(name: str) -> str | None`

### `set_item(name: str, value: str)`

### `del_item(name: str)`

### `contains(name: str) -> bool`

### `keys() -> list[str]`

### `values() -> list[str]`

### `items() -> list[tuple[str, str]]`

### `get_all(name: str) -> list[str] | None`

### `add_header(name: str, value: str)`

### `replace_header(name: str, value: str)`

### `set_content(text: str, subtype: str = "plain")`

### `get_content() -> str`

### `get_payload() -> str | None`

### `is_multipart() -> bool`

### `add_attachment(data: bytes, maintype: str = "application", subtype: str = "octet-stream", filename: str | None = None)`

### `iter_attachments() -> list[Attachment]`

### `as_string() -> str`

### `as_bytes() -> bytes`

### `__str__() -> str`

`repr()` uses the same method.

## Attachment

An email attachment with binary data.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `data` | `bytes` |  |
| `content_type` | `str` |  |
| `filename` | `str \| None` |  |

## MessageError

Base exception for email module errors.
Equivalent to Python's `email.errors.MessageError`.

## MessageParseError

Raised when an email message cannot be parsed.

## HeaderParseError

Raised when a header cannot be parsed.
