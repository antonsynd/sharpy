using System.Collections;

namespace Sharpy.Core;

public abstract partial class Iterator<T>
{
    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator()
    {
        while (true)
        {
            T nextValue;

            try
            {
                nextValue = __Next__();
            }
            catch (StopIteration)
            {
                break;
            }

            yield return nextValue;
        }
    }

    /// <remarks>
    /// Type-erased version of <see cref="GetEnumerator()"/>.
    /// </remarks>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
