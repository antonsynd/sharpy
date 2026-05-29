using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv4 address. Wraps <see cref="System.Net.IPAddress"/>.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Address : IComparable<IPv4Address>, IEquatable<IPv4Address>
    {
        private readonly IPAddress _address;

        /// <summary>The IP version number (4).</summary>
        public int Version => 4;

        /// <summary>The maximum prefix length for an IPv4 address (32).</summary>
        public int MaxPrefixlen => 32;

        /// <summary>Whether this is a private address (RFC 1918).</summary>
        public bool IsPrivate
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                // 10.0.0.0/8
                if (bytes[0] == 10) return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                return false;
            }
        }

        /// <summary>Whether this is a loopback address (127.0.0.0/8).</summary>
        public bool IsLoopback => IPAddress.IsLoopback(_address);

        /// <summary>Whether this is a multicast address (224.0.0.0/4).</summary>
        public bool IsMulticast
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                return bytes[0] >= 224 && bytes[0] <= 239;
            }
        }

        /// <summary>Whether this is a link-local address (169.254.0.0/16).</summary>
        public bool IsLinkLocal
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                return bytes[0] == 169 && bytes[1] == 254;
            }
        }

        /// <summary>Whether this is the unspecified address (0.0.0.0).</summary>
        public bool IsUnspecified => _address.Equals(IPAddress.Any);

        /// <summary>Whether this is a reserved address.</summary>
        public bool IsReserved
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                // 240.0.0.0/4 (excluding 255.255.255.255)
                return bytes[0] >= 240 && !_address.Equals(IPAddress.Broadcast);
            }
        }

        /// <summary>The integer representation of the address.</summary>
        public long Packed
        {
            get
            {
                byte[] bytes = _address.GetAddressBytes();
                return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
            }
        }

        internal IPAddress Address => _address;

        /// <summary>
        /// Creates an IPv4Address from a <see cref="System.Net.IPAddress"/>.
        /// </summary>
        internal IPv4Address(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 address");
            }

            _address = address;
        }

        /// <summary>
        /// Creates an IPv4Address from a string representation.
        /// </summary>
        /// <param name="address">The string representation of the IPv4 address.</param>
        public IPv4Address(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 address");
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
            if (obj is IPv4Address other)
            {
                return _address.Equals(other._address);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(IPv4Address? other)
        {
            if (other == null) return false;
            return _address.Equals(other._address);
        }

        /// <inheritdoc/>
        public int CompareTo(IPv4Address? other)
        {
            if (other == null) return 1;
            long thisVal = Packed;
            long otherVal = other.Packed;
            return thisVal.CompareTo(otherVal);
        }
    }
}
