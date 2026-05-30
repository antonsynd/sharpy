using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Sharpy
{
    public static partial class StructModule
    {
        /// <summary>
        /// Pack values according to the format string and return as Bytes.
        /// </summary>
        public static Bytes Pack(string format, params object[] values)
        {
            FormatSpec spec = FormatParser.Parse(format);
            return PackWithSpec(spec, values);
        }

        /// <summary>
        /// Unpack binary data according to the format string.
        /// </summary>
        public static List<object> Unpack(string format, Bytes buffer)
        {
            FormatSpec spec = FormatParser.Parse(format);
            return UnpackWithSpec(spec, buffer, 0);
        }

        /// <summary>
        /// Unpack binary data from a given offset according to the format string.
        /// </summary>
        public static List<object> UnpackFrom(string format, Bytes buffer, int offset = 0)
        {
            FormatSpec spec = FormatParser.Parse(format);
            return UnpackFromWithSpec(spec, buffer, offset);
        }

        /// <summary>
        /// Calculate the size (in bytes) of the struct described by the format string.
        /// </summary>
        public static int Calcsize(string format)
        {
            FormatSpec spec = FormatParser.Parse(format);
            return FormatParser.CalcSize(spec);
        }

        /// <summary>
        /// Iteratively unpack from buffer according to the format string.
        /// </summary>
        public static IEnumerable<List<object>> IterUnpack(string format, Bytes buffer)
        {
            FormatSpec spec = FormatParser.Parse(format);
            return IterUnpackWithSpec(spec, buffer);
        }

        internal static Bytes PackWithSpec(FormatSpec spec, object[] values)
        {
            int totalSize = FormatParser.CalcSize(spec);
            int expectedValues = FormatParser.CountValues(spec);

            if (values.Length != expectedValues)
            {
                throw new StructError(
                    "pack expected " + expectedValues.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " items for packing (got " + values.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            }

            byte[] result = new byte[totalSize];
            bool isLittleEndian = GetIsLittleEndian(spec.ByteOrder);
            int offset = 0;
            int valueIndex = 0;

            foreach (FormatField field in spec.Fields)
            {
                switch (field.Type)
                {
                    case 'x':
                        // Pad bytes — write zeros
                        for (int i = 0; i < field.Count; i++)
                        {
                            result[offset++] = 0;
                        }
                        break;

                    case 's':
                        PackBytes(result, ref offset, values[valueIndex++], field.Count);
                        break;

                    case 'p':
                        PackPascalString(result, ref offset, values[valueIndex++], field.Count);
                        break;

                    default:
                        for (int i = 0; i < field.Count; i++)
                        {
                            PackSingleValue(result, ref offset, field.Type, values[valueIndex++], isLittleEndian);
                        }
                        break;
                }
            }

            return new Bytes(result);
        }

        internal static List<object> UnpackWithSpec(FormatSpec spec, Bytes buffer, int startOffset)
        {
            int totalSize = FormatParser.CalcSize(spec);
            byte[] data = buffer.ToArray();

            if (data.Length - startOffset < totalSize)
            {
                throw new StructError(
                    "unpack requires a buffer of at least " + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes for unpacking " + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes (got "
                    + (data.Length - startOffset).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            }

            if (startOffset == 0 && data.Length != totalSize)
            {
                throw new StructError(
                    "unpack requires a buffer of " + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes (got " + data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            }

            return UnpackFromData(spec, data, startOffset);
        }

        internal static List<object> UnpackFromWithSpec(FormatSpec spec, Bytes buffer, int offset)
        {
            int totalSize = FormatParser.CalcSize(spec);
            byte[] data = buffer.ToArray();

            if (offset < 0)
            {
                throw new StructError("offset must be non-negative");
            }

            if (data.Length - offset < totalSize)
            {
                throw new StructError(
                    "unpack_from requires a buffer of at least "
                    + (offset + totalSize).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes for unpacking " + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " bytes at offset " + offset.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " (got " + data.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")");
            }

            return UnpackFromData(spec, data, offset);
        }

        internal static IEnumerable<List<object>> IterUnpackWithSpec(FormatSpec spec, Bytes buffer)
        {
            int totalSize = FormatParser.CalcSize(spec);
            byte[] data = buffer.ToArray();

            if (totalSize == 0)
            {
                throw new StructError("cannot iteratively unpack with a struct of length 0");
            }

            if (data.Length % totalSize != 0)
            {
                throw new StructError(
                    "iterative unpacking requires a buffer of a multiple of "
                    + totalSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + " bytes");
            }

            for (int offset = 0; offset < data.Length; offset += totalSize)
            {
                yield return UnpackFromData(spec, data, offset);
            }
        }

        private static List<object> UnpackFromData(FormatSpec spec, byte[] data, int startOffset)
        {
            bool isLittleEndian = GetIsLittleEndian(spec.ByteOrder);
            int offset = startOffset;
            var result = new System.Collections.Generic.List<object>();

            foreach (FormatField field in spec.Fields)
            {
                switch (field.Type)
                {
                    case 'x':
                        // Pad bytes — skip
                        offset += field.Count;
                        break;

                    case 's':
                        result.Add(UnpackBytes(data, ref offset, field.Count));
                        break;

                    case 'p':
                        result.Add(UnpackPascalString(data, ref offset, field.Count));
                        break;

                    default:
                        for (int i = 0; i < field.Count; i++)
                        {
                            result.Add(UnpackSingleValue(data, ref offset, field.Type, isLittleEndian));
                        }
                        break;
                }
            }

            return new List<object>(result);
        }

        private static bool GetIsLittleEndian(ByteOrder order)
        {
            switch (order)
            {
                case ByteOrder.LittleEndian:
                    return true;
                case ByteOrder.BigEndian:
                    return false;
                case ByteOrder.Native:
                case ByteOrder.NativeStandard:
                    return BitConverter.IsLittleEndian;
                default:
                    return BitConverter.IsLittleEndian;
            }
        }

        private static void PackSingleValue(byte[] buffer, ref int offset, char type, object value, bool isLittleEndian)
        {
            Span<byte> span = buffer.AsSpan(offset);

            switch (type)
            {
                case 'b':
                    {
                        int v = ConvertToInt(value);
                        if (v < -128 || v > 127)
                        {
                            throw new StructError("byte format requires -128 <= number <= 127");
                        }
                        buffer[offset] = unchecked((byte)(sbyte)v);
                        offset += 1;
                        break;
                    }

                case 'B':
                    {
                        int v = ConvertToInt(value);
                        if (v < 0 || v > 255)
                        {
                            throw new StructError("ubyte format requires 0 <= number <= 255");
                        }
                        buffer[offset] = (byte)v;
                        offset += 1;
                        break;
                    }

                case '?':
                    {
                        bool b = ConvertToBool(value);
                        buffer[offset] = b ? (byte)1 : (byte)0;
                        offset += 1;
                        break;
                    }

                case 'h':
                    {
                        int v = ConvertToInt(value);
                        if (v < short.MinValue || v > short.MaxValue)
                        {
                            throw new StructError("short format requires " + short.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture) + " <= number <= " + short.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteInt16LittleEndian(span, (short)v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteInt16BigEndian(span, (short)v);
                        }
                        offset += 2;
                        break;
                    }

                case 'H':
                    {
                        int v = ConvertToInt(value);
                        if (v < 0 || v > ushort.MaxValue)
                        {
                            throw new StructError("ushort format requires 0 <= number <= " + ushort.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)v);
                        }
                        offset += 2;
                        break;
                    }

                case 'i':
                case 'l':
                    {
                        int v = ConvertToInt(value);
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteInt32LittleEndian(span, v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteInt32BigEndian(span, v);
                        }
                        offset += 4;
                        break;
                    }

                case 'I':
                case 'L':
                    {
                        long v = ConvertToLong(value);
                        if (v < 0 || v > uint.MaxValue)
                        {
                            throw new StructError("uint format requires 0 <= number <= " + uint.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt32BigEndian(span, (uint)v);
                        }
                        offset += 4;
                        break;
                    }

                case 'q':
                    {
                        long v = ConvertToLong(value);
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteInt64LittleEndian(span, v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteInt64BigEndian(span, v);
                        }
                        offset += 8;
                        break;
                    }

                case 'Q':
                    {
                        long v = ConvertToLong(value);
                        if (v < 0)
                        {
                            throw new StructError("uint64 format requires 0 <= number");
                        }
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteUInt64LittleEndian(span, (ulong)v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)v);
                        }
                        offset += 8;
                        break;
                    }

                case 'n':
                    {
                        long v = ConvertToLong(value);
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteInt64LittleEndian(span, v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteInt64BigEndian(span, v);
                        }
                        offset += 8;
                        break;
                    }

                case 'N':
                    {
                        long v = ConvertToLong(value);
                        if (v < 0)
                        {
                            throw new StructError("size_t format requires 0 <= number");
                        }
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteUInt64LittleEndian(span, (ulong)v);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)v);
                        }
                        offset += 8;
                        break;
                    }

                case 'f':
                    {
                        double dv = ConvertToDouble(value);
                        float fv = (float)dv;
#if NET10_0_OR_GREATER
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteSingleLittleEndian(span, fv);
                        }
                        else
                        {
                            BinaryPrimitives.WriteSingleBigEndian(span, fv);
                        }
#else
                    byte[] floatBytes = BitConverter.GetBytes(fv);
                    if (isLittleEndian != BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(floatBytes);
                    }
                    floatBytes.CopyTo(buffer, offset);
#endif
                        offset += 4;
                        break;
                    }

                case 'd':
                    {
                        double dv = ConvertToDouble(value);
#if NET10_0_OR_GREATER
                        if (isLittleEndian)
                        {
                            BinaryPrimitives.WriteDoubleLittleEndian(span, dv);
                        }
                        else
                        {
                            BinaryPrimitives.WriteDoubleBigEndian(span, dv);
                        }
#else
                    byte[] doubleBytes = BitConverter.GetBytes(dv);
                    if (isLittleEndian != BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(doubleBytes);
                    }
                    doubleBytes.CopyTo(buffer, offset);
#endif
                        offset += 8;
                        break;
                    }

                default:
                    throw new StructError("bad char in struct format: '" + type + "'");
            }
        }

        private static object UnpackSingleValue(byte[] data, ref int offset, char type, bool isLittleEndian)
        {
            ReadOnlySpan<byte> span = data.AsSpan(offset);

            switch (type)
            {
                case 'b':
                    {
                        sbyte v = unchecked((sbyte)data[offset]);
                        offset += 1;
                        return (int)v;
                    }

                case 'B':
                    {
                        int v = data[offset];
                        offset += 1;
                        return v;
                    }

                case '?':
                    {
                        bool v = data[offset] != 0;
                        offset += 1;
                        return v;
                    }

                case 'h':
                    {
                        short v = isLittleEndian
                            ? BinaryPrimitives.ReadInt16LittleEndian(span)
                            : BinaryPrimitives.ReadInt16BigEndian(span);
                        offset += 2;
                        return (int)v;
                    }

                case 'H':
                    {
                        ushort v = isLittleEndian
                            ? BinaryPrimitives.ReadUInt16LittleEndian(span)
                            : BinaryPrimitives.ReadUInt16BigEndian(span);
                        offset += 2;
                        return (int)v;
                    }

                case 'i':
                case 'l':
                    {
                        int v = isLittleEndian
                            ? BinaryPrimitives.ReadInt32LittleEndian(span)
                            : BinaryPrimitives.ReadInt32BigEndian(span);
                        offset += 4;
                        return v;
                    }

                case 'I':
                case 'L':
                    {
                        uint v = isLittleEndian
                            ? BinaryPrimitives.ReadUInt32LittleEndian(span)
                            : BinaryPrimitives.ReadUInt32BigEndian(span);
                        offset += 4;
                        // Return as int if it fits, otherwise as long (Python returns int for unsigned values too)
                        if (v <= int.MaxValue)
                        {
                            return (int)v;
                        }
                        return (long)v;
                    }

                case 'q':
                    {
                        long v = isLittleEndian
                            ? BinaryPrimitives.ReadInt64LittleEndian(span)
                            : BinaryPrimitives.ReadInt64BigEndian(span);
                        offset += 8;
                        return v;
                    }

                case 'Q':
                    {
                        ulong v = isLittleEndian
                            ? BinaryPrimitives.ReadUInt64LittleEndian(span)
                            : BinaryPrimitives.ReadUInt64BigEndian(span);
                        offset += 8;
                        // Sharpy uses long for large unsigned values
                        return (long)v;
                    }

                case 'n':
                    {
                        long v = isLittleEndian
                            ? BinaryPrimitives.ReadInt64LittleEndian(span)
                            : BinaryPrimitives.ReadInt64BigEndian(span);
                        offset += 8;
                        return v;
                    }

                case 'N':
                    {
                        ulong v = isLittleEndian
                            ? BinaryPrimitives.ReadUInt64LittleEndian(span)
                            : BinaryPrimitives.ReadUInt64BigEndian(span);
                        offset += 8;
                        return (long)v;
                    }

                case 'f':
                    {
#if NET10_0_OR_GREATER
                        float fv = isLittleEndian
                            ? BinaryPrimitives.ReadSingleLittleEndian(span)
                            : BinaryPrimitives.ReadSingleBigEndian(span);
#else
                    byte[] floatBytes = new byte[4];
                    Array.Copy(data, offset, floatBytes, 0, 4);
                    if (isLittleEndian != BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(floatBytes);
                    }
                    float fv = BitConverter.ToSingle(floatBytes, 0);
#endif
                        offset += 4;
                        return (double)fv;
                    }

                case 'd':
                    {
#if NET10_0_OR_GREATER
                        double dv = isLittleEndian
                            ? BinaryPrimitives.ReadDoubleLittleEndian(span)
                            : BinaryPrimitives.ReadDoubleBigEndian(span);
#else
                    byte[] doubleBytes = new byte[8];
                    Array.Copy(data, offset, doubleBytes, 0, 8);
                    if (isLittleEndian != BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(doubleBytes);
                    }
                    double dv = BitConverter.ToDouble(doubleBytes, 0);
#endif
                        offset += 8;
                        return dv;
                    }

                default:
                    throw new StructError("bad char in struct format: '" + type + "'");
            }
        }

        private static void PackBytes(byte[] buffer, ref int offset, object value, int count)
        {
            byte[] sourceBytes;

            if (value is Bytes bytesVal)
            {
                sourceBytes = bytesVal.ToArray();
            }
            else if (value is string strVal)
            {
                sourceBytes = Encoding.UTF8.GetBytes(strVal);
            }
            else
            {
                throw new StructError("argument for 's' must be a bytes-like object");
            }

            int copyLen = System.Math.Min(sourceBytes.Length, count);
            Array.Copy(sourceBytes, 0, buffer, offset, copyLen);
            // Zero-pad remaining bytes if source is shorter
            for (int i = copyLen; i < count; i++)
            {
                buffer[offset + i] = 0;
            }

            offset += count;
        }

        private static void PackPascalString(byte[] buffer, ref int offset, object value, int count)
        {
            if (count == 0)
            {
                return;
            }

            byte[] sourceBytes;

            if (value is Bytes bytesVal)
            {
                sourceBytes = bytesVal.ToArray();
            }
            else if (value is string strVal)
            {
                sourceBytes = Encoding.UTF8.GetBytes(strVal);
            }
            else
            {
                throw new StructError("argument for 'p' must be a bytes-like object");
            }

            // First byte is the length, max is count-1 or 255
            int maxLen = count - 1;
            if (maxLen > 255)
            {
                maxLen = 255;
            }

            int dataLen = System.Math.Min(sourceBytes.Length, maxLen);
            buffer[offset] = (byte)dataLen;

            if (dataLen > 0)
            {
                Array.Copy(sourceBytes, 0, buffer, offset + 1, dataLen);
            }

            // Zero-pad remaining bytes
            for (int i = dataLen + 1; i < count; i++)
            {
                buffer[offset + i] = 0;
            }

            offset += count;
        }

        private static Bytes UnpackBytes(byte[] data, ref int offset, int count)
        {
            byte[] result = new byte[count];
            Array.Copy(data, offset, result, 0, count);
            offset += count;
            return new Bytes(result);
        }

        private static Bytes UnpackPascalString(byte[] data, ref int offset, int count)
        {
            if (count == 0)
            {
                offset += count;
                return new Bytes(Array.Empty<byte>());
            }

            int strLen = data[offset];
            int maxLen = count - 1;
            if (strLen > maxLen)
            {
                strLen = maxLen;
            }

            byte[] result = new byte[strLen];
            if (strLen > 0)
            {
                Array.Copy(data, offset + 1, result, 0, strLen);
            }

            offset += count;
            return new Bytes(result);
        }

        private static int ConvertToInt(object value)
        {
            if (value is int intVal)
            {
                return intVal;
            }
            if (value is long longVal)
            {
                if (longVal < int.MinValue || longVal > int.MaxValue)
                {
                    throw new StructError("int too large to convert");
                }
                return (int)longVal;
            }
            if (value is short shortVal)
            {
                return shortVal;
            }
            if (value is byte byteVal)
            {
                return byteVal;
            }
            if (value is sbyte sbyteVal)
            {
                return sbyteVal;
            }

            try
            {
                return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new StructError("required argument is not an integer");
            }
        }

        private static long ConvertToLong(object value)
        {
            if (value is long longVal)
            {
                return longVal;
            }
            if (value is int intVal)
            {
                return intVal;
            }
            if (value is short shortVal)
            {
                return shortVal;
            }
            if (value is byte byteVal)
            {
                return byteVal;
            }
            if (value is sbyte sbyteVal)
            {
                return sbyteVal;
            }
            if (value is uint uintVal)
            {
                return uintVal;
            }
            if (value is ushort ushortVal)
            {
                return ushortVal;
            }

            try
            {
                return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new StructError("required argument is not an integer");
            }
        }

        private static double ConvertToDouble(object value)
        {
            if (value is double doubleVal)
            {
                return doubleVal;
            }
            if (value is float floatVal)
            {
                return floatVal;
            }
            if (value is int intVal)
            {
                return intVal;
            }
            if (value is long longVal)
            {
                return longVal;
            }

            try
            {
                return Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new StructError("required argument is not a float");
            }
        }

        private static bool ConvertToBool(object value)
        {
            if (value is bool boolVal)
            {
                return boolVal;
            }
            if (value is int intVal)
            {
                return intVal != 0;
            }
            if (value is long longVal)
            {
                return longVal != 0;
            }

            try
            {
                return Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                throw new StructError("required argument is not a bool");
            }
        }
    }
}
