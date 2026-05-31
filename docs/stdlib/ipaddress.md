# ipaddress

IPv4/IPv6 address, network, and interface manipulation, mirroring Python's
`ipaddress` module. Wraps `System.Net.IPAddress` with a Python-compatible API.

```python
import ipaddress
```

## Factory functions

These auto-detect the address family and return the appropriate IPv4 or IPv6 type.
The return type is `object` (Sharpy union types are a future feature) — callers use
pattern matching to narrow.

| Function | Description |
|----------|-------------|
| `ip_address(address: str) -> object` | Returns `IPv4Address` or `IPv6Address`. Raises `ValueError` on invalid input. |
| `ip_network(address: str, strict: bool = True) -> object` | Returns `IPv4Network` or `IPv6Network`. |
| `ip_interface(address: str) -> object` | Returns `IPv4Interface` or `IPv6Interface`. |
| `collapse_addresses(addresses: list[object]) -> list[object]` | Merge adjacent/overlapping networks into a minimal set. Mixed versions raise `TypeError`. |
| `summarize_address_range(first: object, last: object) -> list[object]` | Minimal set of networks spanning a range. `first`/`last` must be the same version. |

## Address types

`IPv4Address` and `IPv6Address` wrap a single address.

**Construction:** from a string, an integer (`long` for IPv4, `BigInteger` for IPv6),
or `Bytes` (4 bytes for IPv4, 16 for IPv6). Invalid input raises `ValueError`.

**Properties:** `version`, `max_prefixlen`, `is_private`, `is_loopback`, `is_multicast`,
`is_reserved`, `is_link_local`, `is_global`, `is_unspecified`, `packed`, `compressed`.
`IPv6Address` adds `is_site_local`, `exploded`, and `ipv4_mapped` (extracts the IPv4
address from `::ffff:x.x.x.x`, or `None`).

**Methods/operators:** `to_int()`, value equality, comparison (`<`, `>`, `<=`, `>=`,
`==`, `!=`), and arithmetic (`addr + n`, `addr - n`). Arithmetic that overflows the
address space raises `ValueError`.

## Network types

`IPv4Network` and `IPv6Network` represent a network in CIDR notation and are iterable
(`IEnumerable` over every address).

**Construction:** `IPv4Network("192.168.1.0/24")`. With `strict=True` (default), host
bits being set raises `ValueError`; with `strict=False`, host bits are masked off. A
bare address with no prefix is treated as a host route (`/32` or `/128`).

**Properties:** `network_address`, `broadcast_address`, `netmask`, `hostmask`,
`prefixlen`, `num_addresses` (`long` for IPv4 so a `/0` network's 4 294 967 296
addresses fit; `BigInteger` for IPv6), `with_prefixlen`, `with_netmask`,
`with_hostmask`, and the same `is_*` classification properties as the address types.

**Methods:** `hosts()` (usable hosts — excludes the network and broadcast addresses
for IPv4 prefixes `<= /30`, and the Subnet-Router anycast for IPv6 prefixes `< /127`;
`/31`, `/32`, `/127`, and `/128` yield all addresses), `contains(address)`,
`overlaps(other)`, `subnets(prefixlen_diff=1, new_prefix=None)`,
`supernet(prefixlen_diff=1, new_prefix=None)`, `subnet_of(other)`, `supernet_of(other)`.

## Interface types

`IPv4Interface` and `IPv6Interface` combine a host address with its network
(`"192.168.1.1/24"`). They expose `ip` (the host address, host bits preserved),
`network` (host bits masked), and `with_prefixlen` / `with_netmask` / `with_hostmask`
formatted with the interface address.

## Differences from Python

- **Network classification is based on the network address only.** A network's
  `is_private`, `is_global`, etc. reflect the classification of its
  `network_address`, not the full address range. For networks that straddle a
  classification boundary (e.g. `10.0.0.0/7`, which spans `10.x` private space and
  `11.x` public space) the result therefore differs from Python, which inspects the
  whole range. This is a deliberate simplification kept consistent across
  `IPv4Network` and `IPv6Network`; the range-aware fix is tracked in
  [issue #791](https://github.com/antonsynd/sharpy/issues/791). Per-address
  classification (`ip_address(...).is_private`) matches Python.
- `is_private` covers the RFC 1918 ranges (`10/8`, `172.16/12`, `192.168/16`) for
  IPv4; Python 3.11+ additionally treats several other RFC ranges (e.g. `100.64/10`
  CGNAT) as private. Also tracked in
  [issue #791](https://github.com/antonsynd/sharpy/issues/791).
