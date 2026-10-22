# uuid

UUID objects (universally unique identifiers) according to RFC 4122.

```python
import uuid
```

## Constants

| Name | Type | Description |
|------|------|-------------|
| `NAMESPACE_DNS` | `UUID` | The namespace UUID for fully qualified domain names. |
| `NAMESPACE_URL` | `UUID` | The namespace UUID for URLs. |
| `NAMESPACE_OID` | `UUID` | The namespace UUID for ISO object identifiers. |
| `NAMESPACE_X500` | `UUID` | The namespace UUID for X.500 distinguished names. |

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
| `int` | `int64` | Get the leading 64 bits of the UUID as an integer. |
| `uuid_bytes` | `bytes` | Get the UUID as 16 RFC 4122 bytes. |
| `version` | `int` | Get the UUID version number. |
| `variant` | `str` | Get the UUID variant description. |
| `urn` | `str` | Get the UUID as a URN string. |
| `time_low` | `int64` | Get the low 32 bits of the time field. |
| `time_mid` | `int` | Get the middle 16 bits of the time field. |
| `time_hi_version` | `int` | Get the high time field including the version bits. |
| `clock_seq_hi_variant` | `int` | Get the high clock sequence byte including the variant bits. |
| `clock_seq_low` | `int` | Get the low clock sequence byte. |
| `node` | `int64` | Get the 48-bit node value. |

### `__str__() -> str`

`repr()` uses the same method. Return the canonical hexadecimal string representation of the UUID.
