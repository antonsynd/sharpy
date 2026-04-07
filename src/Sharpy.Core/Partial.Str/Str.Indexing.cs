using System;
using System.Text;

namespace Sharpy
{
    /// <summary>
    /// Indexing and slicing operations for Str.
    /// </summary>
    public readonly partial struct Str
    {
        /// <summary>
        /// Gets the character at the specified index as a single-character Str.
        /// Supports negative indexing.
        /// </summary>
        /// <exception cref="IndexError">Thrown if the index is out of range.</exception>
        public Str this[int index]
        {
            get
            {
                int actual = index < 0 ? Value.Length + index : index;
                if (actual < 0 || actual >= Value.Length)
                {
                    throw new IndexError($"string index out of range");
                }
                return new Str(Value[actual].ToString());
            }
        }

        /// <summary>
        /// Return a slice of the string using Python slice semantics.
        /// Target for <c>s[start:stop:step]</c> codegen.
        /// </summary>
        public Str Slice(int? start, int? stop, int? step)
        {
            return new Str(Sharpy.Slice.GetSlice(Value, start, stop, step));
        }
    }
}
