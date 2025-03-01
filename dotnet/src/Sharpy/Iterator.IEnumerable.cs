using System.Collections;
using Sharpy.Collections.Interfaces;

namespace Sharpy
{
    public abstract partial class Iterator<T> : Iterable<T>
    {
        public sealed IEnumerator<T> GetEnumerator()
        {
            var nextValue = default(T);

            try
            {
                nextValue = __Next__();
            }
            catch (StopIteration)
            {
                nextValue = null;
            }

            yield return nextValue;
        }

        /// <remarks>
        /// Type-erased version of <see cref="GetEnumerator()"/>.
        /// </remarks>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
