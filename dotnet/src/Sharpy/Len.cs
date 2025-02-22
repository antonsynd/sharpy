namespace Sharpy
{
    public static partial class Builtins
    {
        public static uint Len<T>(ISequence<T> sequence)
        {
            return sequence.Len();
        }
    }
}
