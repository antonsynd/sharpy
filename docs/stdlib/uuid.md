# uuid

UUID objects (universally unique identifiers) according to RFC 4122.

```python
import uuid
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `namespace_dns` | `UUID` | The namespace UUID for fully qualified domain names. |
| `namespace_url` | `UUID` | The namespace UUID for URLs. |
| `namespace_oid` | `UUID` | The namespace UUID for ISO object identifiers. |
| `namespace_x500` | `UUID` | The namespace UUID for X.500 distinguished names. |

## Functions

### `uuid.uuid4() -> UUID`

Generate a random version 4 UUID.

### `uuid.uuid1() -> UUID`

Generate a time-based version 1 UUID.

### `uuid.uuid3(namespace_uuid: UUID, name: str) -> UUID`

Generate a name-based version 3 UUID using MD5.

### `uuid.uuid5(namespace_uuid: UUID, name: str) -> UUID`

Generate a name-based version 5 UUID using SHA-1.

## UUID

Represents a UUID value with Python uuid.UUID-style properties.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `hex` | `str` | Get the UUID as 32 lowercase hexadecimal digits. |
| `urn` | `str` | Get the UUID as a URN string. |
