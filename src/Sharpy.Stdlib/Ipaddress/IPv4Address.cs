using System;
using System.Net;
using System.Net.Sockets;

namespace Sharpy
{
    [SharpyModuleType("ipaddress")]
    public sealed class IPv4Address : IComparable<IPv4Address>, IEquatable<IPv4Address>
    {
        private readonly uint _value;

        public int Version => 4;
        public int MaxPrefixlen => 32;

        // CPython 3.12 _private_networks table (IANA special-purpose ranges).
        internal static readonly (uint Network, int Prefix)[] PrivateNetworks = new[]
        {
            (0x00000000u, 8),   // 0.0.0.0/8
            (0x0A000000u, 8),   // 10.0.0.0/8
            (0x7F000000u, 8),   // 127.0.0.0/8
            (0xA9FE0000u, 16),  // 169.254.0.0/16
            (0xAC100000u, 12),  // 172.16.0.0/12
            (0xC0000000u, 24),  // 192.0.0.0/24
            (0xC00000AAu, 31),  // 192.0.0.170/31
            (0xC0000200u, 24),  // 192.0.2.0/24
            (0xC0A80000u, 16),  // 192.168.0.0/16
            (0xC6120000u, 15),  // 198.18.0.0/15
            (0xC6336400u, 24),  // 198.51.100.0/24
            (0xCB007100u, 24),  // 203.0.113.0/24
            (0xF0000000u, 4),   // 240.0.0.0/4
            (0xFFFFFFFFu, 32),  // 255.255.255.255/32
        };

        // CPython 3.12 _private_networks_exceptions table.
        internal static readonly (uint Network, int Prefix)[] PrivateNetworkExceptions = new[]
        {
            (0xC0000009u, 32),  // 192.0.0.9/32
            (0xC000000Au, 32),  // 192.0.0.10/32
        };

        // CPython 3.12 _public_network: 100.64.0.0/10
        internal const uint PublicNetwork = 0x64400000u;
        internal const int PublicNetworkPrefix = 10;

        // 240.0.0.0/4 — reserved/future-use range.
        internal const uint ReservedNetwork = 0xF0000000u;
        internal const int ReservedNetworkPrefix = 4;

        internal static bool InRange(uint addr, uint network, int prefix)
        {
            if (prefix == 0)
                return true;
            if (prefix == 32)
                return addr == network;
            uint mask = 0xFFFFFFFFu << (32 - prefix);
            return (addr & mask) == network;
        }

        internal static bool InAnyPrivate(uint addr)
        {
            foreach (var (network, prefix) in PrivateNetworks)
            {
                if (InRange(addr, network, prefix))
                    return true;
            }
            return false;
        }

        internal static bool InAnyException(uint addr)
        {
            foreach (var (network, prefix) in PrivateNetworkExceptions)
            {
                if (InRange(addr, network, prefix))
                    return true;
            }
            return false;
        }

        public bool IsPrivate => InAnyPrivate(_value) && !InAnyException(_value);

        public bool IsLoopback => (_value >> 24) == 127;

        public bool IsMulticast
        {
            get
            {
                byte b0 = (byte)(_value >> 24);
                return b0 >= 224 && b0 <= 239;
            }
        }

        public bool IsReserved => InRange(_value, ReservedNetwork, ReservedNetworkPrefix);

        public bool IsLinkLocal
        {
            get
            {
                byte b0 = (byte)(_value >> 24);
                byte b1 = (byte)(_value >> 16);
                return b0 == 169 && b1 == 254;
            }
        }

        public bool IsGlobal => !InRange(_value, PublicNetwork, PublicNetworkPrefix) && !IsPrivate;

        public bool IsUnspecified => _value == 0;

        public Bytes Packed
        {
            get
            {
                return new Bytes(new byte[]
                {
                    (byte)(_value >> 24),
                    (byte)(_value >> 16),
                    (byte)(_value >> 8),
                    (byte)(_value)
                });
            }
        }

        public string Compressed => ToString();

        internal uint Value => _value;

        public IPv4Address(string address)
        {
            if (!IPAddress.TryParse(address, out IPAddress? parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ValueError("'" + address + "' does not appear to be an IPv4 address");
            }
            _value = BytesToUint(parsed.GetAddressBytes());
        }

        public IPv4Address(long address)
        {
            if (address < 0 || address > 0xFFFFFFFF)
            {
                throw new ValueError("'" + address + "' is not a valid IPv4 address integer");
            }
            _value = (uint)address;
        }

        public IPv4Address(Bytes packed)
        {
            if (packed.Length != 4)
            {
                throw new ValueError("Packed address must be exactly 4 bytes for IPv4");
            }
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = (byte)packed[i];
            }
            _value = BytesToUint(bytes);
        }

        internal IPv4Address(uint value)
        {
            _value = value;
        }

        public long ToInt()
        {
            return _value;
        }

        public override string ToString()
        {
            return ((_value >> 24) & 0xFF) + "." +
                   ((_value >> 16) & 0xFF) + "." +
                   ((_value >> 8) & 0xFF) + "." +
                   (_value & 0xFF);
        }

        public override int GetHashCode()
        {
            return (int)_value;
        }

        public override bool Equals(object? obj)
        {
            return obj is IPv4Address other && _value == other._value;
        }

        public bool Equals(IPv4Address? other)
        {
            return other != null && _value == other._value;
        }

        public int CompareTo(IPv4Address? other)
        {
            if (other == null)
                return 1;
            return _value.CompareTo(other._value);
        }

        public static IPv4Address operator +(IPv4Address addr, int offset)
        {
            long result = (long)addr._value + offset;
            if (result < 0 || result > 0xFFFFFFFF)
            {
                throw new ValueError("Result address is out of range for IPv4");
            }
            return new IPv4Address((uint)result);
        }

        public static IPv4Address operator -(IPv4Address addr, int offset)
        {
            long result = (long)addr._value - offset;
            if (result < 0 || result > 0xFFFFFFFF)
            {
                throw new ValueError("Result address is out of range for IPv4");
            }
            return new IPv4Address((uint)result);
        }

        public static bool operator <(IPv4Address left, IPv4Address right) => left._value < right._value;
        public static bool operator >(IPv4Address left, IPv4Address right) => left._value > right._value;
        public static bool operator <=(IPv4Address left, IPv4Address right) => left._value <= right._value;
        public static bool operator >=(IPv4Address left, IPv4Address right) => left._value >= right._value;
        public static bool operator ==(IPv4Address? left, IPv4Address? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }
        public static bool operator !=(IPv4Address? left, IPv4Address? right) => !(left == right);

        internal static uint BytesToUint(byte[] bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        internal static byte[] UintToBytes(uint value)
        {
            return new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
        }
    }
}
