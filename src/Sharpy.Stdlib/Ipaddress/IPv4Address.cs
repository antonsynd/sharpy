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

        public bool IsPrivate
        {
            get
            {
                byte b0 = (byte)(_value >> 24);
                byte b1 = (byte)(_value >> 16);
                if (b0 == 10)
                    return true;
                if (b0 == 172 && b1 >= 16 && b1 <= 31)
                    return true;
                if (b0 == 192 && b1 == 168)
                    return true;
                return false;
            }
        }

        public bool IsLoopback => (_value >> 24) == 127;

        public bool IsMulticast
        {
            get
            {
                byte b0 = (byte)(_value >> 24);
                return b0 >= 224 && b0 <= 239;
            }
        }

        public bool IsReserved => (_value >> 24) >= 240;

        public bool IsLinkLocal
        {
            get
            {
                byte b0 = (byte)(_value >> 24);
                byte b1 = (byte)(_value >> 16);
                return b0 == 169 && b1 == 254;
            }
        }

        public bool IsGlobal => !IsPrivate && !IsLoopback && !IsLinkLocal && !IsMulticast && !IsReserved && !IsUnspecified;

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
