using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using SCG = System.Collections.Generic;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Network : SCG.IEnumerable<IPv4Address>, IEquatable<IPv4Network>, IComparable<IPv4Network>
    {
        private readonly uint _networkAddress;
        private readonly int _prefixLength;

        public int Version => 4;
        public int Prefixlen => _prefixLength;
        public int MaxPrefixlen => 32;

        public IPv4Address NetworkAddress => new IPv4Address(_networkAddress);

        public IPv4Address BroadcastAddress
        {
            get
            {
                uint hostMask = _prefixLength == 32 ? 0 : ~(0xFFFFFFFFU << (32 - _prefixLength));
                return new IPv4Address(_networkAddress | hostMask);
            }
        }

        public IPv4Address Netmask
        {
            get
            {
                uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
                return new IPv4Address(mask);
            }
        }

        public IPv4Address Hostmask
        {
            get
            {
                uint mask = _prefixLength == 32 ? 0 : ~(0xFFFFFFFFU << (32 - _prefixLength));
                return new IPv4Address(mask);
            }
        }

        public long NumAddresses => 1L << (32 - _prefixLength);

        public bool IsPrivate => NetworkAddress.IsPrivate;
        public bool IsLoopback => NetworkAddress.IsLoopback;
        public bool IsMulticast => NetworkAddress.IsMulticast;
        public bool IsReserved => NetworkAddress.IsReserved;
        public bool IsLinkLocal => NetworkAddress.IsLinkLocal;
        public bool IsGlobal => NetworkAddress.IsGlobal;

        public string WithPrefixlen => _toIpString(_networkAddress) + "/" + _prefixLength;
        public string WithNetmask => _toIpString(_networkAddress) + "/" + Netmask;
        public string WithHostmask => _toIpString(_networkAddress) + "/" + Hostmask;

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

        public SCG.IEnumerable<IPv4Address> Hosts()
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

            uint hostMask = ~(0xFFFFFFFFU << (32 - _prefixLength));
            for (uint i = 1; i < hostMask; i++)
            {
                yield return new IPv4Address(_networkAddress + i);
            }
        }

        public SCG.IEnumerator<IPv4Address> GetEnumerator()
        {
            long count = NumAddresses;
            for (long i = 0; i < count; i++)
            {
                yield return new IPv4Address((uint)(_networkAddress + i));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Contains(IPv4Address address)
        {
            uint mask = _prefixLength == 0 ? 0 : 0xFFFFFFFFU << (32 - _prefixLength);
            return (address.Value & mask) == _networkAddress;
        }

        public bool Overlaps(IPv4Network other)
        {
            return Contains(other.NetworkAddress) || Contains(other.BroadcastAddress) ||
                   other.Contains(NetworkAddress) || other.Contains(BroadcastAddress);
        }

        public SCG.List<IPv4Network> Subnets(int prefixlenDiff = 1, int? newPrefix = null)
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

            var result = new SCG.List<IPv4Network>();
            int count = 1 << (targetPrefix - _prefixLength);
            uint subnetSize = 1U << (32 - targetPrefix);

            for (int i = 0; i < count; i++)
            {
                result.Add(new IPv4Network(_networkAddress + (uint)i * subnetSize, targetPrefix));
            }

            return result;
        }

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

        public bool SubnetOf(IPv4Network other)
        {
            return other._prefixLength < _prefixLength &&
                   other.Contains(NetworkAddress) && other.Contains(BroadcastAddress);
        }

        public bool SupernetOf(IPv4Network other)
        {
            return other.SubnetOf(this);
        }

        public override string ToString() => _toIpString(_networkAddress) + "/" + _prefixLength;

        public override int GetHashCode() => _networkAddress.GetHashCode() ^ _prefixLength;

        public override bool Equals(object? obj) => obj is IPv4Network other && Equals(other);

        public bool Equals(IPv4Network? other)
        {
            if (other == null) return false;
            return _networkAddress == other._networkAddress && _prefixLength == other._prefixLength;
        }

        public int CompareTo(IPv4Network? other)
        {
            if (other == null) return 1;
            int cmp = _networkAddress.CompareTo(other._networkAddress);
            if (cmp != 0) return cmp;
            return _prefixLength.CompareTo(other._prefixLength);
        }

        public static bool operator <(IPv4Network left, IPv4Network right) => left.CompareTo(right) < 0;
        public static bool operator >(IPv4Network left, IPv4Network right) => left.CompareTo(right) > 0;
        public static bool operator <=(IPv4Network left, IPv4Network right) => left.CompareTo(right) <= 0;
        public static bool operator >=(IPv4Network left, IPv4Network right) => left.CompareTo(right) >= 0;
        public static bool operator ==(IPv4Network? left, IPv4Network? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }
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
