using System;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace Sharpy
{
    /// <summary>
    /// Represents an IPv6 address.
    /// </summary>
    [SharpyModuleType("ipaddress")]
    public sealed class IPv6Address : IComparable<IPv6Address>, IEquatable<IPv6Address>
    {
        private readonly IPAddress _address;
        private readonly byte[] _bytes;

        /// <summary>
        /// Gets the IP version number.
        /// </summary>
        public int Version => 6;
        /// <summary>
        /// Gets the maximum prefix length for IPv6 addresses.
        /// </summary>
        public int MaxPrefixlen => 128;

        /// <summary>
        /// Gets whether the address is in a unique local range.
        /// </summary>
        public bool IsPrivate => (_bytes[0] & 0xFE) == 0xFC;

        /// <summary>
        /// Gets whether the address is a loopback address.
        /// </summary>
        public bool IsLoopback => IPAddress.IsLoopback(_address);

        /// <summary>
        /// Gets whether the address is a multicast address.
        /// </summary>
        public bool IsMulticast => _bytes[0] == 0xFF;

        /// <summary>
        /// Gets whether the address is link-local.
        /// </summary>
        public bool IsLinkLocal => _bytes[0] == 0xFE && (_bytes[1] & 0xC0) == 0x80;

        /// <summary>
        /// Gets whether the address is site-local.
        /// </summary>
        public bool IsSiteLocal => _bytes[0] == 0xFE && (_bytes[1] & 0xC0) == 0xC0;

        /// <summary>
        /// Gets whether the address is the unspecified address.
        /// </summary>
        public bool IsUnspecified => _address.Equals(IPAddress.IPv6None);

        /// <summary>
        /// Gets whether the address is in a reserved range.
        /// </summary>
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

        /// <summary>
        /// Gets whether the address is globally reachable.
        /// </summary>
        public bool IsGlobal => !IsPrivate && !IsLoopback && !IsLinkLocal && !IsSiteLocal &&
                                !IsMulticast && !IsReserved && !IsUnspecified;

        /// <summary>
        /// Gets the packed binary representation of the address.
        /// </summary>
        public Bytes Packed => new Bytes((byte[])_bytes.Clone());

        /// <summary>
        /// Gets the compressed string form of the address.
        /// </summary>
        public string Compressed => _address.ToString();

        /// <summary>
        /// Gets the fully expanded hexadecimal string form of the address.
        /// </summary>
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

        /// <summary>
        /// Gets the embedded IPv4 address if this is an IPv4-mapped IPv6 address.
        /// </summary>
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

        /// <summary>
        /// Initializes an IPv6 address from its string representation.
        /// </summary>
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

        /// <summary>
        /// Initializes an IPv6 address from its integer value.
        /// </summary>
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

        /// <summary>
        /// Initializes an IPv6 address from its packed 16-byte representation.
        /// </summary>
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

        /// <summary>
        /// Returns the integer value of the address.
        /// </summary>
        public BigInteger ToInt()
        {
            byte[] reversed = new byte[17];
            for (int i = 0; i < 16; i++)
            {
                reversed[15 - i] = _bytes[i];
            }
            return new BigInteger(reversed);
        }

        /// <summary>
        /// Returns the compressed string form of the address.
        /// </summary>
        public override string ToString() => _address.ToString();

        /// <summary>
        /// Returns a hash code for the address.
        /// </summary>
        public override int GetHashCode() => _address.GetHashCode();

        /// <summary>
        /// Determines whether the specified object is the same IPv6 address.
        /// </summary>
        public override bool Equals(object? obj) => obj is IPv6Address other && _address.Equals(other._address);

        /// <summary>
        /// Determines whether the specified address is the same IPv6 address.
        /// </summary>
        public bool Equals(IPv6Address? other) => other != null && _address.Equals(other._address);

        /// <summary>
        /// Compares this address with another IPv6 address.
        /// </summary>
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

        /// <summary>
        /// Returns a new address offset forward by the specified number of addresses.
        /// </summary>
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

        /// <summary>
        /// Returns a new address offset backward by the specified number of addresses.
        /// </summary>
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

        /// <summary>
        /// Determines whether one IPv6 address sorts before another.
        /// </summary>
        public static bool operator <(IPv6Address left, IPv6Address right) => left.CompareTo(right) < 0;
        /// <summary>
        /// Determines whether one IPv6 address sorts after another.
        /// </summary>
        public static bool operator >(IPv6Address left, IPv6Address right) => left.CompareTo(right) > 0;
        /// <summary>
        /// Determines whether one IPv6 address sorts before or the same as another.
        /// </summary>
        public static bool operator <=(IPv6Address left, IPv6Address right) => left.CompareTo(right) <= 0;
        /// <summary>
        /// Determines whether one IPv6 address sorts after or the same as another.
        /// </summary>
        public static bool operator >=(IPv6Address left, IPv6Address right) => left.CompareTo(right) >= 0;
        /// <summary>
        /// Determines whether two IPv6 addresses are equal.
        /// </summary>
        public static bool operator ==(IPv6Address? left, IPv6Address? right)
        {
            if (left is null)
                return right is null;
            return left.Equals(right);
        }
        /// <summary>
        /// Determines whether two IPv6 addresses are not equal.
        /// </summary>
        public static bool operator !=(IPv6Address? left, IPv6Address? right) => !(left == right);
    }
}
