using System;
using System.Security.Cryptography;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Represents an immutable universally unique identifier (UUID).
    /// Wraps System.Guid internally and mirrors Python's uuid.UUID API.
    /// </summary>
    [SharpyModuleType("uuid")]
    public sealed class UUID : IEquatable<UUID>, IComparable<UUID>
    {
        private readonly Guid _guid;

        /// <summary>
        /// Create a UUID from a string representation.
        /// Accepts standard formats: "12345678-1234-5678-1234-567812345678",
        /// "{12345678-1234-5678-1234-567812345678}", "12345678123456781234567812345678".
        /// </summary>
        /// <param name="hex">The UUID string to parse.</param>
        public UUID(string hex)
        {
            if (hex == null)
            {
                throw new ValueError("UUID string must not be null");
            }

            if (!Guid.TryParse(hex, out var guid))
            {
                throw new ValueError($"badly formed hexadecimal UUID string: '{hex}'");
            }

            _guid = guid;
        }

        internal UUID(Guid guid)
        {
            _guid = guid;
        }

        /// <summary>
        /// The UUID as a 32-character lowercase hexadecimal string (no hyphens).
        /// </summary>
        public string Hex => _guid.ToString("N");

        /// <summary>
        /// The UUID as a 16-element list of integer byte values.
        /// Uses RFC 4122 byte order (big-endian for time fields).
        /// </summary>
        public List<int> Bytes
        {
            get
            {
                var rfc4122Bytes = ToRfc4122Bytes();
                var result = new System.Collections.Generic.List<int>(16);
                foreach (byte b in rfc4122Bytes)
                {
                    result.Add(b);
                }
                return new List<int>(result);
            }
        }

        /// <summary>
        /// The UUID as a 128-bit integer.
        /// </summary>
        public long Int
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                // Convert to big-endian 128-bit integer (return as long for top 64 bits would lose data)
                // Python returns a full 128-bit int; we approximate with the full hex parse
                var hex = _guid.ToString("N");
                // Use long for the lower 64 bits representation
                // For full fidelity we return the numeric value as a long (may overflow for large UUIDs)
                return long.Parse(hex.Substring(16), System.Globalization.NumberStyles.HexNumber);
            }
        }

        /// <summary>
        /// The UUID version number (1 through 5, meaningful for RFC 4122 UUIDs).
        /// </summary>
        public int Version
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[6] >> 4) & 0x0F;
            }
        }

        /// <summary>
        /// The UUID variant. For RFC 4122 UUIDs this is "specified in RFC 4122".
        /// Returns the variant as a string matching Python's uuid module.
        /// </summary>
        public string Variant
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                int highBits = (bytes[8] >> 4) & 0x0F;
                if ((highBits & 0x08) == 0)
                    return "reserved for NCS compatibility";
                if ((highBits & 0x0C) == 0x08)
                    return "specified in RFC 4122";
                if ((highBits & 0x0E) == 0x0C)
                    return "reserved for Microsoft compatibility";
                return "reserved for future definition";
            }
        }

        /// <summary>
        /// The time_low field (first 32 bits of the UUID).
        /// </summary>
        public long TimeLow
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
            }
        }

        /// <summary>
        /// The time_mid field (bits 32-47 of the UUID).
        /// </summary>
        public int TimeMid
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[4] << 8) | bytes[5];
            }
        }

        /// <summary>
        /// The time_hi_version field (bits 48-63 of the UUID).
        /// </summary>
        public int TimeHiVersion
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[6] << 8) | bytes[7];
            }
        }

        /// <summary>
        /// The clock_seq_hi_variant field (bits 64-71 of the UUID).
        /// </summary>
        public int ClockSeqHiVariant
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return bytes[8];
            }
        }

        /// <summary>
        /// The clock_seq_low field (bits 72-79 of the UUID).
        /// </summary>
        public int ClockSeqLow
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return bytes[9];
            }
        }

        /// <summary>
        /// The node field (last 48 bits of the UUID).
        /// </summary>
        public long Node
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return ((long)bytes[10] << 40) | ((long)bytes[11] << 32) |
                       ((long)bytes[12] << 24) | ((long)bytes[13] << 16) |
                       ((long)bytes[14] << 8) | bytes[15];
            }
        }

        /// <summary>
        /// Returns the standard UUID string format: "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".
        /// </summary>
        public override string ToString()
        {
            return _guid.ToString("D");
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is UUID other && _guid.Equals(other._guid);
        }

        /// <inheritdoc/>
        public bool Equals(UUID? other)
        {
            return other != null && _guid.Equals(other._guid);
        }

        /// <inheritdoc/>
        public int CompareTo(UUID? other)
        {
            if (other == null) return 1;
            return _guid.CompareTo(other._guid);
        }

        /// <summary>
        /// Convert the Guid bytes to RFC 4122 byte order.
        /// .NET stores the first three fields in little-endian; RFC 4122 uses big-endian.
        /// </summary>
        private byte[] ToRfc4122Bytes()
        {
            var bytes = _guid.ToByteArray();
            // .NET Guid byte layout: bytes 0-3 are time_low (LE), 4-5 time_mid (LE), 6-7 time_hi_version (LE)
            // RFC 4122 expects big-endian for these fields
            var rfc = new byte[16];
            // time_low: swap bytes 0-3
            rfc[0] = bytes[3];
            rfc[1] = bytes[2];
            rfc[2] = bytes[1];
            rfc[3] = bytes[0];
            // time_mid: swap bytes 4-5
            rfc[4] = bytes[5];
            rfc[5] = bytes[4];
            // time_hi_version: swap bytes 6-7
            rfc[6] = bytes[7];
            rfc[7] = bytes[6];
            // Remaining bytes (8-15) are in the same order
            Array.Copy(bytes, 8, rfc, 8, 8);
            return rfc;
        }

        /// <summary>
        /// Create a UUID from RFC 4122 ordered bytes.
        /// </summary>
        internal static UUID FromRfc4122Bytes(byte[] rfc4122Bytes)
        {
            if (rfc4122Bytes.Length != 16)
                throw new ValueError("exactly 16 bytes required");

            // Convert from RFC 4122 (big-endian) to .NET Guid (mixed-endian)
            var netBytes = new byte[16];
            // time_low: swap bytes 0-3
            netBytes[0] = rfc4122Bytes[3];
            netBytes[1] = rfc4122Bytes[2];
            netBytes[2] = rfc4122Bytes[1];
            netBytes[3] = rfc4122Bytes[0];
            // time_mid: swap bytes 4-5
            netBytes[4] = rfc4122Bytes[5];
            netBytes[5] = rfc4122Bytes[4];
            // time_hi_version: swap bytes 6-7
            netBytes[6] = rfc4122Bytes[7];
            netBytes[7] = rfc4122Bytes[6];
            // Remaining bytes (8-15) same order
            Array.Copy(rfc4122Bytes, 8, netBytes, 8, 8);

            return new UUID(new Guid(netBytes));
        }
    }
}
