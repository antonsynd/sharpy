using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv6 network. Provides network/host enumeration and membership testing.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Network : IEquatable<IPv6Network>
    {
        private readonly IPAddress _networkAddress;
        private readonly int _prefixLength;

        /// <summary>The IP version number (6).</summary>
        public int Version => 6;

        /// <summary>The prefix length of this network.</summary>
        public int Prefixlen => _prefixLength;

        /// <summary>The maximum prefix length for IPv6 (128).</summary>
        public int MaxPrefixlen => 128;

        /// <summary>The network address.</summary>
        public IPv6Address NetworkAddress => new IPv6Address(_networkAddress);

        /// <summary>The netmask for this network.</summary>
        public IPv6Address Netmask
        {
            get
            {
                byte[] mask = new byte[16];
                int fullBytes = _prefixLength / 8;
                int remainBits = _prefixLength % 8;

                for (int i = 0; i < fullBytes && i < 16; i++)
                {
                    mask[i] = 0xFF;
                }

                if (fullBytes < 16 && remainBits > 0)
                {
                    mask[fullBytes] = (byte)(0xFF << (8 - remainBits));
                }

                return new IPv6Address(new IPAddress(mask));
            }
        }

        /// <summary>The number of addresses in this network.</summary>
        public long NumAddresses
        {
            get
            {
                int hostBits = 128 - _prefixLength;
                if (hostBits >= 63)
                {
                    return long.MaxValue; // too large to represent
                }

                return 1L << hostBits;
            }
        }

        internal IPv6Network(IPAddress networkAddress, int prefixLength, bool strict)
        {
            if (prefixLength < 0 || prefixLength > 128)
            {
                throw new ValueError($"'{prefixLength}' is not a valid prefix length");
            }

            byte[] bytes = networkAddress.GetAddressBytes();
            byte[] masked = ApplyMask(bytes, prefixLength);

            if (strict)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (bytes[i] != masked[i])
                    {
                        throw new ValueError($"'{networkAddress}/{prefixLength}' has host bits set");
                    }
                }
            }

            _networkAddress = new IPAddress(masked);
            _prefixLength = prefixLength;
        }

        /// <summary>
        /// Check whether an address is contained in this network.
        /// </summary>
        /// <param name="address">The address to test.</param>
        /// <returns>True if the address is part of this network.</returns>
        public bool Contains(IPv6Address address)
        {
            byte[] addrBytes = address.Address.GetAddressBytes();
            byte[] masked = ApplyMask(addrBytes, _prefixLength);
            byte[] netBytes = _networkAddress.GetAddressBytes();

            for (int i = 0; i < 16; i++)
            {
                if (masked[i] != netBytes[i]) return false;
            }

            return true;
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
            if (obj is IPv6Network other)
            {
                return Equals(other);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(IPv6Network? other)
        {
            if (other == null) return false;
            return _networkAddress.Equals(other._networkAddress) && _prefixLength == other._prefixLength;
        }

        private static byte[] ApplyMask(byte[] bytes, int prefixLength)
        {
            byte[] result = new byte[16];
            int fullBytes = prefixLength / 8;
            int remainBits = prefixLength % 8;

            for (int i = 0; i < fullBytes && i < 16; i++)
            {
                result[i] = bytes[i];
            }

            if (fullBytes < 16 && remainBits > 0)
            {
                result[fullBytes] = (byte)(bytes[fullBytes] & (0xFF << (8 - remainBits)));
            }

            return result;
        }
    }
}
