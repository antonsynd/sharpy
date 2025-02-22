namespace Sharpy
{
    public static partial class Builtins
    {
        public static uint Len<T>(Sequence<T> sequence)
        {
            return sequence.__Len__();
        }
    }
}
