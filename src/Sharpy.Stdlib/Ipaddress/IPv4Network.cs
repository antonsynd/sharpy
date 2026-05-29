using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv4 network. Provides network/host enumeration and membership testing.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Network : IEquatable<IPv4Network>
    {
        private readonly IPAddress _networkAddress;
        private readonly int _prefixLength;

        /// <summary>The IP version number (4).</summary>
        public int Version => 4;

        /// <summary>The prefix length of this network.</summary>
        public int Prefixlen => _prefixLength;

        /// <summary>The maximum prefix length for IPv4 (32).</summary>
        public int MaxPrefixlen => 32;

        /// <summary>The network address.</summary>
        public IPv4Address NetworkAddress => new IPv4Address(_networkAddress);

        /// <summary>The broadcast address for this network.</summary>
        public IPv4Address BroadcastAddress
        {
            get
            {
                byte[] netBytes = _networkAddress.GetAddressBytes();
                uint netInt = ToUint(netBytes);
                uint hostMask = _prefixLength == 32 ? 0 : ~(0xFFFFFFFFU << (32 - _prefixLength));
                uint broadcast = netInt | hostMask;
                return new IPv4Address(new IPAddress(FromUint(broadcast)));
            }
        }

        /// <summary>The netmask for this network.</summary>
        public IPv4Address Netmask
        {
            get
            {
                uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
                return new IPv4Address(new IPAddress(FromUint(mask)));
            }
        }

        /// <summary>The host mask (inverse of netmask).</summary>
        public IPv4Address Hostmask
        {
            get
            {
                uint mask = _prefixLength == 32 ? 0 : ~(0xFFFFFFFFU << (32 - _prefixLength));
                return new IPv4Address(new IPAddress(FromUint(mask)));
            }
        }

        /// <summary>The number of hosts in this network (excluding network and broadcast addresses).</summary>
        public long NumAddresses
        {
            get
            {
                long total = 1L << (32 - _prefixLength);
                return total;
            }
        }

        internal IPv4Network(IPAddress networkAddress, int prefixLength, bool strict)
        {
            if (prefixLength < 0 || prefixLength > 32)
            {
                throw new ValueError($"'{prefixLength}' is not a valid prefix length");
            }

            byte[] bytes = networkAddress.GetAddressBytes();
            uint addr = ToUint(bytes);
            uint mask = prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - prefixLength);
            uint networkBits = addr & mask;

            if (strict && networkBits != addr)
            {
                throw new ValueError($"'{networkAddress}/{prefixLength}' has host bits set");
            }

            _networkAddress = new IPAddress(FromUint(networkBits));
            _prefixLength = prefixLength;
        }

        /// <summary>
        /// Returns an enumerable of usable host addresses in this network
        /// (excludes network and broadcast addresses for /31 and larger).
        /// </summary>
        public List<IPv4Address> Hosts()
        {
            var result = new List<IPv4Address>();
            byte[] netBytes = _networkAddress.GetAddressBytes();
            uint netInt = ToUint(netBytes);

            if (_prefixLength == 32)
            {
                result.Add(new IPv4Address(_networkAddress));
                return result;
            }

            if (_prefixLength == 31)
            {
                // RFC 3021: point-to-point links, both addresses are usable
                result.Add(new IPv4Address(new IPAddress(FromUint(netInt))));
                result.Add(new IPv4Address(new IPAddress(FromUint(netInt + 1))));
                return result;
            }

            // Exclude network address (first) and broadcast address (last)
            uint hostMask = ~(0xFFFFFFFFU << (32 - _prefixLength));
            uint numHosts = hostMask - 1; // exclude network and broadcast

            for (uint i = 1; i <= numHosts; i++)
            {
                result.Add(new IPv4Address(new IPAddress(FromUint(netInt + i))));
            }

            return result;
        }

        /// <summary>
        /// Check whether an address is contained in this network.
        /// </summary>
        /// <param name="address">The address to test.</param>
        /// <returns>True if the address is part of this network.</returns>
        public bool Contains(IPv4Address address)
        {
            byte[] addrBytes = address.Address.GetAddressBytes();
            byte[] netBytes = _networkAddress.GetAddressBytes();
            uint addrInt = ToUint(addrBytes);
            uint netInt = ToUint(netBytes);
            uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
            return (addrInt & mask) == netInt;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{_networkAddress}/{_prefixLength}";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _networkAddress.GetHashCode() ^ _prefixLength;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is IPv4Network other)
            {
                return Equals(other);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(IPv4Network? other)
        {
            if (other == null) return false;
            return _networkAddress.Equals(other._networkAddress) && _prefixLength == other._prefixLength;
        }

        private static uint ToUint(byte[] bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static byte[] FromUint(uint value)
        {
            return new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }
    }
}
