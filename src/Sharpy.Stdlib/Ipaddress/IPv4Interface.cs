using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv4 interface (an address with a network prefix).
    /// Combines an IPv4Address with network information.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Interface
    {
        private readonly IPv4Address _address;
        private readonly IPv4Network _network;

        /// <summary>The IP version number (4).</summary>
        public int Version => 4;

        /// <summary>The IP address of this interface.</summary>
        public IPv4Address Ip => _address;

        /// <summary>The network this interface belongs to.</summary>
        public IPv4Network Network => _network;

        /// <summary>The prefix length.</summary>
        public int Prefixlen => _network.Prefixlen;

        internal IPv4Interface(IPAddress address, int prefixLength)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ValueError($"'{address}' does not appear to be an IPv4 address");
            }

            if (prefixLength < 0 || prefixLength > 32)
            {
                throw new ValueError($"'{prefixLength}' is not a valid prefix length");
            }

            _address = new IPv4Address(address);
            _network = new IPv4Network(address, prefixLength, false);
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
            if (obj is IPv4Interface other)
            {
                return _address.Equals(other._address) && _network.Equals(other._network);
            }

            return false;
        }
    }
}
