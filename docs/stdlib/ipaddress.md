# ipaddress

```python
import ipaddress
```

## Functions

### `ipaddress.ip_address(address: str) -> object`

### `ipaddress.ip_network(address: str, strict: bool = true) -> object`

### `ipaddress.ip_interface(address: str) -> object`

### `ipaddress.collapse_addresses(addresses: SCG.List[object]) -> SCG.List[object]`

### `ipaddress.summarize_address_range(first: object, last: object) -> SCG.List[object]`

## IPv4Address

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` |  |
| `max_prefixlen` | `int` |  |
| `is_global` | `bool` |  |
| `is_unspecified` | `bool` |  |

### `to_int() -> long`

## IPv4Network

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` |  |
| `prefixlen` | `int` |  |
| `max_prefixlen` | `int` |  |
| `is_private` | `bool` |  |
| `is_loopback` | `bool` |  |
| `is_multicast` | `bool` |  |
| `is_reserved` | `bool` |  |
| `is_link_local` | `bool` |  |
| `is_global` | `bool` |  |

### `hosts() -> SCG.IEnumerable[IPv4Address]`

### `contains(address: IPv4Address) -> bool`

### `overlaps(other: IPv4Network) -> bool`

### `subnets(prefixlen_diff: int = 1, new_prefix: int? = null) -> SCG.List[IPv4Network]`

### `supernet(prefixlen_diff: int = 1, new_prefix: int? = null) -> IPv4Network`

### `subnet_of(other: IPv4Network) -> bool`

### `supernet_of(other: IPv4Network) -> bool`

## IPv6Address

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` |  |
| `max_prefixlen` | `int` |  |
| `is_multicast` | `bool` |  |
| `is_global` | `bool` |  |

### `to_int() -> BigInteger`

## IPv6Network

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` |  |
| `prefixlen` | `int` |  |
| `max_prefixlen` | `int` |  |
| `is_private` | `bool` |  |
| `is_loopback` | `bool` |  |
| `is_multicast` | `bool` |  |
| `is_reserved` | `bool` |  |
| `is_link_local` | `bool` |  |
| `is_global` | `bool` |  |
| `with_prefixlen` | `str` |  |
| `with_netmask` | `str` |  |
| `with_hostmask` | `str` |  |

### `contains(address: IPv6Address) -> bool`

### `overlaps(other: IPv6Network) -> bool`

### `hosts() -> Iterable[IPv6Address]`

### `subnets(prefixlen_diff: int = 1, new_prefix: int? = null) -> list[IPv6Network]`

### `supernet(prefixlen_diff: int = 1, new_prefix: int? = null) -> IPv6Network`

### `subnet_of(other: IPv6Network) -> bool`

### `supernet_of(other: IPv6Network) -> bool`

## IPv4Interface

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ip` | `IPv4Address` |  |
| `network` | `IPv4Network` |  |
| `version` | `int` |  |
| `prefixlen` | `int` |  |
| `with_prefixlen` | `str` |  |
| `with_netmask` | `str` |  |
| `with_hostmask` | `str` |  |

## IPv6Interface

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ip` | `IPv6Address` |  |
| `network` | `IPv6Network` |  |
| `version` | `int` |  |
| `prefixlen` | `int` |  |
| `with_prefixlen` | `str` |  |
| `with_netmask` | `str` |  |
