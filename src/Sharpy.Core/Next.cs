namespace Sharpy
{
    public static partial class Builtins
    {
        /// <summary>
        /// Retrieve the next item from the iterator by calling its Next() method.
        /// If the iterator is exhausted, a StopIteration exception is raised.
        /// </summary>
        /// <typeparam name="T">The type of elements in the iterator</typeparam>
        /// <param name="iterator">The iterator to advance</param>
        /// <returns>The next item from the iterator</returns>
        /// <exception cref="StopIteration">Thrown when the iterator is exhausted</exception>
        /// <example>
        /// <code>
        /// it = iter([1, 2, 3])
        /// next(it)    # 1
        /// next(it)    # 2
        /// next(it)    # 3
        /// </code>
        /// </example>
        public static T Next<T>(Iterator<T> iterator)
        {
            if (iterator is null)
            {
                throw TypeError.ArgNone("next", "iterator");
            }

            return iterator.Next();
        }
    }
}
