using System;

namespace Sharpy
{
    [SharpyModuleType("uuid")]
    public sealed class UUID : IEquatable<UUID>, IComparable<UUID>
    {
        private readonly Guid _guid;

        public UUID(string hex)
        {
            if (hex == null)
            {
                throw new ValueError("UUID string must not be null");
            }

            if (!Guid.TryParse(hex, out var guid))
            {
                throw new ValueError("badly formed hexadecimal UUID string: '" + hex + "'");
            }

            _guid = guid;
        }

        internal UUID(Guid guid)
        {
            _guid = guid;
        }

        public string Hex => _guid.ToString("N");

        public long Int
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                long result = 0;
                for (int i = 0; i < 8; i++)
                {
                    result = (result << 8) | bytes[i];
                }

                return result;
            }
        }

        public Bytes UuidBytes
        {
            get
            {
                return new Bytes(ToRfc4122Bytes());
            }
        }

        public int Version
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[6] >> 4) & 0x0F;
            }
        }

        public string Variant
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                int highBits = (bytes[8] >> 4) & 0x0F;
                if ((highBits & 0x08) == 0)
                {
                    return "reserved for NCS compatibility";
                }

                if ((highBits & 0x0C) == 0x08)
                {
                    return "specified in RFC 4122";
                }

                if ((highBits & 0x0E) == 0x0C)
                {
                    return "reserved for Microsoft compatibility";
                }

                return "reserved for future definition";
            }
        }

        public string Urn => "urn:uuid:" + ToString();

        public long TimeLow
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
            }
        }

        public int TimeMid
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[4] << 8) | bytes[5];
            }
        }

        public int TimeHiVersion
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return (bytes[6] << 8) | bytes[7];
            }
        }

        public int ClockSeqHiVariant
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return bytes[8];
            }
        }

        public int ClockSeqLow
        {
            get
            {
                var bytes = ToRfc4122Bytes();
                return bytes[9];
            }
        }

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

        public override string ToString()
        {
            return _guid.ToString("D");
        }

        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            return obj is UUID other && _guid.Equals(other._guid);
        }

        public bool Equals(UUID? other)
        {
            return other != null && _guid.Equals(other._guid);
        }

        public int CompareTo(UUID? other)
        {
            if (other == null)
            {
                return 1;
            }

            return _guid.CompareTo(other._guid);
        }

        internal byte[] ToRfc4122Bytes()
        {
            var bytes = _guid.ToByteArray();
            var rfc = new byte[16];
            rfc[0] = bytes[3];
            rfc[1] = bytes[2];
            rfc[2] = bytes[1];
            rfc[3] = bytes[0];
            rfc[4] = bytes[5];
            rfc[5] = bytes[4];
            rfc[6] = bytes[7];
            rfc[7] = bytes[6];
            Array.Copy(bytes, 8, rfc, 8, 8);
            return rfc;
        }

        internal static UUID FromRfc4122Bytes(byte[] rfc4122Bytes)
        {
            if (rfc4122Bytes.Length != 16)
            {
                throw new ValueError("exactly 16 bytes required");
            }

            var netBytes = new byte[16];
            netBytes[0] = rfc4122Bytes[3];
            netBytes[1] = rfc4122Bytes[2];
            netBytes[2] = rfc4122Bytes[1];
            netBytes[3] = rfc4122Bytes[0];
            netBytes[4] = rfc4122Bytes[5];
            netBytes[5] = rfc4122Bytes[4];
            netBytes[6] = rfc4122Bytes[7];
            netBytes[7] = rfc4122Bytes[6];
            Array.Copy(rfc4122Bytes, 8, netBytes, 8, 8);

            return new UUID(new Guid(netBytes));
        }
    }
}
