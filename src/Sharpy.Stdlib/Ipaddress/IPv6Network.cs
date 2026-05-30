using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Network : IEquatable<IPv6Network>, IComparable<IPv6Network>
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

        public BigInteger NumAddresses
        {
            get
            {
                int hostBits = 128 - _prefixLength;
                return BigInteger.One << hostBits;
            }
        }

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
                if (masked[i] != _networkAddress[i]) return false;
            }
            return true;
        }

        public bool Overlaps(IPv6Network other)
        {
            return Contains(other.NetworkAddress) || Contains(other.BroadcastAddress) ||
                   other.Contains(NetworkAddress) || other.Contains(BroadcastAddress);
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
            if (other == null) return false;
            if (_prefixLength != other._prefixLength) return false;
            for (int i = 0; i < 16; i++)
            {
                if (_networkAddress[i] != other._networkAddress[i]) return false;
            }
            return true;
        }

        public int CompareTo(IPv6Network? other)
        {
            if (other == null) return 1;
            for (int i = 0; i < 16; i++)
            {
                int cmp = _networkAddress[i].CompareTo(other._networkAddress[i]);
                if (cmp != 0) return cmp;
            }
            return _prefixLength.CompareTo(other._prefixLength);
        }

        public static bool operator ==(IPv6Network? left, IPv6Network? right)
        {
            if (left is null) return right is null;
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
