namespace Sharpy
{
    public static partial class __Exports
    {
        /// <summary>
        /// Return a string containing a printable representation of an object.
        /// </summary>
        /// <remarks>
        /// By default, the representation is a string enclosed in angle
        /// brackets that contains the id of the object. A class can control
        /// what this function returns for its instances by overriding the
        /// <see cref="Object.__Repr__()"/> method.
        /// </remarks>
        public static string Repr(Object? obj)
        {
            return obj?.__Repr__() ?? "None";
        }

        /// <summary>
        /// Return a string containing a printable representation of the
        /// value type or struct. This essentially makes an attempt to return
        /// a string that would yield the same value when written in code.
        /// </summary>
        /// <remarks>
        /// This actually applies to all C# objects, but overload resolution
        /// should prefer the <see cref="Object"/> version for Sharpy objects.
        /// </remarks>
        public static string Repr(object? x)
        {
            return x?.ToString() ?? "None";
        }
    }
}
