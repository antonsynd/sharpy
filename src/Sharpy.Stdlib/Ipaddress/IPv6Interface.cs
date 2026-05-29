using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv6 interface (an address with a network prefix).
    /// Combines an IPv6Address with network information.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Interface
    {
        private readonly IPv6Address _address;
        private readonly IPv6Network _network;

        /// <summary>The IP version number (6).</summary>
        public int Version => 6;

        /// <summary>The IP address of this interface.</summary>
        public IPv6Address Ip => _address;

        /// <summary>The network this interface belongs to.</summary>
        public IPv6Network Network => _network;

        /// <summary>The prefix length.</summary>
        public int Prefixlen => _network.Prefixlen;

        internal IPv6Interface(IPAddress address, int prefixLength)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv6 address");
            }

            if (prefixLength < 0 || prefixLength > 128)
            {
                throw new ValueError($"'{prefixLength}' is not a valid prefix length");
            }

            _address = new IPv6Address(address);
            _network = new IPv6Network(address, prefixLength, false);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{_address}/{_network.Prefixlen}";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _address.GetHashCode() ^ _network.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is IPv6Interface other)
            {
                return _address.Equals(other._address) && _network.Equals(other._network);
            }

            return false;
        }
    }
}
