# email

Email message creation and parsing.

```python
import email
```

## Functions

### `email.message_from_string(text: str) -> EmailMessage`

### `email.message_from_bytes(data: Bytes) -> EmailMessage`

### `email.set_content(text: str, subtype: str = "plain")`

### `email.get_content() -> str`

### `email.get_payload() -> str | None`

### `email.is_multipart() -> bool`

### `email.add_attachment(data: Bytes, maintype: str = "application", subtype: str = "octet-stream", filename: str | None = None)`

### `email.iter_attachments() -> list[Attachment]`

### `email.as_string() -> str`

### `email.as_bytes() -> Bytes`

## EmailMessage

Email message with headers and body.
Equivalent to Python's `email.message.EmailMessage`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `data` | `Bytes` |  |
| `content_type` | `str` |  |
| `filename` | `str | None` |  |

### `get_item(name: str) -> str | None`

### `set_item(name: str, value: str)`

### `del_item(name: str)`

### `contains(name: str) -> bool`

### `keys() -> list[str]`

### `values() -> list[str]`

### `get_all(name: str) -> list[str] | None`

### `add_header(name: str, value: str)`

### `replace_header(name: str, value: str)`

## MessageError

Base exception for email module errors.
Equivalent to Python's `email.errors.MessageError`.

## MessageParseError

Base exception for email module errors.
Equivalent to Python's `email.errors.MessageError`.

## HeaderParseError

Base exception for email module errors.
Equivalent to Python's `email.errors.MessageError`.
