using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Address : IComparable<IPv6Address>, IEquatable<IPv6Address>
    {
        private readonly IPAddress _address;
        private readonly byte[] _bytes;

        public int Version => 6;
        public int MaxPrefixlen => 128;

        // CPython 3.12 _private_networks table for IPv6.
        internal static readonly (byte[] Network, int Prefix)[] PrivateNetworks = new[]
        {
            (new byte[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1}, 128),       // ::1/128
            (new byte[]{0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 128),       // ::/128
            (new byte[]{0,0,0,0,0,0,0,0,0,0,0xFF,0xFF,0,0,0,0}, 96),  // ::ffff:0.0.0.0/96
            (new byte[]{0,0x64,0xFF,0x9B,0,1,0,0,0,0,0,0,0,0,0,0}, 48), // 64:ff9b:1::/48
            (new byte[]{0x01,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 64),     // 100::/64
            (new byte[]{0x20,0x01,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 23),  // 2001::/23
            (new byte[]{0x20,0x01,0x0D,0xB8,0,0,0,0,0,0,0,0,0,0,0,0}, 32), // 2001:db8::/32
            (new byte[]{0x20,0x02,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 16),  // 2002::/16
            (new byte[]{0x3F,0xFF,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 20),  // 3fff::/20
            (new byte[]{0xFC,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 7),      // fc00::/7
            (new byte[]{0xFE,0x80,0,0,0,0,0,0,0,0,0,0,0,0,0,0}, 10),  // fe80::/10
        };

        // CPython 3.12 _private_networks_exceptions table for IPv6.
        internal static readonly (byte[] Network, int Prefix)[] PrivateNetworkExceptions = new[]
        {
            (new byte[]{0x20,0x01,0,1,0,0,0,0,0,0,0,0,0,0,0,1}, 128),   // 2001:1::1/128
            (new byte[]{0x20,0x01,0,1,0,0,0,0,0,0,0,0,0,0,0,2}, 128),   // 2001:1::2/128
            (new byte[]{0x20,0x01,0,3,0,0,0,0,0,0,0,0,0,0,0,0}, 32),    // 2001:3::/32
            (new byte[]{0x20,0x01,0,4,0x01,0x12,0,0,0,0,0,0,0,0,0,0}, 48), // 2001:4:112::/48
            (new byte[]{0x20,0x01,0,0x20,0,0,0,0,0,0,0,0,0,0,0,0}, 28), // 2001:20::/28
            (new byte[]{0x20,0x01,0,0x30,0,0,0,0,0,0,0,0,0,0,0,0}, 28), // 2001:30::/28
        };

        internal static bool InRange(byte[] addr, byte[] network, int prefix)
        {
            byte[] masked = IPv6Network.ApplyMask(addr, prefix);
            for (int i = 0; i < 16; i++)
            {
                if (masked[i] != network[i])
                    return false;
            }
            return true;
        }

        internal static bool InAnyPrivate(byte[] addr)
        {
            foreach (var (network, prefix) in PrivateNetworks)
            {
                if (InRange(addr, network, prefix))
                    return true;
            }
            return false;
        }

        internal static bool InAnyException(byte[] addr)
        {
            foreach (var (network, prefix) in PrivateNetworkExceptions)
            {
                if (InRange(addr, network, prefix))
                    return true;
            }
            return false;
        }

        public bool IsPrivate => InAnyPrivate(_bytes) && !InAnyException(_bytes);

        public bool IsLoopback => IPAddress.IsLoopback(_address);

        public bool IsMulticast => _bytes[0] == 0xFF;

        public bool IsLinkLocal => _bytes[0] == 0xFE && (_bytes[1] & 0xC0) == 0x80;

        public bool IsSiteLocal => _bytes[0] == 0xFE && (_bytes[1] & 0xC0) == 0xC0;

        public bool IsUnspecified => _address.Equals(IPAddress.IPv6None);

        public bool IsReserved
        {
            get
            {
                if (_bytes[0] == 0x20 && _bytes[1] == 0x01 && _bytes[2] == 0x0D && _bytes[3] == 0xB8)
                    return true;
                if (_bytes[0] == 0x01 && _bytes[1] == 0x00)
                    return true;
                return false;
            }
        }

        public bool IsGlobal => !IsPrivate;

        public Bytes Packed => new Bytes((byte[])_bytes.Clone());

        public string Compressed => _address.ToString();

        public string Exploded
        {
            get
            {
                var groups = new string[8];
                for (int i = 0; i < 8; i++)
                {
                    groups[i] = ((_bytes[i * 2] << 8) | _bytes[i * 2 + 1]).ToString("x4");
                }
                return string.Join(":", groups);
            }
        }

        public IPv4Address? Ipv4Mapped
        {
            get
            {
                for (int i = 0; i < 10; i++)
                {
                    if (_bytes[i] != 0)
                        return null;
                }
                if (_bytes[10] != 0xFF || _bytes[11] != 0xFF)
                    return null;

                uint v4 = ((uint)_bytes[12] << 24) | ((uint)_bytes[13] << 16) |
                          ((uint)_bytes[14] << 8) | _bytes[15];
                return new IPv4Address(v4);
            }
        }

        internal IPAddress Address => _address;

        public IPv6Address(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv6 address");
            }
            _address = parsed;
            _bytes = _address.GetAddressBytes();
        }

        public IPv6Address(BigInteger address)
        {
            if (address < 0 || address > BigInteger.Parse("340282366920938463463374607431768211455"))
            {
                throw new ValueError("'" + address + "' is not a valid IPv6 address integer");
            }

            _bytes = new byte[16];
            byte[] raw = address.ToByteArray();
            int copyLen = Math.Min(raw.Length, 16);
            for (int i = 0; i < copyLen; i++)
            {
                _bytes[15 - i] = raw[i];
            }
            _address = new IPAddress(_bytes);
        }

        public IPv6Address(Bytes packed)
        {
            if (packed.Length != 16)
            {
                throw new ValueError("Packed address must be exactly 16 bytes for IPv6");
            }
            _bytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                _bytes[i] = (byte)packed[i];
            }
            _address = new IPAddress(_bytes);
        }

        internal IPv6Address(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv6 address");
            }
            _address = address;
            _bytes = _address.GetAddressBytes();
        }

        public BigInteger ToInt()
        {
            byte[] reversed = new byte[17];
            for (int i = 0; i < 16; i++)
            {
                reversed[15 - i] = _bytes[i];
            }
            return new BigInteger(reversed);
        }

        public override string ToString() => _address.ToString();

        public override int GetHashCode() => _address.GetHashCode();

        public override bool Equals(object? obj) => obj is IPv6Address other && _address.Equals(other._address);

        public bool Equals(IPv6Address? other) => other != null && _address.Equals(other._address);

        public int CompareTo(IPv6Address? other)
        {
            if (other == null)
                return 1;
            for (int i = 0; i < 16; i++)
            {
                int cmp = _bytes[i].CompareTo(other._bytes[i]);
                if (cmp != 0)
                    return cmp;
            }
            return 0;
        }

        public static IPv6Address operator +(IPv6Address addr, int offset)
        {
            BigInteger result = addr.ToInt() + offset;
            BigInteger max = BigInteger.Parse("340282366920938463463374607431768211455");
            if (result < 0 || result > max)
            {
                throw new ValueError("Result address is out of range for IPv6");
            }
            return new IPv6Address(result);
        }

        public static IPv6Address operator -(IPv6Address addr, int offset)
        {
            BigInteger result = addr.ToInt() - offset;
            BigInteger max = BigInteger.Parse("340282366920938463463374607431768211455");
            if (result < 0 || result > max)
            {
                throw new ValueError("Result address is out of range for IPv6");
            }
            return new IPv6Address(result);
        }

        public static bool operator <(IPv6Address left, IPv6Address right) => left.CompareTo(right) < 0;
        public static bool operator >(IPv6Address left, IPv6Address right) => left.CompareTo(right) > 0;
        public static bool operator <=(IPv6Address left, IPv6Address right) => left.CompareTo(right) <= 0;
        public static bool operator >=(IPv6Address left, IPv6Address right) => left.CompareTo(right) >= 0;
        public static bool operator ==(IPv6Address? left, IPv6Address? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }
        public static bool operator !=(IPv6Address? left, IPv6Address? right) => !(left == right);
    }
}
