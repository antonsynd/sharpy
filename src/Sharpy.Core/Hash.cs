namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the hash value of an object.
        /// Calls <see cref="object.GetHashCode()"/> on the given object.
        /// </summary>
        /// <param name="obj">The object to hash</param>
        /// <returns>The hash value as an integer</returns>
        /// <example>
        /// <code>
        /// hash("hello")    # integer hash value
        /// hash(42)         # 42
        /// </code>
        /// </example>
        public static int Hash(object obj)
        {
            if (obj is null)
            {
                throw TypeError.ArgNone("hash", "hashable");
            }

            return obj.GetHashCode();
        }
    }
}
