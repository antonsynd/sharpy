using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv4 interface with an address and network.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Interface
    {
        /// <summary>
        /// Gets the interface address.
        /// </summary>
        public IPv4Address Ip { get; }
        /// <summary>
        /// Gets the associated network.
        /// </summary>
        public IPv4Network Network { get; }

        /// <summary>
        /// Gets the IP version number.
        /// </summary>
        public int Version => 4;
        /// <summary>
        /// Gets the interface prefix length.
        /// </summary>
        public int Prefixlen => Network.Prefixlen;
        /// <summary>
        /// Gets the interface in address/prefix notation.
        /// </summary>
        public string WithPrefixlen => Ip + "/" + Network.Prefixlen;
        /// <summary>
        /// Gets the interface in address/netmask notation.
        /// </summary>
        public string WithNetmask => Ip + "/" + Network.Netmask;
        /// <summary>
        /// Gets the interface in address/hostmask notation.
        /// </summary>
        public string WithHostmask => Ip + "/" + Network.Hostmask;

        /// <summary>
        /// Initializes an IPv4 interface from its string representation.
        /// </summary>
        public IPv4Interface(string address)
        {
            int slashIdx = address.IndexOf('/');
            if (slashIdx < 0)
            {
                Ip = new IPv4Address(address);
                Network = new IPv4Network(address + "/32", strict: false);
                return;
            }

            string addrPart = address.Substring(0, slashIdx);
            string prefixPart = address.Substring(slashIdx + 1);

            Ip = new IPv4Address(addrPart);
            Network = new IPv4Network(address, strict: false);
        }

        /// <summary>
        /// Returns the address/prefix string form of the interface.
        /// </summary>
        public override string ToString() => Ip + "/" + Network.Prefixlen;

        /// <summary>
        /// Returns a hash code for the interface.
        /// </summary>
        public override int GetHashCode() => Ip.GetHashCode() ^ Network.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is the same IPv4 interface.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is IPv4Interface other && Ip.Equals(other.Ip) && Network.Equals(other.Network);
        }
    }

    /// <summary>
    /// Represents an IPv6 interface with an address and network.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Interface
    {
        /// <summary>
        /// Gets the interface address.
        /// </summary>
        public IPv6Address Ip { get; }
        /// <summary>
        /// Gets the associated network.
        /// </summary>
        public IPv6Network Network { get; }

        /// <summary>
        /// Gets the IP version number.
        /// </summary>
        public int Version => 6;
        /// <summary>
        /// Gets the interface prefix length.
        /// </summary>
        public int Prefixlen => Network.Prefixlen;
        /// <summary>
        /// Gets the interface in address/prefix notation.
        /// </summary>
        public string WithPrefixlen => Ip + "/" + Network.Prefixlen;
        /// <summary>
        /// Gets the interface in address/netmask notation.
        /// </summary>
        public string WithNetmask => Ip + "/" + Network.Netmask;

        /// <summary>
        /// Initializes an IPv6 interface from its string representation.
        /// </summary>
        public IPv6Interface(string address)
        {
            int slashIdx = address.IndexOf('/');
            if (slashIdx < 0)
            {
                Ip = new IPv6Address(address);
                Network = new IPv6Network(address + "/128", strict: false);
                return;
            }

            string addrPart = address.Substring(0, slashIdx);
            Ip = new IPv6Address(addrPart);
            Network = new IPv6Network(address, strict: false);
        }

        /// <summary>
        /// Returns the address/prefix string form of the interface.
        /// </summary>
        public override string ToString() => Ip + "/" + Network.Prefixlen;

        /// <summary>
        /// Returns a hash code for the interface.
        /// </summary>
        public override int GetHashCode() => Ip.GetHashCode() ^ Network.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is the same IPv6 interface.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is IPv6Interface other && Ip.Equals(other.Ip) && Network.Equals(other.Network);
        }
    }
}
