using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv6 address. Wraps <see cref="System.Net.IPAddress"/>.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Address : IComparable<IPv6Address>, IEquatable<IPv6Address>
    {
        private readonly IPAddress _address;

        /// <summary>The IP version number (6).</summary>
        public int Version => 6;

        /// <summary>The maximum prefix length for an IPv6 address (128).</summary>
        public int MaxPrefixlen => 128;

        /// <summary>Whether this is a private address (unique local fc00::/7).</summary>
        public bool IsPrivate
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                // fc00::/7
                return (bytes[0] & 0xFE) == 0xFC;
            }
        }

        /// <summary>Whether this is a loopback address (::1).</summary>
        public bool IsLoopback => IPAddress.IsLoopback(_address);

        /// <summary>Whether this is a multicast address (ff00::/8).</summary>
        public bool IsMulticast
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                return bytes[0] == 0xFF;
            }
        }

        /// <summary>Whether this is a link-local address (fe80::/10).</summary>
        public bool IsLinkLocal
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                return bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80;
            }
        }

        /// <summary>Whether this is the unspecified address (::).</summary>
        public bool IsUnspecified => _address.Equals(IPAddress.IPv6None);

        /// <summary>Whether this is a reserved address.</summary>
        public bool IsReserved
        {
            get
            {
                // Simplified: documentation prefix 2001:db8::/32
                byte[] bytes = _address.GetAddressBytes();
                return bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8;
            }
        }

        internal IPAddress Address => _address;

        /// <summary>
        /// Creates an IPv6Address from a <see cref="System.Net.IPAddress"/>.
        /// </summary>
        internal IPv6Address(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv6 address");
            }

            _address = address;
        }

        /// <summary>
        /// Creates an IPv6Address from a string representation.
        /// </summary>
        /// <param name="address">The string representation of the IPv6 address.</param>
        public IPv6Address(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv6 address");
            }

            _address = parsed;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return _address.ToString();
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _address.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is IPv6Address other)
            {
                return _address.Equals(other._address);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(IPv6Address? other)
        {
            if (other == null) return false;
            return _address.Equals(other._address);
        }

        /// <inheritdoc/>
        public int CompareTo(IPv6Address? other)
        {
            if (other == null) return 1;
            byte[] thisBytes = _address.GetAddressBytes();
            byte[] otherBytes = other._address.GetAddressBytes();
            for (int i = 0; i < thisBytes.Length; i++)
            {
                int cmp = thisBytes[i].CompareTo(otherBytes[i]);
                if (cmp != 0) return cmp;
            }

            return 0;
        }
    }
}
