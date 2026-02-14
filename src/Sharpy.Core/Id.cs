using System.Runtime.CompilerServices;
namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Return the identity of an object.
        /// This is an integer which is guaranteed to be unique and constant
        /// for this object during its lifetime.
        /// Maps to RuntimeHelpers.GetHashCode() which returns the sync block index.
        /// </summary>
        public static int Id(object obj)
        {
            if (obj is null)
            {
                throw new TypeError("id() argument must not be None");
            }

            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
