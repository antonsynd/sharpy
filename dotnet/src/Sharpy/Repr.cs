namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return a string containing a printable representation of an object.
        /// By default, the representation is a string enclosed in angle
        /// brackets that contains the id of the object. A class can control
        /// what this function returns for its instances by overriding the
        /// <see cref="Object.__Repr__()"/> method.
        /// </summary>
        public static string Repr(Object obj)
        {
            return obj.__Repr__();
        }

        /// <summary>
        /// Return a string containing a printable representation of the
        /// value type or struct. This essentially makes an attempt to return
        /// a string that would yield the same value when written in code.
        /// </summary>
        public static string Repr<T>(T x) where T : struct
        {
            return x.ToString() ?? "";
        }
    }
}
