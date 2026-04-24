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
            if (index < 0)
            {
                index = array.Length + index;
            }

            if (index < 0 || index >= array.Length)
            {
                throw new IndexError($"array index out of range");
            }

            return array[index];
        }

        /// <summary>
        /// Sets an element in an array, supporting Python-style negative indexing.
        /// </summary>
        public static void SetItem<T>(T[] array, int index, T value)
        {
            if (index < 0)
            {
                index = array.Length + index;
            }

            if (index < 0 || index >= array.Length)
            {
                throw new IndexError($"array index out of range");
            }

            array[index] = value;
        }
    }
}
