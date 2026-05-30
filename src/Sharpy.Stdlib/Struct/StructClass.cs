using System;
using System.Collections.Generic;

namespace Sharpy
{
    /// <summary>
    /// Pre-compiled struct format for repeated packing/unpacking operations.
    /// Corresponds to Python's struct.Struct class.
    /// </summary>
    [SharpyModuleType("struct", "Struct")]
    public sealed class StructClass
    {
        private readonly FormatSpec _spec;

        /// <summary>Gets the format string used to create this Struct instance.</summary>
        public string Format { get; }

        /// <summary>Gets the calculated size of the struct in bytes.</summary>
        public int Size { get; }

        /// <summary>
        /// Create a new Struct instance with a pre-compiled format string.
        /// </summary>
        public StructClass(string format)
        {
            Format = format ?? throw new StructError("format string must not be null");
            _spec = FormatParser.Parse(format);
            Size = FormatParser.CalcSize(_spec);
        }

        /// <summary>
        /// Pack values according to the pre-compiled format and return as Bytes.
        /// </summary>
        public Bytes Pack(params object[] values)
        {
            return StructModule.PackWithSpec(_spec, values);
        }

        /// <summary>
        /// Unpack binary data according to the pre-compiled format.
        /// </summary>
        public List<object> Unpack(Bytes buffer)
        {
            return StructModule.UnpackWithSpec(_spec, buffer, 0);
        }

        /// <summary>
        /// Unpack binary data from a given offset according to the pre-compiled format.
        /// </summary>
        public List<object> UnpackFrom(Bytes buffer, int offset = 0)
        {
            return StructModule.UnpackFromWithSpec(_spec, buffer, offset);
        }

        /// <summary>
        /// Iteratively unpack from buffer according to the pre-compiled format.
        /// </summary>
        public IEnumerable<List<object>> IterUnpack(Bytes buffer)
        {
            return StructModule.IterUnpackWithSpec(_spec, buffer);
        }
    }
}
