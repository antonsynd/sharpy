namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the hash value of an object.
        /// Calls <see cref="object.GetHashCode()"/> on the given object.
        /// </summary>
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
