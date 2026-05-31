# ipaddress

Functions to create and manipulate IPv4 and IPv6 addresses and networks.

```python
import ipaddress
```

## Functions

### `ipaddress.ip_address(address: str) -> object`

Parses a string as an IPv4 or IPv6 address.

### `ipaddress.ip_network(address: str, strict: bool = True) -> object`

Parses a string as an IPv4 or IPv6 network.

### `ipaddress.ip_interface(address: str) -> object`

Parses a string as an IPv4 or IPv6 interface.

### `ipaddress.collapse_addresses(addresses: SCG.List[object]) -> SCG.List[object]`

Collapses addresses and networks into the smallest set of CIDR blocks.

### `ipaddress.summarize_address_range(first: object, last: object) -> SCG.List[object]`

Summarizes an address range as the smallest set of CIDR blocks.

## IPv4Address

Represents an IPv4 address.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` | Gets the IP version number. |
| `max_prefixlen` | `int` | Gets the maximum prefix length for IPv4 addresses. |
| `is_unspecified` | `bool` | Gets whether the address is the unspecified address. |

### `to_int() -> long`

Returns the integer value of the address.

## IPv4Network

Represents an IPv4 network.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` | Gets the IP version number. |
| `prefixlen` | `int` | Gets the network prefix length. |
| `max_prefixlen` | `int` | Gets the maximum prefix length for IPv4 networks. |
| `is_loopback` | `bool` | Gets whether the network is a loopback network. |
| `is_multicast` | `bool` | Gets whether the network is a multicast network. |
| `is_link_local` | `bool` | Gets whether the network is link-local. |

### `hosts() -> SCG.IEnumerable[IPv4Address]`

Iterates over usable host addresses in the network.

### `contains(address: IPv4Address) -> bool`

Determines whether the network contains the specified address.

### `overlaps(other: IPv4Network) -> bool`

Determines whether this network overlaps another network.

### `subnets(prefixlen_diff: int = 1, new_prefix: int | None = None) -> SCG.List[IPv4Network]`

Splits the network into subnets.

### `supernet(prefixlen_diff: int = 1, new_prefix: int | None = None) -> IPv4Network`

Returns the containing supernet.

### `subnet_of(other: IPv4Network) -> bool`

Determines whether this network is a subnet of another network.

### `supernet_of(other: IPv4Network) -> bool`

Determines whether this network is a supernet of another network.

## IPv6Address

Represents an IPv6 address.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` | Gets the IP version number. |
| `max_prefixlen` | `int` | Gets the maximum prefix length for IPv6 addresses. |
| `is_multicast` | `bool` | Gets whether the address is a multicast address. |
| `is_global` | `bool` | Gets whether the address is globally reachable. |

### `to_int() -> BigInteger`

Returns the integer value of the address.

## IPv6Network

Represents an IPv6 network.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `version` | `int` | Gets the IP version number. |
| `prefixlen` | `int` | Gets the network prefix length. |
| `max_prefixlen` | `int` | Gets the maximum prefix length for IPv6 networks. |
| `is_global` | `bool` | Gets whether the network is globally reachable. |
| `is_loopback` | `bool` | Gets whether the network is a loopback network. |
| `is_multicast` | `bool` | Gets whether the network is a multicast network. |
| `is_reserved` | `bool` | Gets whether the network is in a reserved range. |
| `is_link_local` | `bool` | Gets whether the network is link-local. |
| `with_prefixlen` | `str` | Gets the network in address/prefix notation. |
| `with_netmask` | `str` | Gets the network in address/netmask notation. |
| `with_hostmask` | `str` | Gets the network in address/hostmask notation. |

### `contains(address: IPv6Address) -> bool`

Determines whether the network contains the specified address.

### `overlaps(other: IPv6Network) -> bool`

Determines whether this network overlaps another network.

### `hosts() -> Iterable[IPv6Address]`

Iterates over usable host addresses in the network.

### `subnets(prefixlen_diff: int = 1, new_prefix: int | None = None) -> list[IPv6Network]`

Splits the network into subnets.

### `supernet(prefixlen_diff: int = 1, new_prefix: int | None = None) -> IPv6Network`

Returns the containing supernet.

### `subnet_of(other: IPv6Network) -> bool`

Determines whether this network is a subnet of another network.

### `supernet_of(other: IPv6Network) -> bool`

Determines whether this network is a supernet of another network.

## IPv4Interface

Represents an IPv4 interface with an address and network.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ip` | `IPv4Address` | Gets the interface address. |
| `network` | `IPv4Network` | Gets the associated network. |
| `version` | `int` | Gets the IP version number. |
| `prefixlen` | `int` | Gets the interface prefix length. |
| `with_prefixlen` | `str` | Gets the interface in address/prefix notation. |
| `with_netmask` | `str` | Gets the interface in address/netmask notation. |
| `with_hostmask` | `str` | Gets the interface in address/hostmask notation. |

## IPv6Interface

Represents an IPv4 interface with an address and network.

### Properties

| Name | Type | Description |
|------|------|-------------|
| `ip` | `IPv6Address` | Gets the interface address. |
| `network` | `IPv6Network` | Gets the associated network. |
| `version` | `int` | Gets the IP version number. |
| `prefixlen` | `int` | Gets the interface prefix length. |
| `with_prefixlen` | `str` | Gets the interface in address/prefix notation. |
| `with_netmask` | `str` | Gets the interface in address/netmask notation. |
