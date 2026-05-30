using System;

namespace Sharpy
{
    internal enum ByteOrder
    {
        /// <summary>@ — native byte order, native size and alignment.</summary>
        Native,
        /// <summary>= — native byte order, standard sizes.</summary>
        NativeStandard,
        /// <summary>&lt; — little-endian, standard sizes.</summary>
        LittleEndian,
        /// <summary>&gt; or ! — big-endian, standard sizes.</summary>
        BigEndian
    }

    internal sealed class FormatField
    {
        public char Type { get; }
        public int Count { get; }

        public FormatField(char type, int count)
        {
            Type = type;
            Count = count;
        }
    }

    internal sealed class FormatSpec
    {
        public ByteOrder ByteOrder { get; }
        public System.Collections.Generic.List<FormatField> Fields { get; }

        public FormatSpec(ByteOrder byteOrder, System.Collections.Generic.List<FormatField> fields)
        {
            ByteOrder = byteOrder;
            Fields = fields;
        }
    }

    internal static class FormatParser
    {
        private const string ValidFormatChars = "xbBhHiIlLqQfdsp?nN";

        public static FormatSpec Parse(string format)
        {
            if (format == null)
            {
                throw new StructError("format string must not be null");
            }

            int index = 0;
            ByteOrder byteOrder = ByteOrder.Native;

            // Parse optional byte order prefix
            if (format.Length > 0)
            {
                switch (format[0])
                {
                    case '@':
                        byteOrder = ByteOrder.Native;
                        index = 1;
                        break;
                    case '=':
                        byteOrder = ByteOrder.NativeStandard;
                        index = 1;
                        break;
                    case '<':
                        byteOrder = ByteOrder.LittleEndian;
                        index = 1;
                        break;
                    case '>':
                    case '!':
                        byteOrder = ByteOrder.BigEndian;
                        index = 1;
                        break;
                    default:
                        // No prefix: default to native
                        break;
                }
            }

            var fields = new System.Collections.Generic.List<FormatField>();

            while (index < format.Length)
            {
                char c = format[index];

                // Skip whitespace (Python struct ignores whitespace between format codes)
                if (char.IsWhiteSpace(c))
                {
                    index++;
                    continue;
                }

                // Parse optional repeat count
                int count = 0;
                bool hasCount = false;
                while (index < format.Length && format[index] >= '0' && format[index] <= '9')
                {
                    hasCount = true;
                    count = count * 10 + (format[index] - '0');
                    index++;
                }

                if (index >= format.Length)
                {
                    throw new StructError("repeat count given without format specifier");
                }

                char typeChar = format[index];
                index++;

                if (ValidFormatChars.IndexOf(typeChar) < 0)
                {
                    throw new StructError("bad char in struct format: '" + typeChar + "'");
                }

                if (!hasCount)
                {
                    count = 1;
                }

                fields.Add(new FormatField(typeChar, count));
            }

            return new FormatSpec(byteOrder, fields);
        }

        /// <summary>
        /// Calculate the total size in bytes for a parsed format spec.
        /// </summary>
        public static int CalcSize(FormatSpec spec)
        {
            int size = 0;
            foreach (FormatField field in spec.Fields)
            {
                size += GetFieldSize(field);
            }

            return size;
        }

        /// <summary>
        /// Get the number of values expected for packing (excludes pad bytes and counts
        /// 's'/'p' as single values regardless of count).
        /// </summary>
        public static int CountValues(FormatSpec spec)
        {
            int count = 0;
            foreach (FormatField field in spec.Fields)
            {
                switch (field.Type)
                {
                    case 'x':
                        // Pad bytes consume no values
                        break;
                    case 's':
                    case 'p':
                        // s and p are a single value regardless of count
                        count++;
                        break;
                    default:
                        count += field.Count;
                        break;
                }
            }

            return count;
        }

        private static int GetFieldSize(FormatField field)
        {
            switch (field.Type)
            {
                case 'x': return 1 * field.Count;   // pad bytes
                case 'b': return 1 * field.Count;   // signed byte
                case 'B': return 1 * field.Count;   // unsigned byte
                case '?': return 1 * field.Count;   // bool
                case 'h': return 2 * field.Count;   // int16
                case 'H': return 2 * field.Count;   // uint16
                case 'i': return 4 * field.Count;   // int32
                case 'I': return 4 * field.Count;   // uint32
                case 'l': return 4 * field.Count;   // int32 (same as 'i')
                case 'L': return 4 * field.Count;   // uint32 (same as 'I')
                case 'q': return 8 * field.Count;   // int64
                case 'Q': return 8 * field.Count;   // uint64
                case 'n': return 8 * field.Count;   // ssize_t (64-bit)
                case 'N': return 8 * field.Count;   // size_t (64-bit)
                case 'f': return 4 * field.Count;   // float32
                case 'd': return 8 * field.Count;   // float64
                case 's': return field.Count;        // char[] (count = byte length)
                case 'p': return field.Count;        // pascal string (count = byte length)
                default:
                    throw new StructError("bad char in struct format: '" + field.Type + "'");
            }
        }
    }
}
