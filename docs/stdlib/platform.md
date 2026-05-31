# platform

Represents the result of a platform.uname() call.
Mirrors Python's platform.uname_result named tuple.

```python
import platform
```

## Functions

### `platform.system() -> str`

Returns the system/OS name, e.g., "Windows", "Linux", "Darwin".

### `platform.release() -> str`

Returns the system's release version, e.g., "10.0.19041".

### `platform.version() -> str`

Returns the system's release version description.

### `platform.machine() -> str`

Returns the machine type, e.g., "x86_64", "AMD64", "arm64".
Follows Python's platform.machine() conventions per OS.

### `platform.node() -> str`

Returns the computer's network name (hostname).

### `platform.processor() -> str`

Returns the (real) processor name or architecture string.

### `platform.platform(aliased: bool = false, terse: bool = false) -> str`

Returns a single string identifying the underlying platform
with as much useful information as possible.

**Parameters:**

- `aliased` (bool) -- If true, use aliased platform names (currently unused).
- `terse` (bool) -- If true, return a minimal platform string without version.

### `platform.sharpy_version() -> str`

Returns the Sharpy runtime version string.

### `platform.dotnet_version() -> str`

Returns the .NET runtime version string.

### `platform.dotnet_implementation() -> str`

Returns the name of the .NET implementation (always "CoreCLR" on .NET 5+).

### `platform.dotnet_compiler() -> str`

Returns the .NET framework description string.

### `platform.uname() -> UnameResult`

Returns a `UnameResult` containing system identification information.

## uname_result

Represents the result of a platform.uname() call.
Mirrors Python's platform.uname_result named tuple.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `system` | `str` | The operating system name (e.g., "Windows", "Linux", "Darwin"). |
| `node` | `str` | The network name of the machine (hostname). |
| `release` | `str` | The operating system release version. |
| `version` | `str` | The operating system version description. |
| `machine` | `str` | The hardware machine identifier (e.g., "x86_64", "arm64"). |
