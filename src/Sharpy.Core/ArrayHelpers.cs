namespace Sharpy
{
    /// <summary>
    /// Static helpers for CLR array operations emitted by the Sharpy compiler.
    /// Provides Python-style negative indexing for <c>array[T]</c> types.
    /// </summary>
    public static class ArrayHelpers
    {
        /// <summary>
        /// Gets an element from an array, supporting Python-style negative indexing.
        /// </summary>
        public static T GetItem<T>(T[] array, int index)
        {
            int n = array.Length;

            if ((uint)index < (uint)n)
            {
                return array[index];
            }

            // Slow path: a negative index in [-n, 0) counts from the end.
            if (index < 0 && index >= -n)
            {
                return array[index + n];
            }

            throw Sharpy.Index.ArrayOutOfRange();
        }

        /// <summary>
        /// Sets an element in an array, supporting Python-style negative indexing.
        /// </summary>
        public static void SetItem<T>(T[] array, int index, T value)
        {
            int n = array.Length;

            if ((uint)index < (uint)n)
            {
                array[index] = value;
                return;
            }

            // Slow path: a negative index in [-n, 0) counts from the end.
            if (index < 0 && index >= -n)
            {
                array[index + n] = value;
                return;
            }

            throw Sharpy.Index.ArrayOutOfRange();
        }
    }
}
