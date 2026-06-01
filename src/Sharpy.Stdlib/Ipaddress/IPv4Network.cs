using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv4 network.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Network : IEnumerable<IPv4Address>, IEquatable<IPv4Network>, IComparable<IPv4Network>
    {
        private readonly uint _networkAddress;
        private readonly int _prefixLength;

        /// <summary>
        /// Gets the IP version number.
        /// </summary>
        public int Version => 4;
        /// <summary>
        /// Gets the network prefix length.
        /// </summary>
        public int Prefixlen => _prefixLength;
        /// <summary>
        /// Gets the maximum prefix length for IPv4 networks.
        /// </summary>
        public int MaxPrefixlen => 32;

        /// <summary>
        /// Gets the network address.
        /// </summary>
        public IPv4Address NetworkAddress => new IPv4Address(_networkAddress);

        /// <summary>
        /// Gets the broadcast address.
        /// </summary>
        public IPv4Address BroadcastAddress
        {
            get
            {
                uint hostMask = HostMaskBits();
                return new IPv4Address(_networkAddress | hostMask);
            }
        }

        /// <summary>
        /// Gets the network mask.
        /// </summary>
        public IPv4Address Netmask
        {
            get
            {
                uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
                return new IPv4Address(mask);
            }
        }

        /// <summary>
        /// Gets the host mask.
        /// </summary>
        public IPv4Address Hostmask
        {
            get
            {
                return new IPv4Address(HostMaskBits());
            }
        }

        /// <summary>
        /// Gets the number of addresses in the network.
        /// </summary>
        public long NumAddresses => 1L << (32 - _prefixLength);

        // Host-mask bits with explicit /0 and /32 guards. C# masks uint shift counts
        // mod 32, so `0xFFFFFFFFU << 32` would wrongly evaluate to 0xFFFFFFFF (<< 0).
        private uint HostMaskBits()
        {
            if (_prefixLength == 0)
                return 0xFFFFFFFFU;
            if (_prefixLength == 32)
                return 0;
            return ~(0xFFFFFFFFU << (32 - _prefixLength));
        }

        /// <summary>
        /// Gets whether the network is in a private-use range.
        /// </summary>
        public bool IsPrivate
        {
            get
            {
                uint netAddr = _networkAddress;
                uint bcastAddr = _networkAddress | HostMaskBits();
                foreach (var (network, prefix) in IPv4Address.PrivateNetworks)
                {
                    if (IPv4Address.InRange(netAddr, network, prefix) &&
                        IPv4Address.InRange(bcastAddr, network, prefix))
                    {
                        if (!IPv4Address.InAnyException(netAddr) && !IPv4Address.InAnyException(bcastAddr))
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets whether the network is globally reachable.
        /// </summary>
        public bool IsGlobal
        {
            get
            {
                uint netAddr = _networkAddress;
                uint bcastAddr = _networkAddress | HostMaskBits();
                if (IPv4Address.InRange(netAddr, IPv4Address.PublicNetwork, IPv4Address.PublicNetworkPrefix) ||
                    IPv4Address.InRange(bcastAddr, IPv4Address.PublicNetwork, IPv4Address.PublicNetworkPrefix))
                    return false;
                return !IsPrivate;
            }
        }

        /// <summary>
        /// Gets whether the network is in a reserved range.
        /// </summary>
        public bool IsReserved
        {
            get
            {
                uint netAddr = _networkAddress;
                uint bcastAddr = _networkAddress | HostMaskBits();
                return IPv4Address.InRange(netAddr, IPv4Address.ReservedNetwork, IPv4Address.ReservedNetworkPrefix) &&
                       IPv4Address.InRange(bcastAddr, IPv4Address.ReservedNetwork, IPv4Address.ReservedNetworkPrefix);
            }
        }

        /// <summary>
        /// Gets whether the network is a loopback network.
        /// </summary>
        public bool IsLoopback => NetworkAddress.IsLoopback;
        /// <summary>
        /// Gets whether the network is a multicast network.
        /// </summary>
        public bool IsMulticast => NetworkAddress.IsMulticast;
        /// <summary>
        /// Gets whether the network is link-local.
        /// </summary>
        public bool IsLinkLocal => NetworkAddress.IsLinkLocal;

        /// <summary>
        /// Gets the network in address/prefix notation.
        /// </summary>
        public string WithPrefixlen => _toIpString(_networkAddress) + "/" + _prefixLength;
        /// <summary>
        /// Gets the network in address/netmask notation.
        /// </summary>
        public string WithNetmask => _toIpString(_networkAddress) + "/" + Netmask;
        /// <summary>
        /// Gets the network in address/hostmask notation.
        /// </summary>
        public string WithHostmask => _toIpString(_networkAddress) + "/" + Hostmask;

        /// <summary>
        /// Initializes an IPv4 network from its CIDR notation.
        /// </summary>
        public IPv4Network(string address, bool strict = true)
        {
            int slashIdx = address.IndexOf('/');
            string addrPart;
            int prefix;

            if (slashIdx >= 0)
            {
                addrPart = address.Substring(0, slashIdx);
                if (!int.TryParse(address.Substring(slashIdx + 1), out prefix))
                {
                    throw new ValueError("'" + address + "' is not a valid IPv4 network");
                }
            }
            else
            {
                addrPart = address;
                prefix = 32;
            }

            if (!IPAddress.TryParse(addrPart, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv4 network");
            }

            if (prefix < 0 || prefix > 32)
            {
                throw new ValueError("'" + prefix + "' is not a valid prefix length");
            }

            uint addr = IPv4Address.BytesToUint(parsed.GetAddressBytes());
            uint mask = prefix == 0 ? 0 : 0xFFFFFFFFU << (32 - prefix);
            uint networkBits = addr & mask;

            if (strict && networkBits != addr)
            {
                throw new ValueError("'" + address + "' has host bits set");
            }

            _networkAddress = networkBits;
            _prefixLength = prefix;
        }

        internal IPv4Network(uint networkAddress, int prefixLength)
        {
            _networkAddress = networkAddress;
            _prefixLength = prefixLength;
        }

        /// <summary>
        /// Iterates over usable host addresses in the network.
        /// </summary>
        public IEnumerable<IPv4Address> Hosts()
        {
            if (_prefixLength == 32)
            {
                yield return new IPv4Address(_networkAddress);
                yield break;
            }

            if (_prefixLength == 31)
            {
                yield return new IPv4Address(_networkAddress);
                yield return new IPv4Address(_networkAddress + 1);
                yield break;
            }

            uint hostMask = HostMaskBits();
            for (uint i = 1; i < hostMask; i++)
            {
                yield return new IPv4Address(_networkAddress + i);
            }
        }

        /// <summary>
        /// Iterates over all addresses in the network.
        /// </summary>
        public IEnumerator<IPv4Address> GetEnumerator()
        {
            long count = NumAddresses;
            for (long i = 0; i < count; i++)
            {
                yield return new IPv4Address((uint)(_networkAddress + i));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Determines whether the network contains the specified address.
        /// </summary>
        public bool Contains(IPv4Address address)
        {
            uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
            return (address.Value & mask) == _networkAddress;
        }

        /// <summary>
        /// Determines whether this network overlaps another network.
        /// </summary>
        public bool Overlaps(IPv4Network other)
        {
            return Contains(other.NetworkAddress) || Contains(other.BroadcastAddress) ||
                   other.Contains(NetworkAddress) || other.Contains(BroadcastAddress);
        }

        /// <summary>
        /// Splits the network into subnets.
        /// </summary>
        public List<IPv4Network> Subnets(int prefixlenDiff = 1, int? newPrefix = null)
        {
            int targetPrefix;
            if (newPrefix != null)
            {
                targetPrefix = newPrefix.Value;
                if (targetPrefix <= _prefixLength || targetPrefix > 32)
                {
                    throw new ValueError("new prefix must be longer");
                }
            }
            else
            {
                targetPrefix = _prefixLength + prefixlenDiff;
                if (targetPrefix > 32)
                {
                    throw new ValueError("prefix length diff too large");
                }
            }

            var result = new System.Collections.Generic.List<IPv4Network>();
            long count = 1L << (targetPrefix - _prefixLength);
            uint subnetSize = 1U << (32 - targetPrefix);

            for (long i = 0; i < count; i++)
            {
                result.Add(new IPv4Network(_networkAddress + (uint)i * subnetSize, targetPrefix));
            }

            return new List<IPv4Network>(result);
        }

        /// <summary>
        /// Returns the containing supernet.
        /// </summary>
        public IPv4Network Supernet(int prefixlenDiff = 1, int? newPrefix = null)
        {
            int targetPrefix;
            if (newPrefix != null)
            {
                targetPrefix = newPrefix.Value;
            }
            else
            {
                targetPrefix = _prefixLength - prefixlenDiff;
            }

            if (targetPrefix < 0)
            {
                throw new ValueError("prefix length is too small");
            }

            uint mask = targetPrefix == 0 ? 0 : 0xFFFFFFFFU << (32 - targetPrefix);
            return new IPv4Network(_networkAddress & mask, targetPrefix);
        }

        /// <summary>
        /// Determines whether this network is a subnet of another network.
        /// </summary>
        public bool SubnetOf(IPv4Network other)
        {
            return other._prefixLength < _prefixLength &&
                   other.Contains(NetworkAddress) && other.Contains(BroadcastAddress);
        }

        /// <summary>
        /// Determines whether this network is a supernet of another network.
        /// </summary>
        public bool SupernetOf(IPv4Network other)
        {
            return other.SubnetOf(this);
        }

        /// <summary>
        /// Returns the CIDR string form of the network.
        /// </summary>
        public override string ToString() => _toIpString(_networkAddress) + "/" + _prefixLength;

        /// <summary>
        /// Returns a hash code for the network.
        /// </summary>
        public override int GetHashCode() => _networkAddress.GetHashCode() ^ _prefixLength;

        /// <summary>
        /// Determines whether the specified object is the same IPv4 network.
        /// </summary>
        public override bool Equals(object? obj) => obj is IPv4Network other && Equals(other);

        /// <summary>
        /// Determines whether the specified network is the same IPv4 network.
        /// </summary>
        public bool Equals(IPv4Network? other)
        {
            if (other == null)
                return false;
            return _networkAddress == other._networkAddress && _prefixLength == other._prefixLength;
        }

        /// <summary>
        /// Compares this network with another IPv4 network.
        /// </summary>
        public int CompareTo(IPv4Network? other)
        {
            if (other == null)
                return 1;
            int cmp = _networkAddress.CompareTo(other._networkAddress);
            if (cmp != 0)
                return cmp;
            return _prefixLength.CompareTo(other._prefixLength);
        }

        /// <summary>
        /// Determines whether one IPv4 network sorts before another.
        /// </summary>
        public static bool operator <(IPv4Network left, IPv4Network right) => left.CompareTo(right) < 0;
        /// <summary>
        /// Determines whether one IPv4 network sorts after another.
        /// </summary>
        public static bool operator >(IPv4Network left, IPv4Network right) => left.CompareTo(right) > 0;
        /// <summary>
        /// Determines whether one IPv4 network sorts before or the same as another.
        /// </summary>
        public static bool operator <=(IPv4Network left, IPv4Network right) => left.CompareTo(right) <= 0;
        /// <summary>
        /// Determines whether one IPv4 network sorts after or the same as another.
        /// </summary>
        public static bool operator >=(IPv4Network left, IPv4Network right) => left.CompareTo(right) >= 0;
        /// <summary>
        /// Determines whether two IPv4 networks are equal.
        /// </summary>
        public static bool operator ==(IPv4Network? left, IPv4Network? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }
        /// <summary>
        /// Determines whether two IPv4 networks are not equal.
        /// </summary>
        public static bool operator !=(IPv4Network? left, IPv4Network? right) => !(left == right);

        private static string _toIpString(uint value)
        {
            return ((value >> 24) & 0xFF) + "." +
                   ((value >> 16) & 0xFF) + "." +
                   ((value >> 8) & 0xFF) + "." +
                   (value & 0xFF);
        }
    }
}
