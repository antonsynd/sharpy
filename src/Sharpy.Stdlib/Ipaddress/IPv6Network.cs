using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Network : IEnumerable<IPv6Address>, IEquatable<IPv6Network>, IComparable<IPv6Network>
    {
        private readonly byte[] _networkAddress;
        private readonly int _prefixLength;

        public int Version => 6;
        public int Prefixlen => _prefixLength;
        public int MaxPrefixlen => 128;

        public IPv6Address NetworkAddress => new IPv6Address(new IPAddress((byte[])_networkAddress.Clone()));

        public IPv6Address BroadcastAddress
        {
            get
            {
                byte[] last = (byte[])_networkAddress.Clone();
                int fullBytes = _prefixLength / 8;
                int remainBits = _prefixLength % 8;

                if (fullBytes < 16 && remainBits > 0)
                {
                    last[fullBytes] |= (byte)(0xFF >> remainBits);
                    fullBytes++;
                }
                for (int i = fullBytes; i < 16; i++)
                {
                    last[i] = 0xFF;
                }
                return new IPv6Address(new IPAddress(last));
            }
        }

        public IPv6Address Netmask
        {
            get
            {
                byte[] mask = ComputeMask(_prefixLength);
                return new IPv6Address(new IPAddress(mask));
            }
        }

        public IPv6Address Hostmask
        {
            get
            {
                byte[] mask = ComputeMask(_prefixLength);
                byte[] host = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    host[i] = (byte)(~mask[i] & 0xFF);
                }
                return new IPv6Address(new IPAddress(host));
            }
        }

        public BigInteger NumAddresses
        {
            get
            {
                int hostBits = 128 - _prefixLength;
                return BigInteger.One << hostBits;
            }
        }

        public bool IsPrivate => NetworkAddress.IsPrivate;
        public bool IsLoopback => NetworkAddress.IsLoopback;
        public bool IsMulticast => NetworkAddress.IsMulticast;
        public bool IsReserved => NetworkAddress.IsReserved;
        public bool IsLinkLocal => NetworkAddress.IsLinkLocal;
        public bool IsGlobal => NetworkAddress.IsGlobal;

        public string WithPrefixlen => NetworkAddress + "/" + _prefixLength;
        public string WithNetmask => NetworkAddress + "/" + Netmask;
        public string WithHostmask => NetworkAddress + "/" + Hostmask;

        public IPv6Network(string address, bool strict = true)
        {
            int slashIdx = address.IndexOf('/');
            string addrPart;
            int prefix;

            if (slashIdx >= 0)
            {
                addrPart = address.Substring(0, slashIdx);
                if (!int.TryParse(address.Substring(slashIdx + 1), out prefix))
                {
                    throw new ValueError("'" + address + "' is not a valid IPv6 network");
                }
            }
            else
            {
                addrPart = address;
                prefix = 128;
            }

            if (!IPAddress.TryParse(addrPart, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv6 network");
            }

            if (prefix < 0 || prefix > 128)
            {
                throw new ValueError("'" + prefix + "' is not a valid prefix length");
            }

            byte[] bytes = parsed.GetAddressBytes();
            byte[] masked = ApplyMask(bytes, prefix);

            if (strict)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (bytes[i] != masked[i])
                    {
                        throw new ValueError("'" + address + "' has host bits set");
                    }
                }
            }

            _networkAddress = masked;
            _prefixLength = prefix;
        }

        internal IPv6Network(byte[] networkAddress, int prefixLength)
        {
            _networkAddress = (byte[])networkAddress.Clone();
            _prefixLength = prefixLength;
        }

        internal IPv6Network(IPAddress address, int prefixLength, bool strict)
        {
            if (prefixLength < 0 || prefixLength > 128)
            {
                throw new ValueError("'" + prefixLength + "' is not a valid prefix length");
            }

            byte[] bytes = address.GetAddressBytes();
            byte[] masked = ApplyMask(bytes, prefixLength);

            if (strict)
            {
                for (int i = 0; i < 16; i++)
                {
                    if (bytes[i] != masked[i])
                    {
                        throw new ValueError("'" + address + "/" + prefixLength + "' has host bits set");
                    }
                }
            }

            _networkAddress = masked;
            _prefixLength = prefixLength;
        }

        public bool Contains(IPv6Address address)
        {
            byte[] addrBytes = address.Address.GetAddressBytes();
            byte[] masked = ApplyMask(addrBytes, _prefixLength);

            for (int i = 0; i < 16; i++)
            {
                if (masked[i] != _networkAddress[i])
                    return false;
            }
            return true;
        }

        public bool Overlaps(IPv6Network other)
        {
            return Contains(other.NetworkAddress) || Contains(other.BroadcastAddress) ||
                   other.Contains(NetworkAddress) || other.Contains(BroadcastAddress);
        }

        public IEnumerable<IPv6Address> Hosts()
        {
            BigInteger network = NetworkAddress.ToInt();
            BigInteger last = network + NumAddresses - 1;
            // Python excludes only the Subnet-Router anycast (first address) for prefixes
            // shorter than /127; /127 and /128 yield all addresses.
            BigInteger start = _prefixLength >= 127 ? network : network + 1;
            for (BigInteger i = start; i <= last; i++)
            {
                yield return new IPv6Address(i);
            }
        }

        public IEnumerator<IPv6Address> GetEnumerator()
        {
            BigInteger network = NetworkAddress.ToInt();
            BigInteger last = network + NumAddresses - 1;
            for (BigInteger i = network; i <= last; i++)
            {
                yield return new IPv6Address(i);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public List<IPv6Network> Subnets(int prefixlenDiff = 1, int? newPrefix = null)
        {
            int targetPrefix;
            if (newPrefix != null)
            {
                targetPrefix = newPrefix.Value;
                if (targetPrefix <= _prefixLength || targetPrefix > 128)
                {
                    throw new ValueError("new prefix must be longer");
                }
            }
            else
            {
                targetPrefix = _prefixLength + prefixlenDiff;
                if (targetPrefix > 128)
                {
                    throw new ValueError("prefix length diff too large");
                }
            }

            var result = new List<IPv6Network>();
            BigInteger network = NetworkAddress.ToInt();
            BigInteger count = BigInteger.One << (targetPrefix - _prefixLength);
            BigInteger subnetSize = BigInteger.One << (128 - targetPrefix);

            for (BigInteger i = 0; i < count; i++)
            {
                byte[] subnetAddr = new IPv6Address(network + i * subnetSize).Address.GetAddressBytes();
                result.Add(new IPv6Network(subnetAddr, targetPrefix));
            }

            return result;
        }

        public IPv6Network Supernet(int prefixlenDiff = 1, int? newPrefix = null)
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

            byte[] masked = ApplyMask(_networkAddress, targetPrefix);
            return new IPv6Network(masked, targetPrefix);
        }

        public bool SubnetOf(IPv6Network other)
        {
            return other._prefixLength < _prefixLength &&
                   other.Contains(NetworkAddress) && other.Contains(BroadcastAddress);
        }

        public bool SupernetOf(IPv6Network other)
        {
            return other.SubnetOf(this);
        }

        public override string ToString() => new IPAddress((byte[])_networkAddress.Clone()) + "/" + _prefixLength;

        public override int GetHashCode()
        {
            int hash = _prefixLength;
            for (int i = 0; i < 16; i++)
            {
                hash = hash * 31 + _networkAddress[i];
            }
            return hash;
        }

        public override bool Equals(object? obj) => obj is IPv6Network other && Equals(other);

        public bool Equals(IPv6Network? other)
        {
            if (other == null)
                return false;
            if (_prefixLength != other._prefixLength)
                return false;
            for (int i = 0; i < 16; i++)
            {
                if (_networkAddress[i] != other._networkAddress[i])
                    return false;
            }
            return true;
        }

        public int CompareTo(IPv6Network? other)
        {
            if (other == null)
                return 1;
            for (int i = 0; i < 16; i++)
            {
                int cmp = _networkAddress[i].CompareTo(other._networkAddress[i]);
                if (cmp != 0)
                    return cmp;
            }
            return _prefixLength.CompareTo(other._prefixLength);
        }

        public static bool operator ==(IPv6Network? left, IPv6Network? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }
        public static bool operator !=(IPv6Network? left, IPv6Network? right) => !(left == right);
        public static bool operator <(IPv6Network left, IPv6Network right) => left.CompareTo(right) < 0;
        public static bool operator >(IPv6Network left, IPv6Network right) => left.CompareTo(right) > 0;
        public static bool operator <=(IPv6Network left, IPv6Network right) => left.CompareTo(right) <= 0;
        public static bool operator >=(IPv6Network left, IPv6Network right) => left.CompareTo(right) >= 0;

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

        private static byte[] ComputeMask(int prefixLength)
        {
            byte[] mask = new byte[16];
            int fullBytes = prefixLength / 8;
            int remainBits = prefixLength % 8;

            for (int i = 0; i < fullBytes && i < 16; i++)
            {
                mask[i] = 0xFF;
            }

            if (fullBytes < 16 && remainBits > 0)
            {
                mask[fullBytes] = (byte)(0xFF << (8 - remainBits));
            }

            return mask;
        }
    }
}
